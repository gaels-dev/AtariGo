using AtariGo.Server.Application.Email;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginCodeDeliveryService
{
    private readonly AtariGoDbContext _dbContext;
    private readonly EmailDeliveryService _email;
    private readonly AuthenticationCodeProtector _protector;
    private readonly AuthenticationCodeIssuanceService _issuance;
    private readonly TimeProvider _timeProvider;

    public LoginCodeDeliveryService(AtariGoDbContext dbContext, EmailDeliveryService email,
        AuthenticationCodeProtector protector, AuthenticationCodeIssuanceService issuance,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(issuance);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _email = email;
        _protector = protector;
        _issuance = issuance;
        _timeProvider = timeProvider;
    }

    public bool IsConfigured => _email.IsConfigured && _protector.IsConfigured;

    public TimeSpan ResendDelay => _issuance.LoginResendDelay;

    public Task<DateTime?> FindRetryAtAsync(int userId,
        CancellationToken cancellationToken)
    {
        return _issuance.FindLoginRetryAtAsync(userId, cancellationToken);
    }

    public LoginCodeDelivery Prepare(User user, PendingLogin pending, string cultureName)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(cultureName);
        DateTime now = GetNow();
        string value = AuthenticationCodeProtector.CreateCode();
        var code = new AuthenticationCode
        {
            UserId = pending.UserId,
            CodeType = LoginVerificationOptions.Purpose,
            CodeHash = _protector.Protect(CreateScope(pending), value),
            CreatedAt = now,
            ExpiresAt = now.Add(LoginVerificationOptions.CodeLifetime)
        };
        _dbContext.AuthenticationCodes.Add(code);
        EmailMessage message = AuthenticationEmailContent.Login.CreateMessage(user.Email, value,
            cultureName);
        return new LoginCodeDelivery(pending with
        {
            DeliveryStatus = LoginDeliveryStatus.Pending
        }, code, message);
    }

    public bool Matches(PendingLoginContext context, string code)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _protector.Matches(CreateScope(context.Pending), code, context.Code.CodeHash);
    }

    public Task<bool> SendAsync(LoginCodeDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        return _email.TrySendAsync(delivery.Message, cancellationToken);
    }

    private static AuthenticationCodeScope CreateScope(PendingLogin pending)
    {
        return new AuthenticationCodeScope(pending.UserId, LoginVerificationOptions.Purpose,
            pending.TokenHash);
    }

    private DateTime GetNow()
    {
        return DateTime.UnixEpoch.AddSeconds(_timeProvider.GetUtcNow().ToUnixTimeSeconds());
    }
}
