using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.UnitTests;

public sealed class DisplayNamesTests
{
    [Theory]
    [InlineData("rokaskamble39@gmail.com", null, "Rokaskamble")]
    [InlineData("anna.k_smith+hikes@x.io", null, "Anna K Smith")]
    [InlineData("123@x.io", null, "Hiker")]
    [InlineData("Ben", null, "Ben")]
    [InlineData("  Ben Ito  ", "ben@x.io", "Ben Ito")]
    [InlineData(null, "cleo-m.2@x.io", "Cleo M")]
    [InlineData("   ", "dan_99@x.io", "Dan")]
    [InlineData(null, null, "Hiker")]
    [InlineData("", "  ", "Hiker")]
    [InlineData("eva.lynn@x.io", "other@x.io", "Eva Lynn")]
    public void Names_are_kept_and_emails_are_made_readable(string? name, string? email, string expected) =>
        Assert.Equal(expected, DisplayNames.Friendly(name, email));

    [Fact]
    public void Long_names_are_capped()
    {
        Assert.Equal(DisplayNames.MaxLength, DisplayNames.Friendly(new string('a', 200)).Length);
        Assert.True(DisplayNames.Friendly(string.Join('.', Enumerable.Repeat("walker", 30)) + "@x.io").Length <= DisplayNames.MaxLength);
    }

    [Theory]
    [InlineData("a@b.c", true)]
    [InlineData("Ann", false)]
    [InlineData("@handle", false)]
    [InlineData("ann@", false)]
    [InlineData("Ann @ the hut", false)]
    [InlineData(null, false)]
    public void Recognises_emails(string? value, bool expected) =>
        Assert.Equal(expected, DisplayNames.IsEmail(value));
}
