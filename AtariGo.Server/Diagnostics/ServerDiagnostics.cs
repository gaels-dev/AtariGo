using Microsoft.Extensions.DependencyInjection.Extensions;

using AtariGo.Diagnostics;

namespace AtariGo.Server.Diagnostics;

public sealed class ServerDiagnostics : IAsyncDisposable
{
    private readonly DiagnosticFileWriter _writer;

    public ServerDiagnostics(DiagnosticFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _writer = new DiagnosticFileWriter(options);
    }

    public DiagnosticStorageStatus Status => _writer.Status;

    public static ServerDiagnostics Configure(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        DiagnosticFileOptions options = ServerDiagnosticSettings.Read(builder.Configuration);
        var diagnostics = new ServerDiagnostics(options);
        builder.Logging.AddProvider(diagnostics.CreateProvider());
        builder.Services.AddSingleton<LoggerFactory>();
        builder.Services.PostConfigure<LoggerFactoryOptions>(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.None);
        builder.Services.Replace(ServiceDescriptor.Singleton<ILoggerFactory, SafeLoggerFactory>());
        return diagnostics;
    }

    public ILoggerProvider CreateProvider()
    {
        return new DiagnosticFileLoggerProvider(_writer);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return _writer.FlushAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _writer.DisposeAsync();
    }
}
