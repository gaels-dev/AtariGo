using System.Diagnostics;
using System.IO;


using AtariGo.Client.Services;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class ClientFileTraceListenerTests
{
    [TestMethod]
    public void TraceEvent_FormattedFailure_PersistsTimestampSeverityAndArguments()
    {
        // Arrange
        string path = Path.GetTempFileName();

        try
        {
            // Act
            using (var listener = new ClientFileTraceListener(path))
            {
                listener.TraceEvent(null, "test", TraceEventType.Warning, 0,
                    "Friend request failed: {0}.", "Unavailable");
            }

            string content = File.ReadAllText(path);

            // Assert
            StringAssert.Contains(content, "-06:00");
            StringAssert.Contains(content, "Warning: Friend request failed: Unavailable.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
