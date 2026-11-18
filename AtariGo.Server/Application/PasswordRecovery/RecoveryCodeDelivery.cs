using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using AtariGo.Server.Application.Authentication;
using AtariGo.Server.Application.Email;
using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;

namespace AtariGo.Server.Application.PasswordRecovery;

public sealed class RecoveryCodeDelivery
{
    private readonly AtariGoDbContext _dbContext;
    private readonly RecoveryCodeProtector _protector;
    private readonly EmailDeliveryService _email;
    private readonly AuthenticationCodeIssuanceService _issuance;
    private readonly TimeProvider _timeProvider;

    public RecoveryCodeDelivery(AtariGoDbContext dbContext, RecoveryCodeProtector protector,
        EmailDeliveryService email, AuthenticationCodeIssuanceService issuance,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(issuance);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _dbContext = dbContext;
        _protector = protector;
        _email = email;
        _issuance = issuance;
        _timeProvider = timeProvider;
    }

    public async Task<RecoveryResult> DeliverAsync(RecoveryRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_protector.IsConfigured || !_email.IsConfigured)
        {
            return RecoveryResult.Unavailable;
        }
        RecoveryDelivery? delivery = await IssueAsync(command, cancellationToken);
        if (delivery is null)
        {
            return RecoveryResult.Accepted;
        }
        bool sent = await _email.TrySendAsync(delivery.Message, cancellationToken);
        if (!sent)
        {
            await InvalidateDeliveryAsync(delivery.CodeId);
            return RecoveryResult.DeliveryFailed;
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
        if (user?.Credential is null || await _issuance.FindRecoveryRetryAtAsync(user.UserId,
            cancellationToken) is not null)
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
            Message = AuthenticationEmailContent.Recovery.CreateMessage(user.Email, value,
                command.CultureName)
        };
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

}
