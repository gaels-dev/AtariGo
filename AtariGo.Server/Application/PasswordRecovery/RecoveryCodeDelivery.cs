using MailKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

using AtariGo.Server.Application.Email;
using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoveryCodeDelivery
{
    private readonly AtariGoDbContext _dbContext;
    private readonly RecoveryCodeProtector _protector;
    private readonly IEmailSender _email;
    private readonly PasswordRecoveryOptions _options;
    private readonly TimeProvider _timeProvider;

    public RecoveryCodeDelivery(AtariGoDbContext dbContext, RecoveryCodeProtector protector,
        IEmailSender email, IOptions<PasswordRecoveryOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _protector = protector;
        _email = email;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<RecoveryResult> DeliverAsync(RecoveryRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!_protector.IsConfigured || !_email.IsConfigured)
        {
            return RecoveryResult.Unavailable;
        }
        RecoveryDelivery? delivery = await IssueAsync(command, cancellationToken);
        if (delivery is null)
        {
            return RecoveryResult.Accepted;
        }
        try
        {
            await _email.SendAsync(delivery.Message, cancellationToken);
        }
        catch (Exception exception) when (IsDeliveryFailure(exception))
        {
            await InvalidateDeliveryAsync(delivery.CodeId);
            return RecoveryResult.DeliveryFailed;
        }
        finally
        {
            delivery.Message.Body = string.Empty;
        }
        return RecoveryResult.Accepted;
    }

    private async Task<RecoveryDelivery?> IssueAsync(RecoveryRequestCommand command,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var accounts = new AccountCredentialStore(_dbContext);
        User? user = await accounts.LockAccountAsync(command.Identifier, cancellationToken);
        DateTime now = GetCurrentTime();
        if (user?.Credential is null || !await CanIssueAsync(user.UserId, now, cancellationToken))
        {
            return null;
        }
        await _dbContext.AuthenticationCodes
            .Where(code => code.UserId == user.UserId
                && code.CodeType == RecoveryCodeProtector.Purpose
                && code.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.UsedAt, now),
                cancellationToken);
        string value = RecoveryCodeProtector.CreateCode();
        var code = new AuthenticationCode
        {
            UserId = user.UserId,
            CodeType = RecoveryCodeProtector.Purpose,
            CodeHash = _protector.Protect(user.UserId, value),
            CreatedAt = now,
            ExpiresAt = now.Add(RecoveryCodeProtector.Lifetime)
        };
        _dbContext.AuthenticationCodes.Add(code);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RecoveryDelivery
        {
            CodeId = code.AuthenticationCodeId,
            Message = RecoveryEmailContent.CreateMessage(user.Email, value, command.CultureName)
        };
    }

    private async Task<bool> CanIssueAsync(int userId, DateTime now,
        CancellationToken cancellationToken)
    {
        DateTime windowStart = now.Subtract(RecoveryRequestLimiter.RequestWindow);
        IQueryable<AuthenticationCode> recent = _dbContext.AuthenticationCodes
            .Where(code => code.UserId == userId && code.CodeType == RecoveryCodeProtector.Purpose
                && code.CreatedAt > windowStart);
        int count = await recent.CountAsync(cancellationToken);
        if (count >= _options.AccountRequestsPerHour)
        {
            return false;
        }
        DateTime? lastSent = await recent.MaxAsync(code => (DateTime?)code.CreatedAt,
            cancellationToken);
        return lastSent is null || lastSent <= now.AddSeconds(-_options.ResendSeconds);
    }

    private Task<int> InvalidateDeliveryAsync(long codeId)
    {
        DateTime now = GetCurrentTime();
        return _dbContext.AuthenticationCodes
            .Where(code => code.AuthenticationCodeId == codeId && code.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.UsedAt, now));
    }

    private DateTime GetCurrentTime()
    {
        return DateTime.UnixEpoch.AddSeconds(_timeProvider.GetUtcNow().ToUnixTimeSeconds());
    }

    private static bool IsDeliveryFailure(Exception exception)
    {
        return exception is IOException or OperationCanceledException or CommandException
            or ProtocolException or MailKit.Security.AuthenticationException
            or System.Net.Sockets.SocketException
            or System.Security.Authentication.AuthenticationException;
    }
}
