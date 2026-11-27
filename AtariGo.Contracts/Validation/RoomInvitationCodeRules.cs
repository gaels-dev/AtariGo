namespace AtariGo.Contracts.Validation;

public static class RoomInvitationCodeRules
{
    public const int GeneratedLength = 6;
    public const int MaximumLength = 10;

    public static bool IsValid(string? code)
    {
        return code is not null && code.Length == GeneratedLength
            && code.All(IsUppercaseAlphanumeric);
    }

    private static bool IsUppercaseAlphanumeric(char character)
    {
        return char.IsAsciiDigit(character) || character is >= 'A' and <= 'Z';
    }
}
