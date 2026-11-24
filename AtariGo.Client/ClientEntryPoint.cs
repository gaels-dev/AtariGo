using AtariGo.Client.Services;

namespace AtariGo.Client;

public static class ClientEntryPoint
{
    public static async Task<int> Main()
    {
        await using ClientDiagnostics diagnostics = ClientDiagnostics.Start();
        var completion = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => RunApplication(diagnostics, completion));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await completion.Task.ConfigureAwait(false);
    }

    private static void RunApplication(ClientDiagnostics diagnostics,
        TaskCompletionSource<int> completion)
    {
        try
        {
            var application = new App(diagnostics);
            application.InitializeComponent();
            completion.TrySetResult(application.Run());
        }
        catch (Exception failure)
        {
            diagnostics.RecordFailure("ClientTerminated", failure);
            completion.TrySetException(failure);
        }
    }
}
