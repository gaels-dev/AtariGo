namespace AtariGo.Contracts.Validation;

public static class DescriptionRules
{
    public const int MaximumLength = 200;

    public static bool IsValid(string? description)
    {
        return description is null || description.Length <= MaximumLength;
    }
}
