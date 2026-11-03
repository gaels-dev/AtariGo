using System;
using System.Diagnostics;
using System.IO;

namespace AtariGo.Client.Services;

public sealed class ClientDiagnostics : IDisposable
{
    private readonly ClientFileTraceListener _listener;

    private ClientDiagnostics(ClientFileTraceListener listener)
    {
        _listener = listener;
    }

    public static ClientDiagnostics? TryStart()
    {
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "AtariGo", "logs");
            Directory.CreateDirectory(directory);
            string fileName
                = $"client-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}.log";
            var listener = new ClientFileTraceListener(Path.Combine(directory, fileName));
            Trace.Listeners.Add(listener);
            Trace.TraceInformation("Client diagnostics started.");
            return new ClientDiagnostics(listener);
        }
        catch (IOException exception)
        {
            Debug.WriteLine($"Client diagnostics unavailable: {exception.GetType().Name}.");
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.WriteLine($"Client diagnostics unavailable: {exception.GetType().Name}.");
        }

        return null;
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(_listener);
        _listener.Dispose();
    }
}
