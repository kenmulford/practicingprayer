using System.ComponentModel;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class EditablePrayerTests
{
    // ── Accessible descriptions (#15) ──────────────────────────

    [Fact]
    public void TitleAccessibleDescription_FoldsPositionAndTotal()
    {
        var row = new EditablePrayer { Position = 2, Total = 3 };

        Assert.Equal("Prayer title, item 2 of 3", row.TitleAccessibleDescription);
    }

    [Fact]
    public void DetailsAccessibleDescription_FoldsPositionAndTotal()
    {
        var row = new EditablePrayer { Position = 2, Total = 3 };

        Assert.Equal("Prayer details, item 2 of 3", row.DetailsAccessibleDescription);
    }

    [Fact]
    public void RemoveAccessibleDescription_FoldsPositionAndTotal()
    {
        var row = new EditablePrayer { Position = 2, Total = 3 };

        Assert.Equal("Remove prayer, item 2 of 3", row.RemoveAccessibleDescription);
    }

    [Fact]
    public void SettingPosition_RaisesPropertyChangedForAllThreeDescriptions()
    {
        var row = new EditablePrayer { Total = 3 };
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.Position = 2;

        Assert.Contains(nameof(EditablePrayer.TitleAccessibleDescription), raised);
        Assert.Contains(nameof(EditablePrayer.DetailsAccessibleDescription), raised);
        Assert.Contains(nameof(EditablePrayer.RemoveAccessibleDescription), raised);
    }

    [Fact]
    public void SettingTotal_RaisesPropertyChangedForAllThreeDescriptions()
    {
        var row = new EditablePrayer { Position = 1 };
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.Total = 4;

        Assert.Contains(nameof(EditablePrayer.TitleAccessibleDescription), raised);
        Assert.Contains(nameof(EditablePrayer.DetailsAccessibleDescription), raised);
        Assert.Contains(nameof(EditablePrayer.RemoveAccessibleDescription), raised);
    }
}
