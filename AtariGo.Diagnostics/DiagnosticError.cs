namespace AtariGo.Diagnostics;

public sealed class DiagnosticError
{
    private DiagnosticError(string typeName, int? hResult, int? nativeCode)
    {
        TypeName = typeName;
        HResult = hResult;
        NativeCode = nativeCode;
    }

    public string TypeName
    {
        get;
    }

    public int? HResult
    {
        get;
    }

    public int? NativeCode
    {
        get;
    }

    public static DiagnosticError FromException(Exception exception, int? nativeCode = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string typeName = DiagnosticName.NormalizeTypeName(exception.GetType());
        return new DiagnosticError(typeName, exception.HResult, nativeCode);
    }

    public static DiagnosticError FromType(Type exceptionType)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);
        if (!typeof(Exception).IsAssignableFrom(exceptionType))
        {
            throw new ArgumentException("An exception type is required.", nameof(exceptionType));
        }
        return new DiagnosticError(DiagnosticName.NormalizeTypeName(exceptionType), null, null);
    }
}
