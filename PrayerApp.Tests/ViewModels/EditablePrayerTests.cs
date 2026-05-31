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

    [Fact]
    public void UpdatePosition_SetsIndexedSemanticDescriptions()
    {
        var row = EditablePrayer.FromParsed("Pray for Mom", null);

        row.UpdatePosition(2, 3);

        Assert.Equal("Prayer title, item 2 of 3", row.TitleSemanticDescription);
        Assert.Equal("Prayer details, item 2 of 3", row.DetailsSemanticDescription);
        Assert.Equal("Remove prayer, item 2 of 3", row.RemoveSemanticDescription);
    }

    [Fact]
    public void UpdatePosition_SameValues_DoesNotRaisePropertyChanged()
    {
        var row = EditablePrayer.FromParsed("Pray for Mom", null);
        row.UpdatePosition(1, 2);
        var changed = false;
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditablePrayer.TitleSemanticDescription))
                changed = true;
        };

        row.UpdatePosition(1, 2);

        Assert.False(changed);
    }
}
