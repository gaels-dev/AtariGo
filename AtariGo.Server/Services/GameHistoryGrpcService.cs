using System.Data.Common;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Diagnostics;
using AtariGo.Server.Diagnostics;
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
        ServerPlayerDiagnostics.Record(_logger, PlayerOperation.QueryHistory,
            new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Succeeded,
                Context = new DiagnosticContext { UserId = identity.UserId }
            });
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

    private static RpcException CreateFailure(StatusCode status)
    {
        return new RpcException(new Status(status, "The game history is unavailable."));
    }

    public override async Task<ListGamesByPlayerResponse> ListGamesByPlayer(
        ListGamesByPlayerRequest request, ServerCallContext context)
    {
        try
        {
            return await ReadHistoryAsync(context);
        }
        catch (OperationCanceledException exception)
            when (context.CancellationToken.IsCancellationRequested)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.QueryHistory,
                (exception, context.CancellationToken));
            throw new RpcException(new Status(StatusCode.Cancelled, "The query was cancelled."));
        }
        catch (DbException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.QueryHistory,
                (exception, context.CancellationToken));
            throw CreateFailure(StatusCode.Unavailable);
        }
        catch (InvalidOperationException exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.QueryHistory,
                (exception, context.CancellationToken));
            throw CreateFailure(StatusCode.Internal);
        }
        catch (Exception exception)
        {
            ServerPlayerDiagnostics.RecordFailure(_logger, PlayerOperation.QueryHistory,
                (exception, context.CancellationToken));
            throw;
        }
    }
}
