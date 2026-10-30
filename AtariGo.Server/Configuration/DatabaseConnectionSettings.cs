using System;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AtariGo.Server.Configuration
{
    public static class DatabaseConnectionSettings
    {
        public const string ConnectionName = "AtariGoConnection";

        public static bool Validate(IConfiguration configuration, ILogger logger)
        {
            bool isValid = true;
            try
            {
                GetConnectionString(configuration);
            }
            catch (InvalidOperationException exception)
            {
                // Only the sanitized outer message is safe for operator logs.
                logger.LogError("Server configuration is invalid: {ConfigurationError}",
                    exception.Message);
                isValid = false;
            }

            return isValid;
        }

        public static string GetConnectionString(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            string? connectionString = configuration.GetConnectionString(ConnectionName);
            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Trim() == "...")
            {
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:AtariGoConnection in user secrets "
                        + "or the server environment before starting the server.");
            }

            SqlConnectionStringBuilder settings = ParseConnectionString(connectionString);
            if (string.IsNullOrWhiteSpace(settings.DataSource)
                || string.IsNullOrWhiteSpace(settings.InitialCatalog))
            {
                throw new InvalidOperationException(
                    "The SQL Server connection must specify both Server and Database.");
            }

            return connectionString;
        }

        private static SqlConnectionStringBuilder ParseConnectionString(string connectionString)
        {
            try
            {
                return new SqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException exception)
            {
                // Parser messages may contain fragments of the secret connection string.
                throw new InvalidOperationException(
                    "The SQL Server connection string is invalid. "
                        + "Review the private configuration; its value has not been logged.",
                    exception);
            }
        }
    }
}
