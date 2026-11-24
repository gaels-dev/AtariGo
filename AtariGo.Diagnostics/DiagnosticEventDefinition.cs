namespace AtariGo.Diagnostics;

public sealed class DiagnosticEventDefinition
{
    public DiagnosticEventDefinition(int id, string source, string operation)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        DiagnosticName.Validate(source);
        DiagnosticName.Validate(operation);
        Id = id;
        Source = source;
        Operation = operation;
    }

    public int Id { get; }

    public string Source { get; }

    public string Operation { get; }
}
