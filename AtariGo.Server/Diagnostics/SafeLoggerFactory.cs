namespace AtariGo.Server.Diagnostics;

public sealed class SafeLoggerFactory : ILoggerFactory
{
    private readonly LoggerFactory _inner;
    private readonly LoggerExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public SafeLoggerFactory(LoggerFactory inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        string category = SafeLogNames.Category(categoryName);
        return new SafeLogger(_inner.CreateLogger(category), category, _scopes);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _inner.AddProvider(provider);
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}
