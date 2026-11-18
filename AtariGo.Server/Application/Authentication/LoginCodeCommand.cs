namespace AtariGo.Server.Application.Authentication;

public sealed record LoginCodeCommand(string ChallengeToken, string Code);
