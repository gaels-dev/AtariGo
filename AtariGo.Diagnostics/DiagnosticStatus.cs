namespace AtariGo.Diagnostics;

public sealed class DiagnosticStatus
{
    public DiagnosticStatus(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Enum.IsDefined(value.GetType(), value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        TypeName = DiagnosticName.NormalizeTypeName(value.GetType());
        Value = value.ToString();
    }

    public string TypeName
    {
        get;
    }

    public string Value
    {
        get;
    }
}
