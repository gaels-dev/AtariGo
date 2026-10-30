using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;

using AtariGo.Client.Services;

namespace AtariGo.Client
{
    public partial class App : Application
    {
        private const int InvalidConfigurationExitCode = 1;

        private GrpcClientConnection? _grpcConnection;

        private static string GetLocalizedMessage(string resourceKey)
        {
            return AtariGo.Client.Properties.Resources.ResourceManager.GetString(
                resourceKey,
                CultureInfo.CurrentUICulture) ?? resourceKey;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                _grpcConnection = GrpcClientConnection.Shared;
            }
            catch (ArgumentException exception)
            {
                Trace.TraceError("Invalid gRPC configuration: {0}", exception.Message);
                MessageBox.Show(
                    GetLocalizedMessage("Connection_Err_InvalidAddress"),
                    GetLocalizedMessage("Connection_Title_ConfigurationError"),
                    MessageBoxButton.OK);
                Shutdown(InvalidConfigurationExitCode);
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _grpcConnection?.Dispose();
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }
}
