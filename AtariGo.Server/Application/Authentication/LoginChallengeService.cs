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

    public LoginChallengeService(AtariGoDbContext dbContext, LoginCodeDeliveryService delivery,
        LoginAttemptService attempts, ISessionTokenProvider tokens)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(tokens);
        _dbContext = dbContext;
        _delivery = delivery;
        _attempts = attempts;
        _tokens = tokens;
    }

    public async Task<LoginPreparation> StartAsync(User user, string cultureName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        Credential credential = user.Credential
            ?? throw new InvalidOperationException("Account credentials are required.");
        if (!_delivery.IsConfigured)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.Unavailable));
        }
        IssuedSessionToken token = _tokens.Create();
        DateTime now = _attempts.UtcNow;
        var challenge = new LoginChallenge
        {
            UserId = user.UserId, TokenHash = token.Hash,
            AuthenticationVersion = credential.AuthenticationVersion,
            CreatedAt = now, IssueUntil = now.Add(LoginVerificationOptions.IssuanceLifetime)
        };
        DateTime? retryAt = await _delivery.FindRetryAtAsync(challenge, cancellationToken);
        if (retryAt.HasValue)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.RateLimited)
            {
                RetryAt = retryAt
            });
        }
        _dbContext.LoginChallenges.Add(challenge);
        LoginCodeDelivery delivery = _delivery.Prepare(new LoginChallengeContext(user, challenge),
            cultureName);
        return new LoginPreparation(CreatePendingResult(user, challenge) with
        {
            ChallengeToken = token.Value
        }, delivery);
    }

    public async Task<LoginOperationResult> SendAsync(LoginPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.Delivery is null)
        {
            return preparation.Result;
        }
        bool sent = await _delivery.SendAsync(preparation.Delivery, cancellationToken);
        using var timeout = new CancellationTokenSource(finalizationTimeout);
        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(timeout.Token);
        LoginChallengeContext? context = await FindAsync(preparation.Result.ChallengeToken,
            timeout.Token);
        LoginOperationResult result = new LoginOperationResult(LoginStatus.InvalidChallenge);
        if (context is not null && IsPending(context)
            && context.Challenge.CodeBinding == preparation.Delivery.Binding)
        {
            result = await FinalizeDeliveryAsync(context, sent, timeout.Token);
            result = result with
            {
                ChallengeToken = context.Challenge.ClosedAt is null
                    ? preparation.Result.ChallengeToken : string.Empty
            };
        }
        await _dbContext.SaveChangesAsync(timeout.Token);
        await transaction.CommitAsync(timeout.Token);
        return result;
    }

    public async Task<LoginOperationResult> VerifyAsync(LoginCodeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        LoginChallengeContext? context = await FindAsync(command.ChallengeToken, cancellationToken);
        LoginOperationResult result = context is null
            ? new LoginOperationResult(LoginStatus.InvalidChallenge)
            : await CompleteAsync(context, command.Code, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<LoginOperationResult> ResendAsync(LoginChallengeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _dbContext.ChangeTracker.Clear();
        LoginPreparation preparation;
        await using (IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken))
        {
            LoginChallengeContext? context = await FindAsync(command.ChallengeToken,
                cancellationToken);
            preparation = await PrepareResendAsync(context, command, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await SendAsync(preparation, cancellationToken);
    }

    public async Task CancelAsync(string token, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        LoginChallengeContext? context = await FindAsync(token, cancellationToken);
        if (context is not null)
        {
            Close(context.Challenge);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<LoginPreparation> PrepareResendAsync(LoginChallengeContext? context,
        LoginChallengeCommand command, CancellationToken cancellationToken)
    {
        if (context is null || !IsPending(context)
            || context.Challenge.IssueUntil <= _attempts.UtcNow)
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
        DateTime? retryAt = await _delivery.FindRetryAtAsync(context.Challenge, cancellationToken);
        if (retryAt.HasValue)
        {
            return new LoginPreparation(new LoginOperationResult(LoginStatus.RateLimited)
            {
                RetryAt = retryAt
            });
        }
        LoginCodeDelivery delivery = _delivery.Prepare(context, command.CultureName);
        return new LoginPreparation(CreatePendingResult(context.User, context.Challenge) with
        {
            ChallengeToken = command.ChallengeToken
        }, delivery);
    }

    private async Task<LoginOperationResult> CompleteAsync(LoginChallengeContext context,
        string code, CancellationToken cancellationToken)
    {
        if (!IsPending(context))
        {
            return new LoginOperationResult(LoginStatus.InvalidChallenge);
        }
        LoginOperationResult? restriction = await _attempts.CheckAccessAsync(context.User,
            cancellationToken);
        if (restriction is not null)
        {
            return restriction;
        }
        LoginChallenge challenge = context.Challenge;
        AuthenticationCode? active = challenge.ActiveCode;
        if (active is null || active.UsedAt is not null
            || challenge.DeliveryState != LoginVerificationOptions.Sent)
        {
            return new LoginOperationResult(LoginStatus.InvalidChallenge);
        }
        if (active.ExpiresAt <= _attempts.UtcNow)
        {
            return new LoginOperationResult(LoginStatus.CodeExpired);
        }
        if (!_delivery.Matches(challenge, code))
        {
            active.AttemptCount++;
            await _attempts.RegisterFailureAsync(context.User, cancellationToken);
            Credential credential = context.User.Credential
                ?? throw new InvalidOperationException("Account credentials are required.");
            return new LoginOperationResult(credential.LockedUntil > _attempts.UtcNow
                ? LoginStatus.TemporarilyLocked : LoginStatus.InvalidCode)
            {
                RetryAt = credential.LockedUntil
            };
        }
        return CreateSession(context);
    }

    private LoginOperationResult CreateSession(LoginChallengeContext context)
    {
        Credential credential = context.User.Credential
            ?? throw new InvalidOperationException("Account credentials are required.");
        DateTime now = _attempts.UtcNow;
        Close(context.Challenge);
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

    private async Task<LoginOperationResult> FinalizeDeliveryAsync(LoginChallengeContext context,
        bool sent, CancellationToken cancellationToken)
    {
        LoginOperationResult? restriction = await _attempts.CheckAccessAsync(context.User,
            cancellationToken);
        if (restriction is not null)
        {
            Close(context.Challenge);
            return restriction;
        }
        LoginChallenge challenge = context.Challenge;
        challenge.DeliveryState = sent ? LoginVerificationOptions.Sent
            : LoginVerificationOptions.Failed;
        if (!sent)
        {
            challenge.DeliveryFailures++;
            if (challenge.ActiveCode is not null)
            {
                challenge.ActiveCode.UsedAt = _attempts.UtcNow;
            }
            if (challenge.DeliveryFailures >= LoginVerificationOptions.MaximumDeliveryFailures)
            {
                Close(challenge);
            }
        }
        return CreatePendingResult(context.User, challenge) with
        {
            Status = sent ? LoginStatus.ChallengeRequired : LoginStatus.DeliveryFailed
        };
    }

    private async Task<LoginChallengeContext?> FindAsync(string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumTokenLength)
        {
            return null;
        }
        string hash = _tokens.Hash(token);
        int? userId = await _dbContext.LoginChallenges.AsNoTracking()
            .Where(challenge => challenge.TokenHash == hash)
            .Select(challenge => (int?)challenge.UserId).SingleOrDefaultAsync(cancellationToken);
        if (!userId.HasValue)
        {
            return null;
        }
        var accounts = new AccountCredentialStore(_dbContext);
        User? user = await accounts.LockAccountAsync(userId.Value, cancellationToken);
        if (user?.Credential is null)
        {
            return null;
        }
        LoginChallenge challenge = await _dbContext.LoginChallenges.Include(item => item.ActiveCode)
            .SingleAsync(item => item.TokenHash == hash, cancellationToken);
        return new LoginChallengeContext(user, challenge);
    }

    private static bool IsPending(LoginChallengeContext context)
    {
        return context.Challenge.ClosedAt is null
            && context.User.Credential?.AuthenticationVersion
                == context.Challenge.AuthenticationVersion;
    }

    private void Close(LoginChallenge challenge)
    {
        challenge.ClosedAt = _attempts.UtcNow;
        if (challenge.ActiveCode is not null)
        {
            challenge.ActiveCode.UsedAt = _attempts.UtcNow;
        }
    }

    private LoginOperationResult CreatePendingResult(User user, LoginChallenge challenge)
    {
        int separator = user.Email.IndexOf('@');
        string masked = separator > 0 ? user.Email[..1] + "***" + user.Email[separator..] : "***";
        return new LoginOperationResult(LoginStatus.ChallengeRequired)
        {
            MaskedEmail = masked, CodeExpiresAt = challenge.ActiveCode?.ExpiresAt,
            RetryAt = challenge.LastSentAt?.Add(_delivery.ResendDelay)
        };
    }
}
