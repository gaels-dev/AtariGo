using Microsoft.Data.SqlClient;

namespace AtariGo.Server.Configuration
{
    public static class DatabaseReadinessCommand
    {
        public const string CommandLineSwitch = "--check-database";
        public const int FailureExitCode = 1;

        private const int SuccessExitCode = 0;
        private const int CheckTimeoutSeconds = 20;

        public static async Task<int> RunAsync(IServiceProvider services, ILogger logger)
        {
            int exitCode = FailureExitCode;
            using IServiceScope scope = services.CreateScope();
            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(CheckTimeoutSeconds));
            DatabaseReadinessCheck check = scope.ServiceProvider
                .GetRequiredService<DatabaseReadinessCheck>();

            try
            {
                int checkedTables = await check.CheckAsync(cancellation.Token);
                logger.LogInformation(
                    "Database check passed. Verified {TableCount} mapped tables without reading "
                        + "records or changing data. No network listener was started.",
                    checkedTables);
                exitCode = SuccessExitCode;
            }
            catch (SqlException exception)
            {
                logger.LogError(
                    "Database check failed. SQL error number: {ErrorNumber}. Check the SQL "
                        + "service, private settings, SQL certificate, permissions, and schema.",
                    exception.Number);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                logger.LogError("Database check exceeded its {Seconds}-second limit.",
                    CheckTimeoutSeconds);
            }

            return exitCode;
        }
    }
}
