using Microsoft.Extensions.Options;

using AtariGo.Server.Application.Email;
using AtariGo.Server.Application.PasswordRecovery;
using AtariGo.Server.Security;

namespace AtariGo.Server.Application.Authentication;

public static class LoginServiceRegistration
{
    public static IServiceCollection AddLoginVerification(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<LoginVerificationOptions>().BindConfiguration("LoginVerification")
            .Validate(options => options.AccountRequestsPerHour > 0 && options.IpRequestsPerHour > 0
                && options.ResendSeconds >= LoginVerificationOptions.MinimumResendSeconds
                && options.MaximumPendingLogins > 0);
        services.AddSingleton(CreateCodeProtector);
        services.AddSingleton<AuthenticationRequestLimiter>();
        services.AddSingleton<EmailDeliveryService>();
        services.AddSingleton<PendingLoginStore>();
        services.AddScoped<AuthenticationCodeIssuanceService>();
        services.AddScoped<LoginAttemptService>();
        services.AddScoped<LoginCodeDeliveryService>();
        services.AddScoped<LoginChallengeService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        return services;
    }

    private static AuthenticationCodeProtector CreateCodeProtector(IServiceProvider provider)
    {
        return new AuthenticationCodeProtector(provider
            .GetRequiredService<IOptions<PasswordRecoveryOptions>>().Value.HmacKey);
    }
}
