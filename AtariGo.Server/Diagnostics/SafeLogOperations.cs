namespace AtariGo.Server.Diagnostics;

internal static class SafeLogOperations
{
    private static readonly Dictionary<string, string> operations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A concurrent game transition will be reloaded."] =
                "AConcurrentGameTransitionWillBeReloaded",
            ["A game clock transition could not be persisted."] =
                "AGameClockTransitionCouldNotBePersisted",
            ["Could not register account: {ErrorType}."] = "CouldNotRegisterAccount",
            ["Database access failed while connecting to game {GameId}."] =
                "DatabaseAccessFailedWhileConnectingToGame",
            ["Email delivery failed with {ErrorType}."] = "EmailDeliveryFailedWith",
            ["Friend operation {Operation} failed with {ErrorType}: {Status}."] =
                "FriendOperationFailedWith",
            ["Friend request {RequestId} cancellation: {Status}."] = "FriendRequestCancellation",
            ["Friend request {RequestId} response: {Status}."] = "FriendRequestResponse",
            ["Friend request send result: {Status}."] = "FriendRequestSendResult",
            ["Friend response rejected a nonpositive identifier."] =
                "FriendResponseRejectedANonpositiveIdentifier",
            ["Friend search rejected invalid search text."] =
                "FriendSearchRejectedInvalidSearchText",
            ["Friendship removal failed with {ErrorType}: {Status}."] =
                "FriendshipRemovalFailedWith",
            ["Friendship removal for users {UserId} and {FriendUserId}: {Removed}."] =
                "FriendshipRemovalForUsersAnd",
            ["Friendship removal rejected an invalid friend identifier."] =
                "FriendshipRemovalRejectedAnInvalidFriendIdentifier",
            ["Game clock supervision stopped."] = "GameClockSupervisionStopped",
            ["Game clocks cannot access the database."] = "GameClocksCannotAccessTheDatabase",
            ["Game disconnect could not access SQL. GameId: {GameId}."] =
                "GameDisconnectCouldNotAccessSQLGameId",
            ["Game disconnect could not be persisted. GameId: {GameId}."] =
                "GameDisconnectCouldNotBePersistedGameId",
            ["Game ended after a deadline. GameId: {GameId}. Reason: {Reason}."] =
                "GameEndedAfterADeadlineGameIdReason",
            ["Game history query failed with {ErrorType}: {Status}."] =
                "GameHistoryQueryFailedWith",
            ["Game persistence failed. GameId: {GameId}"] = "GamePersistenceFailedGameId",
            ["Game reconnection grace expired. GameId: {GameId}."] =
                "GameReconnectionGraceExpiredGameId",
            ["Game session RPC cancelled by client. GameId: {GameId}"] =
                "GameSessionRPCCancelledByClientGameId",
            ["Game session stream closed by client. GameId: {GameId}"] =
                "GameSessionStreamClosedByClientGameId",
            ["Game session transport closed by client. GameId: {GameId}"] =
                "GameSessionTransportClosedByClientGameId",
            ["Game stream connected. GameId: {GameId}."] = "GameStreamConnectedGameId",
            ["Game stream disconnected. GameId: {GameId}."] = "GameStreamDisconnectedGameId",
            ["Game transport failed with {ErrorType}. GameId: {GameId}"] =
                "GameTransportFailedWithGameId",
            ["Invitation send in room {RoomId}: {Status}."] = "InvitationSendInRoom",
            ["Login failed with {ErrorType}."] = "LoginFailedWith",
            ["Login operation {Operation} completed with {Status}."] =
                "LoginOperationCompletedWith",
            ["Password change failed with {ErrorType}."] = "PasswordChangeFailedWith",
            ["Password recovery failed with {ErrorType}."] = "PasswordRecoveryFailedWith",
            ["Profile request failed."] = "ProfileRequestFailed",
            ["Public matchmaking RPC was cancelled by the client."] =
                "PublicMatchmakingRPCWasCancelledByTheClient",
            ["Public matchmaking stream was cancelled by the client."] =
                "PublicMatchmakingStreamWasCancelledByTheClient",
            ["Real-time session RPC ended by cancellation."] =
                "RealTimeSessionRPCEndedByCancellation",
            ["Real-time session stream stopped by cancellation."] =
                "RealTimeSessionStreamStoppedByCancellation",
            ["Real-time session transport ended when the client closed the stream."] =
                "RealTimeSessionTransportEndedWhenTheClientClosedTheStream",
            ["Report submission failed with {Status}."] = "ReportSubmissionFailedWith",
            ["Room configuration request for room {RoomId} by user {UserId}: {Status}."] =
                "RoomConfigurationRequestForRoomByUser",
            ["Room invitation {InvitationId} for room {RoomId}: {Status}."] =
                "RoomInvitationForRoom",
            ["Room invitation {InvitationId} in room {RoomId}: {State}."] = "RoomInvitationInRoom",
            ["Room state expiry worker stopped with the host."] =
                "RoomStateExpiryWorkerStoppedWithTheHost",
            ["Server configuration is invalid: {ConfigurationError}"] =
                "ServerConfigurationIsInvalid",
            ["Server host terminated."] = "ServerHostTerminated",
            ["The paired game RPC stopped after cancellation."] =
                "ThePairedGameRPCStoppedAfterCancellation",
            ["The paired game stream also failed with {ErrorType}."] =
                "ThePairedGameStreamAlsoFailedWith",
            ["The paired game stream task stopped after cancellation."] =
                "ThePairedGameStreamTaskStoppedAfterCancellation",
            ["The password reset write was rolled back."] = "ThePasswordResetWriteWasRolledBack",
        };

    internal static string? Read<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> values)
        {
            return null;
        }
        object? value = values.FirstOrDefault(pair => pair.Key == "{OriginalFormat}").Value;
        return value is string template && operations.TryGetValue(template, out string? operation)
            ? operation : null;
    }
}
