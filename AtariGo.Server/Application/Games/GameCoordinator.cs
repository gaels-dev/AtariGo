namespace AtariGo.Server.Application.Games;

public sealed class GameCoordinator : IDisposable, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGameSessionRegistry _registry;
    private readonly GameOperationGate _operations;

    public GameCoordinator(IServiceScopeFactory scopeFactory,
        IGameSessionRegistry registry, GameOperationGate operations)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(operations);
        _scopeFactory = scopeFactory;
        _registry = registry;
        _operations = operations;
    }

    public async Task<GameSessionConnection> ConnectAsync(GameSessionDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        using GameOperationLease lease = await _operations.EnterAsync(
            definition.GameId, cancellationToken);
        using IServiceScope scope = _scopeFactory.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationContext operation = await commands.LoadAsync(
            definition.GameId, cancellationToken);
        GameSessionConnection connection = _registry.Connect(definition with
        {
            Progress = operation.Progress,
            Version = operation.Version
        });
        try
        {
            Publish(await commands.AdvanceAsync(operation,
                () => _registry.AreBothConnected(definition.GameId)));
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
        using GameOperationLease lease = await _operations.EnterAsync(
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
        using GameOperationLease lease = await _operations.EnterAsync(gameId, cancellationToken);
        using IServiceScope scope = _scopeFactory.CreateScope();
        GameCommandService commands = scope.ServiceProvider
            .GetRequiredService<GameCommandService>();
        GameOperationContext operation = await commands.LoadAsync(gameId, cancellationToken);
        Publish(await commands.AdvanceAsync(operation, () => _registry.AreBothConnected(gameId)));
    }

    public void Disconnect(GameSessionConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _registry.Disconnect(connection);
    }

    public ChatMessageSubmissionResult PublishChatMessage(
        GameSessionConnection connection, string text)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(text);
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
}
