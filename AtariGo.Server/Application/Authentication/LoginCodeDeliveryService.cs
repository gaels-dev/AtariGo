using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using MailKit;

using AtariGo.Server.Application.Email;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginCodeDeliveryService
{
    private const int BindingByteLength = 16;
    private static readonly TimeSpan requestWindow = TimeSpan.FromHours(1);
    private readonly AtariGoDbContext _dbContext;
    private readonly IEmailSender _sender;
    private readonly LoginCodeProtector _protector;
    private readonly LoginVerificationOptions _options;
    private readonly TimeProvider _timeProvider;

    public LoginCodeDeliveryService(AtariGoDbContext dbContext, IEmailSender sender,
        LoginCodeProtector protector, IOptions<LoginVerificationOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _sender = sender;
        _protector = protector;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public bool IsConfigured => _sender.IsConfigured && _protector.IsConfigured;

    public TimeSpan ResendDelay => TimeSpan.FromSeconds(_options.ResendSeconds);

    public async Task<DateTime?> FindRetryAtAsync(LoginChallenge challenge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        DateTime now = GetNow();
        DateTime windowStart = now.Subtract(requestWindow);
        List<DateTime> issued = await _dbContext.AuthenticationCodes.AsNoTracking()
            .Where(code => code.UserId == challenge.UserId
                && code.CodeType == LoginVerificationOptions.Purpose
                && code.CreatedAt > windowStart)
            .OrderBy(code => code.CreatedAt).Select(code => code.CreatedAt)
            .ToListAsync(cancellationToken);
        if (issued.Count >= _options.AccountRequestsPerHour)
        {
            return issued[0].Add(requestWindow);
        }
        DateTime? last = issued.Count > 0 ? issued[^1] : challenge.LastSentAt;
        DateTime? next = last?.AddSeconds(_options.ResendSeconds);
        return next > now ? next : null;
    }

    public LoginCodeDelivery Prepare(LoginChallengeContext context, string cultureName)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cultureName);
        LoginChallenge challenge = context.Challenge;
        DateTime now = GetNow();
        if (challenge.ActiveCode is not null)
        {
            challenge.ActiveCode.UsedAt = now;
        }
        challenge.CodeBinding = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(BindingByteLength));
        string value = AuthenticationCodeProtector.CreateCode();
        var code = new AuthenticationCode
        {
            UserId = challenge.UserId, CodeType = LoginVerificationOptions.Purpose,
            CodeHash = _protector.Protect(challenge, value), CreatedAt = now,
            ExpiresAt = now.Add(LoginVerificationOptions.CodeLifetime)
        };
        _dbContext.AuthenticationCodes.Add(code);
        challenge.ActiveCode = code;
        challenge.LastSentAt = now;
        challenge.DeliveryState = LoginVerificationOptions.Pending;
        EmailMessage message = LoginEmailContent.CreateMessage(context.User.Email, value,
            cultureName);
        return new LoginCodeDelivery(challenge, message);
    }

    public bool Matches(LoginChallenge challenge, string code)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return challenge.ActiveCode is not null
            && _protector.Matches(challenge, code, challenge.ActiveCode.CodeHash);
    }

    public async Task<bool> SendAsync(LoginCodeDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        try
        {
            await _sender.SendAsync(delivery.Message, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException
            or CommandException or ProtocolException or MailKit.Security.AuthenticationException
            or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException)
        {
            return false;
        }
        finally
        {
            delivery.Message.Body = string.Empty;
        }
        return true;
    }

    private DateTime GetNow()
    {
        return DateTime.UnixEpoch.AddSeconds(_timeProvider.GetUtcNow().ToUnixTimeSeconds());
    }
}
