namespace AtariGo.Diagnostics;

public sealed class DiagnosticError
{
    private DiagnosticError(string typeName, int hResult, int? nativeCode)
    {
        TypeName = typeName;
        HResult = hResult;
        NativeCode = nativeCode;
    }

    public string TypeName { get; }

    public int HResult { get; }

    public int? NativeCode { get; }

    public static DiagnosticError FromException(Exception exception, int? nativeCode = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string typeName = DiagnosticName.NormalizeTypeName(exception.GetType());
        return new DiagnosticError(typeName, exception.HResult, nativeCode);
    }
}
