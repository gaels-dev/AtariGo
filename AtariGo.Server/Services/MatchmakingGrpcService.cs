using Grpc.Core;

using AtariGo.Contracts;
using AtariGo.Server.Application.Games;
using AtariGo.Server.Application.Matchmaking;
using AtariGo.Server.Application.Sessions;

namespace AtariGo.Server.Services;

public sealed class MatchmakingGrpcService : Matchmaking.MatchmakingBase
{
    private const string GuestCredentialHeader = "x-guest-credential";
    private const string GuestNamePrefix = "Guest-";
    private const int MaximumGuestNameLength = 15;

    private readonly PublicMatchmakingService _matchmaking;
    private readonly IGuestIdentityRegistry _guestIdentityRegistry;
    private readonly ISessionService _sessionService;
    private readonly ILogger<MatchmakingGrpcService> _logger;

    public MatchmakingGrpcService(
        PublicMatchmakingService matchmaking,
        IGuestIdentityRegistry guestIdentityRegistry,
        ISessionService sessionService,
        ILogger<MatchmakingGrpcService> logger)
    {
        ArgumentNullException.ThrowIfNull(matchmaking);
        ArgumentNullException.ThrowIfNull(guestIdentityRegistry);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(logger);
        _matchmaking = matchmaking;
        _guestIdentityRegistry = guestIdentityRegistry;
        _sessionService = sessionService;
        _logger = logger;
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

            return new MatchmakingPlayer
            {
                PlayerKey = GameSessionRules.CreateGuestKey(guestIdentity.GuestId),
                DisplayName = guestIdentity.DisplayName,
                GuestName = guestIdentity.DisplayName,
                GuestIdentityId = guestIdentity.GuestId
            };
        }

        string sessionToken = SessionRequestAuthentication.GetSessionToken(context);
        SessionIdentity? identity = await _sessionService.ValidateSessionAsync(
            sessionToken, context.CancellationToken);
        if (identity is null)
        {
            throw SessionRequestAuthentication.CreateUnauthenticatedException();
        }

        return new MatchmakingPlayer
        {
            PlayerKey = GameSessionRules.CreateUserKey(identity.UserId),
            UserId = identity.UserId,
            DisplayName = identity.UserName
        };
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
        MatchmakingTicket? ticket = _matchmaking.Join(player);
        if (ticket is null)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                "The player is already searching for a match."));
        }

        try
        {
            await foreach (MatchmakingEvent matchmakingEvent in
                _matchmaking.ReadEventsAsync(ticket, context.CancellationToken))
            {
                await responseStream.WriteAsync(matchmakingEvent, context.CancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (context.CancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Public matchmaking stream was cancelled by the client.");
        }
        catch (RpcException exception)
            when (context.CancellationToken.IsCancellationRequested
                && exception.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Public matchmaking RPC was cancelled by the client.");
        }
    }
}
