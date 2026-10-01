using Vault.Core.Validation;
using Xunit;

namespace Vault.Tests;

public class ValidationTests
{
    [Fact]
    public void RequiredText_RejectsNullEmptyAndTooLong()
    {
        Assert.Throws<ValidationException>(() => InputValidator.RequiredText(null, "Label", 10));
        Assert.Throws<ValidationException>(() => InputValidator.RequiredText("   ", "Label", 10));
        Assert.Throws<ValidationException>(() => InputValidator.RequiredText(new string('a', 11), "Label", 10));
        Assert.Equal("ok", InputValidator.RequiredText(" ok ", "Label", 10));
    }

    [Fact]
    public void OptionalUrl_RequiresAbsoluteHttpOrHttps()
    {
        Assert.Null(InputValidator.OptionalUrl(null));
        Assert.Null(InputValidator.OptionalUrl("  "));
        Assert.Equal("https://x.internal", InputValidator.OptionalUrl("https://x.internal"));
        Assert.Throws<ValidationException>(() => InputValidator.OptionalUrl("not-a-url"));
        Assert.Throws<ValidationException>(() => InputValidator.OptionalUrl("ftp://files"));
        Assert.Throws<ValidationException>(() => InputValidator.OptionalUrl("javascript:alert(1)"));
    }

    [Fact]
    public void Sid_MustMatchWindowsSidFormat()
    {
        Assert.Equal("S-1-5-21-100-200-300-1001",
            InputValidator.RequiredSid("S-1-5-21-100-200-300-1001"));
        Assert.Throws<ValidationException>(() => InputValidator.RequiredSid("not-a-sid"));
        Assert.Throws<ValidationException>(() => InputValidator.RequiredSid("CORP\\user"));
    }

    [Fact]
    public void Password_Required_And_Bounded()
    {
        Assert.Throws<ValidationException>(() => InputValidator.RequiredPassword(null));
        Assert.Throws<ValidationException>(() => InputValidator.RequiredPassword(""));
        Assert.Throws<ValidationException>(
            () => InputValidator.RequiredPassword(new string('a', InputValidator.MaxPasswordLength + 1)));
        Assert.Equal("fine", InputValidator.RequiredPassword("fine"));
    }

    [Fact]
    public void Tags_AreNormalizedAndBounded()
    {
        var tags = InputValidator.NormalizeTags(new[] { " Prod ", "prod", "sql", "" });
        Assert.Equal(2, tags.Count);
        Assert.Throws<ValidationException>(
            () => InputValidator.NormalizeTags(new[] { new string('t', 100) }));
    }
}
