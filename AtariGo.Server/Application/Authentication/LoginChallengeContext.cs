using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.Authentication;

public sealed record LoginChallengeContext(User User, LoginChallenge Challenge);
