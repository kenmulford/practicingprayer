using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class EditablePrayerTests
{
    [Fact]
    public void UpdatePosition_SetsIndexedSemanticDescriptions()
    {
        var row = new EditablePrayer();

        row.UpdatePosition(2, 3);

        Assert.Equal("Prayer title, item 2 of 3", row.TitleSemanticDescription);
        Assert.Equal("Prayer details, item 2 of 3", row.DetailsSemanticDescription);
        Assert.Equal("Remove prayer, item 2 of 3", row.RemoveSemanticDescription);
    }

    [Fact]
    public void UpdatePosition_SameValues_DoesNotRaisePropertyChanged()
    {
        var row = new EditablePrayer();
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
