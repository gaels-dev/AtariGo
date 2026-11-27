using Grpc.Core;

using AtariGo.Contracts;

namespace AtariGo.Server.Services;

internal static class AccountSanctionGrpcErrors
{
    public static RpcException CreateAccountSuspended()
    {
        return Create(GrpcFailureCodes.AccountSuspended,
            "The account is temporarily suspended.");
    }

    public static RpcException CreateParticipantSuspended()
    {
        return Create(GrpcFailureCodes.ParticipantSuspended,
            "A player is temporarily suspended.");
    }

    private static RpcException Create(string errorCode, string detail)
    {
        Metadata trailers = new Metadata { { GrpcFailureCodes.TrailerKey, errorCode } };
        return new RpcException(new Status(StatusCode.PermissionDenied, detail), trailers);
    }
}
