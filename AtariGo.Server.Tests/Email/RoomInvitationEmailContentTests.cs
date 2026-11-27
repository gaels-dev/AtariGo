using AtariGo.Server.Application.Email;
using AtariGo.Server.Application.RoomInvitations;
using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Tests.Email;

[TestClass]
[TestCategory("Unit")]
public sealed class RoomInvitationEmailContentTests
{
    [TestMethod]
    [DataRow("es-MX", "Código de sala")]
    [DataRow("de-DE", "Raumcode")]
    [DataRow("fr-FR", "Room code")]
    [DataRow("not a culture", "Room code")]
    public void CreateMessage_SupportedOrUnknownCulture_FormatsLocalizedInvitation(
        string culture, string expected)
    {
        // Arrange
        var invitation = new RoomInvitationEntry(1, 42, DateTimeOffset.MaxValue)
        {
            RoomCode = "AZ9Q2M",
            Sender = new LobbyParticipant(1, "Host"),
            Recipient = new LobbyParticipant(2, "Friend")
        };

        // Act
        EmailMessage message = RoomInvitationEmailContent.CreateMessage(
            invitation, "friend@example.test", culture);

        // Assert
        Assert.AreEqual("friend@example.test", message.Recipient);
        StringAssert.Contains(message.Body, "Host");
        StringAssert.Contains(message.Body, "AZ9Q2M");
        StringAssert.Contains(message.Body, expected);
        Assert.IsFalse(message.Body.Contains("{0}", StringComparison.Ordinal));
    }
}
