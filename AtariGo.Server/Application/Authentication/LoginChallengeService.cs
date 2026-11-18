using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using AtariGo.Server.Data;
using AtariGo.Server.Data.Entities;
using AtariGo.Server.Security;

namespace AtariGo.Server.Application.Authentication;

public sealed class LoginChallengeService
{
    private const int MaximumTokenLength = 128;
    private static readonly TimeSpan sessionLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan finalizationTimeout = TimeSpan.FromSeconds(10);
    private readonly AtariGoDbContext _dbContext;
    private readonly LoginCodeDeliveryService _delivery;
    private readonly LoginAttemptService _attempts;
    private readonly ISessionTokenProvider _tokens;
    private readonly PendingLoginStore _pending;

    public LoginChallengeService(AtariGoDbContext dbContext, LoginCodeDeliveryService delivery,
        LoginAttemptService attempts, ISessionTokenProvider tokens, PendingLoginStore pending)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(pending);
        _dbContext = dbContext;
        _delivery = delivery;
        _attempts = attempts;
        _tokens = tokens;
        _pending = pending;
    }

    public async Task<LoginPreparation> StartAsync(User user, string cultureName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(cultureName);
        if (!_delivery.IsConfigured)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.Unavailable));
        }
        DateTime? retryAt = await _delivery.FindRetryAtAsync(user.UserId, cancellationToken);
        if (retryAt.HasValue)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.RateLimited)
            {
                RetryAt = retryAt
            });
        }
        IssuedSessionToken token = _tokens.Create();
        var pending = new PendingLogin
        {
            UserId = user.UserId, TokenHash = token.Hash,
            IssueUntil = _attempts.UtcNow.Add(LoginVerificationOptions.IssuanceLifetime)
        };
        LoginCodeDelivery delivery = _delivery.Prepare(user, pending, cultureName);
        var context = new PendingLoginContext(user, delivery.Pending, delivery.Code);
        return new LoginPreparation(CreatePendingResult(context) with
        {
            ChallengeToken = token.Value
        }, delivery) { StartsLogin = true };
    }

    public async Task<LoginOperationResult> SendAsync(LoginPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.Delivery is null)
        {
            return preparation.Result;
        }
        LoginCodeDelivery delivery = preparation.Delivery;
        PendingLogin pending = delivery.Pending with
        {
            CodeId = delivery.Code.AuthenticationCodeId
        };
        if (preparation.StartsLogin && !_pending.TryAdd(pending))
        {
            delivery.Message.Body = string.Empty;
            return new LoginOperationResult(LoginStatus.Unavailable);
        }
        bool sent = await _delivery.SendAsync(delivery, cancellationToken);
        using var timeout = new CancellationTokenSource(finalizationTimeout);
        return await FinalizeSendAsync(preparation, sent, timeout.Token);
    }

    public async Task<LoginOperationResult> VerifyAsync(LoginCodeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        string hash = GetTokenHash(command.ChallengeToken);
        using PendingLoginLease lease = await _pending.EnterAsync(hash, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        PendingLoginContext? context = await FindAsync(hash, cancellationToken);
        LoginOperationResult result = context is null
            ? new LoginOperationResult(LoginStatus.InvalidChallenge)
            : await CompleteAsync(context, command.Code, cancellationToken);
        await CommitAsync(context, transaction, cancellationToken);
        return result;
    }

    public async Task<LoginOperationResult> ResendAsync(LoginChallengeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        LoginPreparation preparation;
        string hash = GetTokenHash(command.ChallengeToken);
        using (PendingLoginLease lease = await _pending.EnterAsync(hash, cancellationToken))
        {
            _dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            PendingLoginContext? context = await FindAsync(hash, cancellationToken);
            preparation = await PrepareResendAsync(context, command, cancellationToken);
            await CommitAsync(context, transaction, cancellationToken);
        }
        return await SendAsync(preparation, cancellationToken);
    }

    public async Task CancelAsync(string token, CancellationToken cancellationToken)
    {
        string hash = GetTokenHash(token);
        using PendingLoginLease lease = await _pending.EnterAsync(hash, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        PendingLoginContext? context = await FindAsync(hash, cancellationToken);
        if (context is not null)
        {
            Close(context);
        }
        await CommitAsync(context, transaction, cancellationToken);
    }

    private async Task<LoginOperationResult> FinalizeSendAsync(LoginPreparation preparation,
        bool sent, CancellationToken cancellationToken)
    {
        string hash = GetTokenHash(preparation.Result.ChallengeToken);
        using PendingLoginLease lease = await _pending.EnterAsync(hash, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        PendingLoginContext? context = await FindAsync(hash, cancellationToken);
        var result = new LoginOperationResult(LoginStatus.InvalidChallenge);
        if (context is not null
            && context.Code.AuthenticationCodeId == preparation.Delivery!.Code.AuthenticationCodeId)
        {
            result = await FinalizeDeliveryAsync(context, sent, cancellationToken);
            result = result with
            {
                ChallengeToken = context.Pending.IsClosed
                    ? string.Empty : preparation.Result.ChallengeToken
            };
        }
        await CommitAsync(context, transaction, cancellationToken);
        return result;
    }

    private async Task<LoginPreparation> PrepareResendAsync(PendingLoginContext? context,
        LoginChallengeCommand command, CancellationToken cancellationToken)
    {
        if (context is null || context.Pending.IssueUntil <= _attempts.UtcNow)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.InvalidChallenge));
        }
        LoginOperationResult? restriction = await _attempts.CheckAccessAsync(context.User,
            cancellationToken);
        if (restriction is not null)
        {
            return new LoginPreparation(restriction);
        }
        if (!_delivery.IsConfigured)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.Unavailable));
        }
        DateTime? retryAt = await _delivery.FindRetryAtAsync(context.User.UserId,
            cancellationToken);
        if (retryAt.HasValue)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.RateLimited)
            {
                RetryAt = retryAt
            });
        }
        context.Code.UsedAt = _attempts.UtcNow;
        LoginCodeDelivery delivery = _delivery.Prepare(context.User, context.Pending,
            command.CultureName);
        context.Pending = delivery.Pending;
        context.Code = delivery.Code;
        return new LoginPreparation(CreatePendingResult(context) with
        {
            ChallengeToken = command.ChallengeToken
        }, delivery);
    }

    private async Task<LoginOperationResult> CompleteAsync(PendingLoginContext context,
        string code, CancellationToken cancellationToken)
    {
        LoginOperationResult? restriction = await _attempts.CheckAccessAsync(context.User,
            cancellationToken);
        if (restriction is not null)
        {
            return restriction;
        }
        if (context.Pending.DeliveryStatus != LoginDeliveryStatus.Sent)
        {
            return new LoginOperationResult(LoginStatus.InvalidChallenge);
        }
        if (context.Code.ExpiresAt <= _attempts.UtcNow)
        {
            return new LoginOperationResult(LoginStatus.CodeExpired);
        }
        if (!_delivery.Matches(context, code))
        {
            context.Code.AttemptCount++;
            await _attempts.RegisterFailureAsync(context.User, cancellationToken);
            return CreateFailureResult(context);
        }
        return CreateSession(context);
    }

    private LoginOperationResult CreateFailureResult(PendingLoginContext context)
    {
        Credential credential = context.User.Credential
            ?? throw new InvalidOperationException("Account credentials are required.");
        bool locked = credential.LockedUntil > _attempts.UtcNow;
        if (locked)
        {
            Close(context);
        }
        return new LoginOperationResult(locked
            ? LoginStatus.TemporarilyLocked : LoginStatus.InvalidCode)
        {
            RetryAt = credential.LockedUntil
        };
    }

    private LoginOperationResult CreateSession(PendingLoginContext context)
    {
        Credential credential = context.User.Credential
            ?? throw new InvalidOperationException("Account credentials are required.");
        DateTime now = _attempts.UtcNow;
        Close(context);
        credential.FailedAttempts = 0;
        credential.LockedUntil = null;
        IssuedSessionToken token = _tokens.Create();
        _dbContext.Sessions.Add(new Session
        {
            UserId = context.User.UserId, SessionTokenHash = token.Hash,
            StartedAt = now, LastActivityAt = now, ExpiresAt = now.Add(sessionLifetime),
            State = DatabaseValues.SessionStates.Active
        });
        return new LoginOperationResult(LoginStatus.Success)
        {
            UserId = context.User.UserId, UserName = context.User.UserName,
            SessionToken = token.Value
        };
    }

    private async Task<LoginOperationResult> FinalizeDeliveryAsync(PendingLoginContext context,
        bool sent, CancellationToken cancellationToken)
    {
        LoginOperationResult? restriction = await _attempts.CheckAccessAsync(context.User,
            cancellationToken);
        if (restriction is not null)
        {
            Close(context);
            return restriction;
        }
        context.Pending = context.Pending with
        {
            DeliveryStatus = sent ? LoginDeliveryStatus.Sent : LoginDeliveryStatus.Failed,
            DeliveryFailures = context.Pending.DeliveryFailures + (sent ? 0 : 1)
        };
        if (context.Pending.DeliveryFailures >= LoginVerificationOptions.MaximumDeliveryFailures)
        {
            Close(context);
        }
        return CreatePendingResult(context) with
        {
            Status = sent ? LoginStatus.ChallengeRequired : LoginStatus.DeliveryFailed
        };
    }

    private async Task<PendingLoginContext?> FindAsync(string hash,
        CancellationToken cancellationToken)
    {
        PendingLogin? pending = _pending.Find(hash);
        if (pending is null)
        {
            return null;
        }
        var accounts = new AccountCredentialStore(_dbContext);
        User? user = await accounts.LockAccountAsync(pending.UserId, cancellationToken);
        AuthenticationCode? code = await _dbContext.AuthenticationCodes.SingleOrDefaultAsync(
            item => item.AuthenticationCodeId == pending.CodeId && item.UserId == pending.UserId
                && item.CodeType == LoginVerificationOptions.Purpose, cancellationToken);
        if (user?.Credential is null || code is null || code.UsedAt is not null)
        {
            _pending.Publish(pending with { IsClosed = true });
            return null;
        }
        return new PendingLoginContext(user, pending, code);
    }

    private async Task CommitAsync(PendingLoginContext? context, IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (context is not null)
        {
            _pending.Publish(context.Pending with { CodeId = context.Code.AuthenticationCodeId });
        }
    }

    private string GetTokenHash(string token)
    {
        return string.IsNullOrWhiteSpace(token) || token.Length > MaximumTokenLength
            ? string.Empty : _tokens.Hash(token);
    }

    private void Close(PendingLoginContext context)
    {
        context.Pending = context.Pending with { IsClosed = true };
        context.Code.UsedAt = _attempts.UtcNow;
    }

    private LoginOperationResult CreatePendingResult(PendingLoginContext context)
    {
        string email = context.User.Email;
        int separator = email.IndexOf('@');
        string masked = separator > 0 ? email[..1] + "***" + email[separator..] : "***";
        return new LoginOperationResult(LoginStatus.ChallengeRequired)
        {
            MaskedEmail = masked, CodeExpiresAt = context.Code.ExpiresAt,
            RetryAt = context.Code.CreatedAt.Add(_delivery.ResendDelay)
        };
    }
}
