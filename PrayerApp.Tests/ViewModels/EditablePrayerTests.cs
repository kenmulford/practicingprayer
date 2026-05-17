using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class EditablePrayerTests
{
    [Fact]
    public void FromParsed_EmptyDetails_StartsCollapsedWithLink()
    {
        var row = EditablePrayer.FromParsed("Pray for Mom", null);

        Assert.False(row.IsDetailsExpanded);
        Assert.True(row.ShowDetailsLink);
    }

    [Fact]
    public void FromParsed_NonEmptyDetails_StartsExpandedWithoutLink()
    {
        var row = EditablePrayer.FromParsed(
            "Sis is graduating",
            "please pray for her ceremony this weekend");

        Assert.True(row.IsDetailsExpanded);
        Assert.False(row.ShowDetailsLink);
    }

    [Fact]
    public void ExpandDetailsCommand_RevealsEditor()
    {
        var row = EditablePrayer.FromParsed("Pray for Dad", "   ");

        row.ExpandDetailsCommand.Execute(null);

        Assert.True(row.IsDetailsExpanded);
        Assert.False(row.ShowDetailsLink);
    }

    [Fact]
    public void NewRow_DefaultsToCollapsedDetails()
    {
        var row = new EditablePrayer();

        Assert.False(row.IsDetailsExpanded);
        Assert.True(row.ShowDetailsLink);
    }
}
