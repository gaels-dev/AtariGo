namespace AtariGo.Server.Application.Authentication;

public sealed record LoginOperationResult(
    LoginStatus Status,
    int UserId = 0,
    string UserName = "",
    string SessionToken = "");
