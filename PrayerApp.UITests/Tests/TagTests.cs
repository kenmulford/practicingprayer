using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// UAT Section 7: Tags
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Section", "7-Tags")]
public class TagTests
{
    private readonly AppiumSetup _setup;
    public TagTests(AppiumSetup setup) => _setup = setup;

    /// <summary>7.1: Tags list loads — all tags visible with color swatches.</summary>
    [Fact]
    public void Tags_PageLoads_ShowsTagList()
    {
        _setup.Driver.EnsureOnTab("Tags", _setup);
        Assert.True(_setup.Driver.IsDisplayed("Tags_List_Tags"),
            "Tag list should be visible on Tags page");
    }
}
