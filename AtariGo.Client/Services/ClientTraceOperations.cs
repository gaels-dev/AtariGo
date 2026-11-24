namespace AtariGo.Client.Services;

internal static class ClientTraceOperations
{
    private static readonly Dictionary<string, string> operations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A completed game connection attempt was replaced."] =
                "ACompletedGameConnectionAttemptWasReplaced",
            ["Async command cancelled by its owner."] =
                "AsyncCommandCancelledByItsOwner",
            ["Async command failure handler failed. Exception types: {0}, {1}."] =
                "AsyncCommandFailureHandlerFailedExceptionTypes",
            ["Avatar {0} could not be loaded: {1}."] =
                "AvatarCouldNotBeLoaded",
            ["Client request failed. Exception type: {0}."] =
                "ClientRequestFailedExceptionType",
            ["Friend request response cancelled by its owner."] =
                "FriendRequestResponseCancelledByItsOwner",
            ["Game chat send failed. Exception type: {0}."] =
                "GameChatSendFailedExceptionType",
            ["Game history presentation failed with {0}."] =
                "GameHistoryPresentationFailedWith",
            ["Game history query failed: {0}."] =
                "GameHistoryQueryFailed",
            ["Game stream failed. GameId: {0}. Error: {1}. Attempt: {2}."] =
                "GameStreamFailedGameIdErrorAttempt",
            ["Game stream restored. GameId: {0}."] =
                "GameStreamRestoredGameId",
            ["Gameplay command failed: {0}."] =
                "GameplayCommandFailed",
            ["Gameplay connection attempt stopped."] =
                "GameplayConnectionAttemptStopped",
            ["Gameplay session failed: {0}."] =
                "GameplaySessionFailed",
            ["Gameplay session stopped with its owner."] =
                "GameplaySessionStoppedWithItsOwner",
            ["Guest identity revocation failed with status {0}."] =
                "GuestIdentityRevocationFailedWithStatus",
            ["Guest sign-out failed with exception type {0}."] =
                "GuestSignOutFailedWithExceptionType",
            ["Invitation mutation unconfirmed: {0}."] =
                "InvitationMutationUnconfirmed",
            ["Invitation refresh stopped with its owner."] =
                "InvitationRefreshStoppedWithItsOwner",
            ["Late guest identity revocation failed: {0}."] =
                "LateGuestIdentityRevocationFailed",
            ["Leaderboard refresh cancelled because its view was left."] =
                "LeaderboardRefreshCancelledBecauseItsViewWasLeft",
            ["Lobby profile load failed with status {0}."] =
                "LobbyProfileLoadFailedWithStatus",
            ["Lobby profile load was cancelled."] =
                "LobbyProfileLoadWasCancelled",
            ["Login cancelled because its window was closed."] =
                "LoginCancelledBecauseItsWindowWasClosed",
            ["Login was not confirmed. Error type: {0}."] =
                "LoginWasNotConfirmedErrorType",
            ["Logout wait cancelled because its window was closed."] =
                "LogoutWaitCancelledBecauseItsWindowWasClosed",
            ["Password change was not confirmed. Error type: {0}."] =
                "PasswordChangeWasNotConfirmedErrorType",
            ["Password recovery was not confirmed. Error type: {0}."] =
                "PasswordRecoveryWasNotConfirmedErrorType",
            ["Pending login cancellation was not confirmed. Error type: {0}."] =
                "PendingLoginCancellationWasNotConfirmedErrorType",
            ["Profile presentation failed with {0}."] =
                "ProfilePresentationFailedWith",
            ["Profile request failed with status {0}."] =
                "ProfileRequestFailedWithStatus",
            ["Public matchmaking search was cancelled."] =
                "PublicMatchmakingSearchWasCancelled",
            ["Public matchmaking stream was cancelled."] =
                "PublicMatchmakingStreamWasCancelled",
            ["Real-time connection failed. Reason: {0}."] =
                "RealTimeConnectionFailedReason",
            ["Real-time session stopped by its owner."] =
                "RealTimeSessionStoppedByItsOwner",
            ["Registration request failed. Reason: {0}."] =
                "RegistrationRequestFailedReason",
            ["Registration wait cancelled because its dialog was closed."] =
                "RegistrationWaitCancelledBecauseItsDialogWasClosed",
            ["Report presentation failed with {0}."] =
                "ReportPresentationFailedWith",
            ["Report submission failed: {0}."] =
                "ReportSubmissionFailed",
            ["Retrying game stream. GameId: {0}. Attempt: {1}."] =
                "RetryingGameStreamGameIdAttempt",
            ["Room command failed: {0}."] =
                "RoomCommandFailed",
            ["Room observation failed: {0}."] =
                "RoomObservationFailed",
            ["Room observation stopped with its owner."] =
                "RoomObservationStoppedWithItsOwner",
            ["Room state recovery failed: {0}."] =
                "RoomStateRecoveryFailed",
            ["Session logout failed. RPC status: {0}."] =
                "SessionLogoutFailedRPCStatus",
        };

    internal static string Find(string template)
    {
        return operations.TryGetValue(template, out string? operation)
            ? operation : "UnclassifiedTrace";
    }
}
