using AtariGo.Client.Services;
using AtariGo.Contracts;

namespace AtariGo.Client.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class GameSessionSnapshotValidatorTests
{
    [TestMethod]
    public void IsValid_CompleteSnapshotForExpectedGame_ReturnsTrue()
    {
        // Arrange
        GameSessionSnapshot snapshot = GameSessionTestData.Snapshot(1);
        // Act
        bool valid = GameSessionSnapshotValidator.IsValid(snapshot, 1);
        // Assert
        Assert.IsTrue(valid);
    }

    [TestMethod]
    [DataRow("Game")]
    [DataRow("Size")]
    [DataRow("Count")]
    [DataRow("Duplicate")]
    [DataRow("Position")]
    [DataRow("Color")]
    [DataRow("Chat")]
    public void IsValid_InvalidSnapshot_ReturnsFalse(string invalidField)
    {
        // Arrange
        GameSessionSnapshot snapshot = CreateInvalidSnapshot(invalidField);
        // Act
        bool valid = GameSessionSnapshotValidator.IsValid(snapshot, 1);
        // Assert
        Assert.IsFalse(valid);
    }

    private static GameSessionSnapshot CreateInvalidSnapshot(string field)
    {
        GameSessionSnapshot snapshot = GameSessionTestData.Snapshot(1);
        switch (field)
        {
            case "Game":
                snapshot.GameId++;
                break;
            case "Size":
                snapshot.BoardSize++;
                break;
            case "Count":
                snapshot.Cells.RemoveAt(0);
                break;
            case "Duplicate":
                snapshot.Cells[1] = snapshot.Cells[0].Clone();
                break;
            case "Position":
                snapshot.Cells[0].Row = -1;
                break;
            case "Color":
                snapshot.PlayerStoneColor = "Invalid";
                break;
            case "Chat":
                snapshot.MaximumChatMessageLength = 0;
                break;
        }
        return snapshot;
    }
}
