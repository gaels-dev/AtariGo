using System.Runtime.CompilerServices;

namespace AtariGo.Diagnostics;

internal static class DiagnosticName
{
    internal const int MaximumLength = 128;

    internal static void Validate(string value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > MaximumLength || !value.All(IsAllowedCharacter))
        {
            throw new ArgumentException(
                "Diagnostic names require at most 128 ASCII letters, digits, dots or underscores.",
                parameterName);
        }
    }

    internal static string NormalizeTypeName(Type type)
    {
        string name = type.Name;
        int length = Math.Min(name.Length, MaximumLength);
        char[] characters = name[..length].ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (!IsAllowedCharacter(characters[index]))
            {
                characters[index] = '_';
            }
        }
        return new string(characters);
    }

    private static bool IsAllowedCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is '.' or '_';
    }
}
