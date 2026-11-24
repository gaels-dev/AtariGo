using System.Text.Json;

namespace AtariGo.Diagnostics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class DiagnosticErrorTests
{
    [TestMethod]
    public void FromType_NonExceptionType_RejectsInput()
    {
        // Arrange
        Type type = typeof(string);
        // Act
        ArgumentException failure = Assert.ThrowsExactly<ArgumentException>(
            () => DiagnosticError.FromType(type));
        // Assert
        Assert.AreEqual("exceptionType", failure.ParamName);
    }

    [TestMethod]
    public void FromType_NullType_RejectsInput()
    {
        // Arrange
        Type type = null!;
        // Act
        ArgumentNullException failure = Assert.ThrowsExactly<ArgumentNullException>(
            () => DiagnosticError.FromType(type));
        // Assert
        Assert.AreEqual("exceptionType", failure.ParamName);
    }

    [TestMethod]
    public void FromException_DatabaseFailure_PreservesOnlyTechnicalCodes()
    {
        // Arrange
        var failure = new InvalidOperationException("Duplicate private value");
        // Act
        DiagnosticError error = DiagnosticError.FromException(failure, 2627);
        // Assert
        Assert.AreEqual(nameof(InvalidOperationException), error.TypeName);
        Assert.AreEqual(failure.HResult, error.HResult);
        Assert.AreEqual(2627, error.NativeCode);
    }

    [TestMethod]
    public void FromException_NoNativeCode_PreservesAbsence()
    {
        // Arrange
        var failure = new IOException("Synthetic failure");
        // Act
        DiagnosticError error = DiagnosticError.FromException(failure);
        // Assert
        Assert.IsNull(error.NativeCode);
    }

    [TestMethod]
    public void FromException_PrivateExceptionData_DoesNotRetainPayload()
    {
        // Arrange
        const string privateValue = "synthetic-private-value@example.invalid";
        var inner = new InvalidOperationException(privateValue);
        var failure = new IOException(privateValue, inner);
        failure.Data["Password"] = privateValue;
        failure.Source = privateValue;
        failure.HelpLink = privateValue;
        // Act
        DiagnosticError error = DiagnosticError.FromException(failure);
        string serialized = JsonSerializer.Serialize(error);
        // Assert
        Assert.DoesNotContain(privateValue, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("InnerException", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Message", serialized, StringComparison.Ordinal);
        Assert.AreEqual(nameof(IOException), error.TypeName);
    }

    [TestMethod]
    public void FromException_NullException_RejectsInput()
    {
        // Arrange
        Exception failure = null!;
        // Act
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => DiagnosticError.FromException(failure));
        // Assert
        Assert.AreEqual("exception", exception.ParamName);
    }
}
