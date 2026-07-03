using HumanCron.Models;

namespace HumanCron.Tests.Models;

/// <summary>
/// Tests for the optional Exception carried by ParseResult&lt;T&gt;.Error.
/// </summary>
[TestFixture]
public class ParseResultExceptionTests
{
    [Test]
    public void Error_WithoutException_DefaultsExceptionToNull()
    {
        var result = new ParseResult<string>.Error("clean message");

        Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public void Error_WithException_PreservesSameInstanceAndCleanMessage()
    {
        var innerException = new InvalidOperationException("some internal detail");

        var result = new ParseResult<string>.Error("clean message", innerException);

        Assert.That(result.Exception, Is.SameAs(innerException));
        Assert.That(result.Message, Is.EqualTo("clean message"));
    }
}
