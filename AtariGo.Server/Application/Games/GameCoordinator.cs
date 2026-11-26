using Microsoft.EntityFrameworkCore;

using AtariGo.Server.Concurrency;
using AtariGo.Server.Data;

namespace AtariGo.Server.Application.Games;

public sealed class GameCoordinator : IDisposable, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGameSessionRegistry _registry;
    private readonly GameOperationGate _operations;
    private readonly ILogger<GameCoordinator> _logger;

    public GameCoordinator(IServiceScopeFactory scopeFactory,
        IGameSessionRegistry registry, GameOperationGate operations,
        ILogger<GameCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _registry = registry;
        _operations = operations;
        _logger = logger;
    }

    public async Task<GameSessionConnection> ConnectAsync(GameSessionDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        using AsyncOperationLease lease = await _operations.EnterAsync(
            definition.GameId, cancellationToken);
        using IServiceScope scope = _scopeFactory.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationContext operation = await commands.LoadAsync(
            definition.GameId, cancellationToken);
        GameOperationResult advanced = await commands.AdvanceAsync(operation,
            () => _registry.AreBothConnected(definition.GameId));
        Publish(advanced);
        operation = await commands.LoadAsync(definition.GameId, cancellationToken);
        commands.EnsureReconnectAllowed(operation, definition.Player.PlayerKey);
        GameSessionConnection connection = _registry.Connect(definition with
        {
            Progress = operation.Progress,
            Version = operation.Version
        });
        try
        {
            GameOperationResult connected = await commands.PlayerConnectedAsync(operation,
                connection.PlayerKey);
            Publish(connected);
            GameOperationContext current = await commands.LoadAsync(
                definition.GameId, cancellationToken);
            Publish(await commands.AdvanceAsync(current,
                () => _registry.AreBothConnected(definition.GameId)));
            LogConnection(definition, operation.Progress);
        }
        catch
        {
            _registry.Disconnect(connection);
            connection.Dispose();
            throw;
        }
        return connection;
    }

    public async Task ProcessAsync(GameSessionConnection connection,
        GameActionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(command);
        using AsyncOperationLease lease = await _operations.EnterAsync(
            connection.GameId, cancellationToken);
        if (!_registry.IsCurrent(connection))
        {
            return;
        }
        using IServiceScope scope = _scopeFactory.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationResult result = await commands.ProcessAsync(connection, command,
            () => _registry.AreBothConnected(connection.GameId));
        Publish(result);
        if (result.ActionResult is not null)
        {
            _registry.PublishActionResult(connection, result.ActionResult);
        }
    }

    public async Task AdvanceAsync(long gameId, CancellationToken cancellationToken)
    {
        using AsyncOperationLease lease = await _operations.EnterAsync(gameId, cancellationToken);
        using IServiceScope scope = _scopeFactory.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationContext operation = await commands.LoadAsync(gameId, cancellationToken);
        GameOperationResult result = await commands.AdvanceAsync(operation,
            () => _registry.AreBothConnected(gameId));
        Publish(result);
    }

    public async Task DisconnectAsync(GameSessionConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!_registry.IsCurrent(connection))
        {
            return;
        }
        using AsyncOperationLease lease = await _operations.EnterAsync(
            connection.GameId, CancellationToken.None);
        if (!_registry.IsCurrent(connection))
        {
            return;
        }
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            GameCommandService commands = scope.ServiceProvider
                .GetRequiredService<GameCommandService>();
            GameOperationContext operation = await commands.LoadAsync(
                connection.GameId, CancellationToken.None);
            GameOperationResult disconnected = await commands.PlayerDisconnectedAsync(
                operation, connection.PlayerKey);
            _registry.Disconnect(connection);
            Publish(disconnected);
            LogDisconnection(connection, disconnected.Progress ?? operation.Progress);
        }
        finally
        {
            _registry.Disconnect(connection);
        }
    }

    public async Task AcknowledgeTurnAsync(GameSessionConnection connection, long version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using AsyncOperationLease lease = await _operations.EnterAsync(
            connection.GameId, cancellationToken);
        if (!_registry.IsCurrent(connection))
        {
            return;
        }
        using IServiceScope scope = _scopeFactory.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationContext operation = await commands.LoadAsync(connection.GameId,
            cancellationToken);
        GameOperationResult advanced = await commands.AdvanceAsync(operation,
            () => _registry.AreBothConnected(connection.GameId));
        Publish(advanced);
        operation = await commands.LoadAsync(connection.GameId, cancellationToken);
        GameOperationResult acknowledged = await commands.AcknowledgeTurnAsync(
            operation, connection.PlayerKey, version);
        Publish(acknowledged);
    }

    public async Task<ChatMessageSubmissionResult> PublishChatMessageAsync(
        GameSessionConnection connection, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(text);
        if (!GameSessionRules.TryGetUserId(connection.PlayerKey, out int userId))
        {
            return _registry.RejectChatMessage(
                connection, ChatMessageRejectionReason.GuestNotAllowed);
        }
        using IServiceScope scope = _scopeFactory.CreateScope();
        AtariGoDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<AtariGoDbContext>();
        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        DateTime now = DateTime.UnixEpoch.AddSeconds(
            timeProvider.GetUtcNow().ToUnixTimeSeconds());
        bool isMuted = await dbContext.Sanctions.AsNoTracking()
            .AnyAsync(sanction => sanction.UserId == userId
                && sanction.SanctionType == DatabaseValues.SanctionTypes.ChatMute
                && sanction.RevokedAt == null && sanction.StartedAt <= now
                && sanction.EndsAt > now, cancellationToken);
        if (isMuted)
        {
            return _registry.RejectChatMessage(connection, ChatMessageRejectionReason.Muted);
        }
        return _registry.PublishChatMessage(connection, text);
    }

    public void Dispose()
    {
        _operations.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _operations.DisposeAsync();
    }

    private void Publish(GameOperationResult result)
    {
        if (result.Progress is not null)
        {
            _registry.PublishProgress(result.GameId, result.Progress, result.Version);
        }
    }

    private void LogConnection(GameSessionDefinition definition, GameRuntimeState previous)
    {
        DateTime? reconnectDeadline = previous.WhiteReconnectDeadlineUtc;
        if (definition.Player.StoneColor == DatabaseValues.StoneColors.Black)
        {
            reconnectDeadline = previous.BlackReconnectDeadlineUtc;
        }
        if (reconnectDeadline.HasValue)
        {
            _logger.LogInformation("Game stream reconnected. GameId: {GameId}. "
                + "StoneColor: {StoneColor}.", definition.GameId, definition.Player.StoneColor);
        }
        else
        {
            _logger.LogInformation("Game stream connected. GameId: {GameId}. "
                + "StoneColor: {StoneColor}.", definition.GameId, definition.Player.StoneColor);
        }
    }

    private void LogDisconnection(GameSessionConnection connection, GameRuntimeState progress)
    {
        if (connection.IsGracefulClosure && progress.IsTerminal)
        {
            _logger.LogInformation("Completed game stream closed gracefully. GameId: {GameId}.",
                connection.GameId);
        }
        else
        {
            _logger.LogWarning("Game stream disconnected. GameId: {GameId}. "
                + "StoneColor: {StoneColor}. State: {State}. "
                + "BlackReconnectDeadlineUtc: {BlackDeadline}. "
                + "WhiteReconnectDeadlineUtc: {WhiteDeadline}.",
                connection.GameId, connection.StoneColor, progress.Status,
                progress.BlackReconnectDeadlineUtc, progress.WhiteReconnectDeadlineUtc);
        }
    }
}
