using FolderSwitch.App.Startup;

namespace FolderSwitch.App.Tests;

/// <summary>Which update feed addresses FolderSwitch accepts: HTTPS, or plain HTTP on this machine only.</summary>
public class UpdateFeedTests
{
    [Theory]
    [InlineData("https://example.org/folderswitch/")]
    [InlineData("HTTPS://EXAMPLE.ORG/folderswitch")]
    [InlineData("https://example.org:8443/releases/")]
    [InlineData("  https://example.org/folderswitch/  ")]
    public void HTTPS_addresses_are_accepted(string url)
    {
        Assert.True(UpdateChecker.IsAcceptedFeedUrl(url));
    }

    [Theory]
    [InlineData("http://localhost:8000/")]
    [InlineData("http://LOCALHOST/")]
    [InlineData("http://127.0.0.1:8000/")]
    [InlineData("http://[::1]:8000/")]
    public void Plain_HTTP_on_this_machine_is_accepted_for_the_manual_update_test(string url)
    {
        Assert.True(UpdateChecker.IsAcceptedFeedUrl(url));
    }

    [Theory]
    [InlineData("http://example.org/folderswitch/")]
    [InlineData("http://192.168.1.10:8000/")]
    [InlineData("http://localhost.example.org/")]
    [InlineData("ftp://example.org/folderswitch/")]
    [InlineData("file:///C:/releases/")]
    [InlineData(@"\\server\releases")]
    [InlineData("example.org/folderswitch")]
    [InlineData("")]
    [InlineData("   ")]
    public void Any_other_address_is_refused(string url)
    {
        Assert.False(UpdateChecker.IsAcceptedFeedUrl(url));
    }

    [Fact]
    public void The_shipped_address_is_empty_or_accepted()
    {
        // Empty: updates are off. Otherwise it must be one FolderSwitch would use.
        Assert.True(UpdateFeed.Url.Length == 0 || UpdateChecker.IsAcceptedFeedUrl(UpdateFeed.Url));
    }
}
