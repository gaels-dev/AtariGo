using Grpc.Core;

using AtariGo.Client.Models;
using AtariGo.Contracts;
using AtariGo.Diagnostics;

namespace AtariGo.Client.Services;

public sealed class GrpcGameHistoryClient : IGameHistoryClient
{
    private const int MaximumHistoryEntries = 20;

    private readonly string _sessionToken;
    private readonly GameHistory.GameHistoryClient _client;

    public GrpcGameHistoryClient(string sessionToken)
        : this(sessionToken, GrpcClientConnection.Shared.CallInvoker)
    {
    }

    public GrpcGameHistoryClient(string sessionToken, CallInvoker callInvoker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentNullException.ThrowIfNull(callInvoker);
        _sessionToken = sessionToken;
        _client = new GameHistory.GameHistoryClient(callInvoker);
    }

    public async Task<GameHistoryQueryResult> ListGamesByPlayerAsync(
        CancellationToken cancellationToken)
    {
        GameHistoryQueryResult result;
        try
        {
            Metadata headers = GrpcCallPolicy.CreateHeaders(_sessionToken);
            ListGamesByPlayerResponse response = await _client.ListGamesByPlayerAsync(
                new ListGamesByPlayerRequest(),
                GrpcCallPolicy.CreateOptions(cancellationToken, headers)).ResponseAsync;
            cancellationToken.ThrowIfCancellationRequested();
            result = MapResponse(response);
            ClientPlayerDiagnostics.Record(PlayerOperation.QueryHistory,
                CreateDetails(result));
        }
        catch (RpcException exception)
        {
            ClientPlayerDiagnostics.RecordFailure(PlayerOperation.QueryHistory, exception,
                cancellationToken);
            ClientFailureReason reason = cancellationToken.IsCancellationRequested
                ? ClientFailureReason.Cancelled : GrpcClientFailures.GetReason(exception);
            result = CreateFailure(reason);
        }
        catch (OperationCanceledException exception)
        {
            ClientPlayerDiagnostics.RecordFailure(PlayerOperation.QueryHistory, exception,
                cancellationToken);
            result = CreateFailure(cancellationToken.IsCancellationRequested
                ? ClientFailureReason.Cancelled : ClientFailureReason.Timeout);
        }
        return result;
    }

    private static GameHistoryQueryResult MapResponse(ListGamesByPlayerResponse response)
    {
        if (!IsValidResponse(response))
        {
            return CreateFailure(ClientFailureReason.RequestFailed);
        }
        return new GameHistoryQueryResult
        {
            Entries = response.Entries.Select(entry => new GameHistoryEntry
            {
                GameId = entry.GameId,
                PlayedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(
                    entry.PlayedAtUnixMilliseconds),
                OpponentName = entry.OpponentName,
                Result = entry.Result,
                StoneColor = entry.StoneColor,
                OpponentStoneColor = entry.OpponentStoneColor,
                State = entry.State
            }).ToArray()
        };
    }

    private static bool IsValidResponse(ListGamesByPlayerResponse response)
    {
        if (response.Entries.Count > MaximumHistoryEntries)
        {
            return false;
        }
        var gameIds = new HashSet<long>();
        GameHistorySummary? previous = null;
        bool isValid = true;
        foreach (GameHistorySummary entry in response.Entries)
        {
            if (!IsValidSummary(entry) || !gameIds.Add(entry.GameId)
                || !IsOrdered(previous, entry))
            {
                isValid = false;
                break;
            }
            previous = entry;
        }
        return isValid;
    }

    private static bool IsOrdered(GameHistorySummary? previous, GameHistorySummary current)
    {
        return previous is null
            || previous.PlayedAtUnixMilliseconds > current.PlayedAtUnixMilliseconds
            || (previous.PlayedAtUnixMilliseconds == current.PlayedAtUnixMilliseconds
                && previous.GameId > current.GameId);
    }

    private static bool IsValidSummary(GameHistorySummary entry)
    {
        bool hasValidColors = entry.StoneColor is "Black" or "White"
            && entry.OpponentStoneColor is "Black" or "White"
            && entry.StoneColor != entry.OpponentStoneColor;
        bool hasValidCodes = entry.Result is "Pending" or "Won" or "Lost"
            or "Surrendered" or "Abandoned"
            && entry.State is "Waiting" or "InProgress" or "Completed"
                or "Abandoned" or "Cancelled";
        long timestamp = entry.PlayedAtUnixMilliseconds;
        bool hasValidDate = timestamp >= DateTimeOffset.MinValue.ToUnixTimeMilliseconds()
            && timestamp <= DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();
        return entry.GameId > 0 && !string.IsNullOrWhiteSpace(entry.OpponentName)
            && hasValidColors && hasValidCodes && hasValidDate;
    }

    private static DiagnosticEventDetails CreateDetails(GameHistoryQueryResult result)
    {
        if (result.Failure != ClientFailureReason.None)
        {
            return new DiagnosticEventDetails
            {
                Outcome = DiagnosticOutcome.Unconfirmed,
                Severity = DiagnosticSeverity.Warning,
                Status = new DiagnosticStatus(result.Failure)
            };
        }
        return new DiagnosticEventDetails { Outcome = DiagnosticOutcome.Succeeded };
    }

    private static GameHistoryQueryResult CreateFailure(ClientFailureReason failure)
    {
        return new GameHistoryQueryResult { Failure = failure };
    }
}
