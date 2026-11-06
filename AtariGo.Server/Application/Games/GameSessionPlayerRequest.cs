using System;

namespace AtariGo.Server.Application.Games;

public sealed record GameSessionPlayerRequest(
    long GameId,
    int? UserId,
    Guid? GuestIdentityId);
