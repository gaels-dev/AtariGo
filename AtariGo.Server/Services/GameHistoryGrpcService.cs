using System.Data.Common;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.GameHistory;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class GameHistoryGrpcService : GameHistory.GameHistoryBase
{
    private readonly IGameHistoryService _history;
    private readonly ISessionService _sessions;
    private readonly ILogger<GameHistoryGrpcService> _logger;

    public GameHistoryGrpcService(IGameHistoryService history, ISessionService sessions,
        ILogger<GameHistoryGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(logger);
        _history = history;
        _sessions = sessions;
        _logger = logger;
    }

    private async Task<ListGamesByPlayerResponse> ReadHistoryAsync(ServerCallContext context)
    {
        string token = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessions.ValidateSessionAsync(
            token, context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }
        IReadOnlyList<GameHistoryEntry> entries = await _history.ListGamesByPlayerAsync(
            identity, context.CancellationToken);
        var response = new ListGamesByPlayerResponse();
        foreach (GameHistoryEntry entry in entries)
        {
            response.Entries.Add(MapEntry(entry));
        }
        return response;
    }

    private static GameHistorySummary MapEntry(GameHistoryEntry entry)
    {
        DateTime utc = DateTime.SpecifyKind(entry.PlayedAtUtc, DateTimeKind.Utc);
        return new GameHistorySummary
        {
            GameId = entry.GameId,
            PlayedAtUnixMilliseconds = new DateTimeOffset(utc).ToUnixTimeMilliseconds(),
            OpponentName = entry.OpponentName,
            Result = entry.Result,
            StoneColor = entry.StoneColor,
            OpponentStoneColor = entry.OpponentStoneColor,
            State = entry.State
        };
    }

    private RpcException CreateFailure(Exception exception, StatusCode status)
    {
        _logger.LogError("Game history query failed with {ErrorType}: {Status}.",
            exception.GetType().Name, status);
        return new RpcException(new Status(status, "The game history is unavailable."));
    }

    public override async Task<ListGamesByPlayerResponse> ListGamesByPlayer(
        ListGamesByPlayerRequest request, ServerCallContext context)
    {
        try
        {
            return await ReadHistoryAsync(context);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "The query was cancelled."));
        }
        catch (DbException exception)
        {
            throw CreateFailure(exception, StatusCode.Unavailable);
        }
        catch (InvalidOperationException exception)
        {
            throw CreateFailure(exception, StatusCode.Internal);
        }
    }
}
