
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using AtariGo.Server.Configuration;

namespace AtariGo.Server.Tests.Configuration
{
    [TestClass]
    [TestCategory("Unit")]
    public sealed class DatabaseConnectionSettingsTests
    {
        private const string ConnectionKey = "ConnectionStrings:AtariGoConnection";

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("...")]
        [DataRow("Server=example;")]
        [DataRow("Database=AtariGo;")]
        public void GetConnectionString_MissingRequiredSettings_RejectsConfiguration(string? value)
        {
            // Arrange
            IConfiguration configuration = CreateConfiguration(value);

            // Act
            Action validate = () => DatabaseConnectionSettings.GetConnectionString(configuration);

            // Assert
            Assert.ThrowsExactly<InvalidOperationException>(validate);
        }

        [TestMethod]
        public void GetConnectionString_InvalidSyntax_PreservesCauseAndSanitizesOuterMessage()
        {
            // Arrange
            const string privateMarker = "PRIVATE_TEST_MARKER";
            IConfiguration configuration = CreateConfiguration(privateMarker);

            // Act
            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
                () => DatabaseConnectionSettings.GetConnectionString(configuration));

            // Assert
            Assert.IsInstanceOfType<ArgumentException>(exception.InnerException);
            Assert.IsFalse(exception.Message.Contains(privateMarker, StringComparison.Ordinal));
        }

        [TestMethod]
        [DataRow("Server=example;Database=AtariGo;Integrated Security=True;")]
        [DataRow("Server=example;Database=AtariGo;User Id=test;Password=fake-test-password;")]
        public void GetConnectionString_ValidSettings_PreservesAuthenticationOptions(string value)
        {
            // Arrange
            IConfiguration configuration = CreateConfiguration(value);

            // Act
            string actual = DatabaseConnectionSettings.GetConnectionString(configuration);

            // Assert
            Assert.AreEqual(value, actual);
        }

        [TestMethod]
        public void Validate_InvalidSettings_ReturnsFailureWithoutThrowingPrivateCause()
        {
            // Arrange
            IConfiguration configuration = CreateConfiguration("PRIVATE_TEST_MARKER");

            // Act
            bool valid = DatabaseConnectionSettings.Validate(configuration, NullLogger.Instance);

            // Assert
            Assert.IsFalse(valid);
        }

        private static IConfiguration CreateConfiguration(string? connectionString)
        {
            var settings = new Dictionary<string, string?>
            {
                [ConnectionKey] = connectionString
            };
            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }
    }
}
