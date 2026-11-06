using Grpc.Core;

namespace AtariGo.Server.Services;

internal static class SessionRequestAuthentication
{
    private const string AuthorizationHeader = "authorization";
    private const string BearerPrefix = "Bearer ";

    public static string GetSessionToken(ServerCallContext context)
    {
        string? authorization = context.RequestHeaders.GetValue(
            AuthorizationHeader);
        if (authorization is null
            || !authorization.StartsWith(
                BearerPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw CreateUnauthenticatedException();
        }

        string token = authorization[BearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw CreateUnauthenticatedException();
        }

        return token;
    }

    public static RpcException CreateUnauthenticatedException()
    {
        return new RpcException(new Status(
            StatusCode.Unauthenticated,
            "La sesión no es válida o ha vencido."));
    }
}
