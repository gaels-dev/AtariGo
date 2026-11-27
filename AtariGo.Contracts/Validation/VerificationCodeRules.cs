namespace AtariGo.Contracts.Validation;

public static class VerificationCodeRules
{
    public const int Length = 6;
    public const int LifetimeMinutes = 10;
    public static TimeSpan Lifetime => TimeSpan.FromMinutes(LifetimeMinutes);

    public static bool IsValid(string? code)
    {
        return code is not null && code.Length == Length
            && code.All(character => character is >= '0' and <= '9');
    }
}
