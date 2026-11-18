namespace AtariGo.Server.Application.Authentication;

public static class LoginServiceRegistration
{
    public static IServiceCollection AddLoginVerification(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<LoginVerificationOptions>().BindConfiguration("LoginVerification")
            .Validate(options => options.AccountRequestsPerHour > 0 && options.IpRequestsPerHour > 0
                && options.ResendSeconds >= LoginVerificationOptions.MinimumResendSeconds);
        services.AddSingleton<LoginCodeProtector>();
        services.AddSingleton<LoginRequestLimiter>();
        services.AddScoped<LoginAttemptService>();
        services.AddScoped<LoginCodeDeliveryService>();
        services.AddScoped<LoginChallengeService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        return services;
    }
}
