using AtariGo.Contracts.Validation;

namespace AtariGo.Client.Tests.Models;

[TestClass]
[TestCategory("Unit")]
public sealed class DescriptionRulesTests
{
    [TestMethod]
    [DataRow(99, true)]
    [DataRow(100, true)]
    [DataRow(101, false)]
    public void IsValid_EmojiDescription_RespectsUtf16StorageCapacity(int count, bool expected)
    {
        string description = string.Concat(Enumerable.Repeat("😀", count));

        Assert.AreEqual(expected, DescriptionRules.IsValid(description));
    }

    [TestMethod]
    public void IsValid_OptionalAndWhitespaceDescriptions_EnforcesRawLength()
    {
        Assert.IsTrue(DescriptionRules.IsValid(null));
        Assert.IsTrue(DescriptionRules.IsValid(string.Empty));
        Assert.IsTrue(DescriptionRules.IsValid(new string(' ', 200)));
        Assert.IsFalse(DescriptionRules.IsValid(new string(' ', 201)));
    }
}
