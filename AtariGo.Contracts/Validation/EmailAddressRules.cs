using System.Globalization;
using System.Net.Mail;

namespace AtariGo.Contracts.Validation;

public static class EmailAddressRules
{
    public const int MaximumLength = 254;
    private const int MaximumLocalLength = 64;
    private const int MaximumLabelLength = 63;

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Any(char.IsControl))
        {
            return false;
        }
        string address = email.Trim();
        if (address.Length > MaximumLength || address.Any(char.IsWhiteSpace)
            || !MailAddress.TryCreate(address, out MailAddress? parsed))
        {
            return false;
        }
        return parsed.DisplayName.Length == 0 && IsValidLocalPart(parsed.User)
            && string.Equals(address, parsed.Address, StringComparison.Ordinal)
            && IsValidDomain(parsed.Host);
    }

    private static bool IsValidLocalPart(string localPart)
    {
        return localPart.Length <= MaximumLocalLength
            && !localPart.StartsWith('.') && !localPart.EndsWith('.')
            && !localPart.Contains("..", StringComparison.Ordinal)
            && localPart.All(IsLocalCharacter);
    }

    private static bool IsLocalCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character)
            || "!#$%&'*+-/=?^_`{|}~.".Contains(character);
    }

    private static bool IsValidDomain(string host)
    {
        try
        {
            string asciiHost = new IdnMapping().GetAscii(host);
            string[] labels = asciiHost.Split('.');
            return labels.Length >= 2 && labels.All(IsValidLabel)
                && labels[^1].Any(char.IsAsciiLetter);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsValidLabel(string label)
    {
        return label.Length is > 0 and <= MaximumLabelLength
            && char.IsAsciiLetterOrDigit(label[0])
            && char.IsAsciiLetterOrDigit(label[^1])
            && label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }
}
