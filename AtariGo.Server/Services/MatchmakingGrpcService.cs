using System.Diagnostics;

using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Rooms;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class MatchmakingGrpcService : Matchmaking.MatchmakingBase
{
    private const string GuestCredentialHeader = "x-guest-credential";
    private const string GuestNamePrefix = "Guest-";
    private const int MaximumGuestNameLength = 15;

    private readonly IPublicMatchmakingQueue _matchmakingQueue;
    private readonly IGuestIdentityRegistry _guestIdentityRegistry;
    private readonly ISessionService _sessionService;
    private readonly IGameLifecycleService _gameLifecycleService;
    private readonly TimeProvider _timeProvider;

    public MatchmakingGrpcService(
        IPublicMatchmakingQueue matchmakingQueue,
        IGuestIdentityRegistry guestIdentityRegistry,
        ISessionService sessionService,
        IGameLifecycleService gameLifecycleService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(matchmakingQueue);
        ArgumentNullException.ThrowIfNull(guestIdentityRegistry);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(gameLifecycleService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _matchmakingQueue = matchmakingQueue;
        _guestIdentityRegistry = guestIdentityRegistry;
        _sessionService = sessionService;
        _gameLifecycleService = gameLifecycleService;
        _timeProvider = timeProvider;
    }

    public override Task<CreateGuestIdentityResponse> CreateGuestIdentity(
        CreateGuestIdentityRequest request,
        ServerCallContext context)
    {
        string displayName = request.DisplayName.Trim();
        if (!IsValidGuestName(displayName))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "The guest display name is invalid."));
        }

        IssuedGuestIdentity issuedIdentity = _guestIdentityRegistry.Create(displayName);
        var response = new CreateGuestIdentityResponse
        {
            Credential = issuedIdentity.Credential
        };
        return Task.FromResult(response);
    }

    public override Task<RevokeGuestIdentityResponse> RevokeGuestIdentity(
        RevokeGuestIdentityRequest request,
        ServerCallContext context)
    {
        string credential = GetGuestCredential(context);
        _guestIdentityRegistry.Revoke(credential);
        return Task.FromResult(new RevokeGuestIdentityResponse());
    }

    public override async Task SearchPublicMatch(
        SearchPublicMatchRequest request,
        IServerStreamWriter<MatchmakingEvent> responseStream,
        ServerCallContext context)
    {
        MatchmakingPlayer player = await IdentifyPlayerAsync(context);
        MatchmakingTicket? ticket = _matchmakingQueue.Join(player);
        if (ticket is null)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                "The player is already searching for a match."));
        }

        try
        {
            await SendSearchEventsAsync(ticket, responseStream, context.CancellationToken);
        }
        catch (OperationCanceledException)
            when (context.CancellationToken.IsCancellationRequested)
        {
            Trace.TraceInformation("Public matchmaking stream was cancelled by the client.");
        }
        catch (RpcException exception)
            when (context.CancellationToken.IsCancellationRequested
                && exception.StatusCode == StatusCode.Cancelled)
        {
            Trace.TraceInformation("Public matchmaking RPC was cancelled by the client.");
        }
        finally
        {
            _matchmakingQueue.Disconnect(ticket);
        }
    }

    private async Task<MatchmakingPlayer> IdentifyPlayerAsync(
        ServerCallContext context)
    {
        string? guestCredential = context.RequestHeaders.GetValue(
            GuestCredentialHeader);
        if (!string.IsNullOrWhiteSpace(guestCredential))
        {
            GuestIdentity? guestIdentity = _guestIdentityRegistry.Find(guestCredential);
            if (guestIdentity is null)
            {
                throw SessionRequestAuthentication.CreateUnauthenticatedException();
            }

            return new MatchmakingPlayer(
                $"guest:{guestIdentity.GuestId:N}",
                null,
                guestIdentity.DisplayName,
                guestIdentity.DisplayName);
        }

        string sessionToken = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken, context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return new MatchmakingPlayer(
            $"user:{identity.UserId}",
            identity.UserId,
            identity.UserName,
            null);
    }

    private async Task SendSearchEventsAsync(
        MatchmakingTicket ticket,
        IServerStreamWriter<MatchmakingEvent> responseStream,
        CancellationToken cancellationToken)
    {
        await foreach (MatchmakingEvent matchmakingEvent in
            ticket.Notifications.Reader.ReadAllAsync(cancellationToken))
        {
            await responseStream.WriteAsync(matchmakingEvent, cancellationToken);
            if (matchmakingEvent.EventCase == MatchmakingEvent.EventOneofCase.MatchCountdownStarted)
            {
                PublicMatchState? match = ticket.Match;
                if (match is not null)
                {
                    await CompleteCountdownAsync(match, cancellationToken);
                }
            }

            if (matchmakingEvent.EventCase == MatchmakingEvent.EventOneofCase.PublicGameStarted)
            {
                return;
            }
        }
    }

    private async Task CompleteCountdownAsync(
        PublicMatchState match,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = match.CountdownEndsAt - _timeProvider.GetUtcNow();
        if (remaining > TimeSpan.Zero)
        {
            using CancellationTokenSource delayCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    match.CountdownCancellation.Token,
                    cancellationToken);
            try
            {
                await Task.Delay(remaining, _timeProvider,
                    delayCancellation.Token);
            }
            catch (OperationCanceledException)
                when (match.CountdownCancellation.IsCancellationRequested)
            {
                return;
            }
        }

        if (!_matchmakingQueue.TryClaimStart(match))
        {
            await match.GameCompletion.Task.WaitAsync(cancellationToken);
            return;
        }

        await PersistAndPublishGameAsync(match, cancellationToken);
    }

    private async Task PersistAndPublishGameAsync(
        PublicMatchState match,
        CancellationToken cancellationToken)
    {
        bool isComplete = false;
        try
        {
            var request = new PublicMatchStartRequest(
                match.BlackTicket.Player,
                match.WhiteTicket.Player,
                CreateGameConfiguration());
            long gameId = await _gameLifecycleService.StartPublicGameAsync(
                request, CancellationToken.None);
            _matchmakingQueue.CompleteStart(match, gameId);
            isComplete = true;
        }
        finally
        {
            if (!isComplete)
            {
                _matchmakingQueue.FailStart(match);
            }
        }
    }

    private static LobbyConfiguration CreateGameConfiguration()
    {
        return new LobbyConfiguration(
            PublicGameRules.CaptureGoal,
            PublicGameRules.GameTimeSeconds,
            PublicGameRules.TurnTimeSeconds);
    }

    private static bool IsValidGuestName(string displayName)
    {
        if (!displayName.StartsWith(GuestNamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string guestName = displayName[GuestNamePrefix.Length..];
        return guestName.Length > 0 && guestName.Length <= MaximumGuestNameLength;
    }

    private static string GetGuestCredential(ServerCallContext context)
    {
        string? credential = context.RequestHeaders.GetValue(GuestCredentialHeader);
        if (string.IsNullOrWhiteSpace(credential))
        {
            throw new RpcException(new Status(
                StatusCode.Unauthenticated,
                "The guest identity is not valid."));
        }

        return credential;
    }
}
