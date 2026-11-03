using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services
{
    [TestClass]
    [TestCategory("Unit")]
    public sealed class GrpcClientSettingsTests
    {
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void ParseServerAddress_MissingConfiguration_UsesLocalHttps(string? address)
        {
            // Arrange
            var expectedAddress = new Uri("https://localhost:7061");

            // Act
            Uri actualAddress = GrpcClientSettings.ParseServerAddress(address);

            // Assert
            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [TestMethod]
        [DataRow("https://192.168.100.24:7061")]
        [DataRow("https://atarigo.example")]
        [DataRow("https://[::1]:7061/")]
        [DataRow("  https://server-name:7443  ")]
        public void ParseServerAddress_ValidHttpsOrigin_ReturnsConfiguredAddress(string address)
        {
            // Arrange
            var expectedAddress = new Uri(address.Trim());

            // Act
            Uri actualAddress = GrpcClientSettings.ParseServerAddress(address);

            // Assert
            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [TestMethod]
        [DataRow("http://localhost:7061")]
        [DataRow("ftp://server-name")]
        [DataRow("server-name:7061")]
        [DataRow("not a server address")]
        [DataRow("https://0.0.0.0:7061")]
        [DataRow("https://[::]:7061")]
        [DataRow("https://server-name:0")]
        [DataRow("https://server-name:65536")]
        [DataRow("https://user:password@server-name:7061")]
        [DataRow("https://server-name:7061/api")]
        [DataRow("https://server-name:7061/?token=secret")]
        [DataRow("https://server-name:7061/#fragment")]
        public void ParseServerAddress_InvalidOrigin_ThrowsArgumentException(string address)
        {
            // Arrange
            Action parseAddress = () => GrpcClientSettings.ParseServerAddress(address);

            // Act
            ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(parseAddress);

            // Assert
            Assert.AreEqual("configuredAddress", exception.ParamName);
        }
    }
}
