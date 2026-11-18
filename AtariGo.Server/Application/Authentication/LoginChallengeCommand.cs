namespace AtariGo.Server.Application.Authentication;

public sealed record LoginChallengeCommand(string ChallengeToken, string CultureName);
