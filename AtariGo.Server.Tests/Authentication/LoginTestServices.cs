using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;

namespace AtariGo.Server.Tests.Authentication;

internal static class LoginTestServices
{
    public static AuthenticationService Create(AtariGoDbContext context,
        RecordingLoginEmailSender? email = null, TimeProvider? clock = null)
    {
        TimeProvider time = clock ?? TimeProvider.System;
        var attempts = new LoginAttemptService(context, time);
        var protector = new LoginCodeProtector(Options.Create(new PasswordRecoveryOptions
        {
            HmacKey = new string('K', 64)
        }));
        var delivery = new LoginCodeDeliveryService(context,
            email ?? new RecordingLoginEmailSender(),
            protector, Options.Create(new LoginVerificationOptions()), time);
        var challenges = new LoginChallengeService(context, delivery, attempts,
            new SessionTokenProvider());
        return new AuthenticationService(context, new PasswordHasher<User>(), attempts, challenges);
    }
}
