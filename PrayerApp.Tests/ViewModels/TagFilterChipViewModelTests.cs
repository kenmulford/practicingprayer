using PrayerApp.Models;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class TagFilterChipViewModelTests
{
    [Fact]
    public void Constructor_SetsTag()
    {
        var tag = new PrayerTag { Id = 3, Name = "Family" };

        var sut = new TagFilterChipViewModel(tag, _ => { });

        Assert.Same(tag, sut.Tag);
    }

    [Fact]
    public void IsSelected_DefaultsFalse()
    {
        var tag = new PrayerTag { Id = 1, Name = "Test" };
        var sut = new TagFilterChipViewModel(tag, _ => { });

        Assert.False(sut.IsSelected);
    }

    [Fact]
    public void ToggleCommand_TogglesIsSelected()
    {
        var tag = new PrayerTag { Id = 1, Name = "Test" };
        var sut = new TagFilterChipViewModel(tag, _ => { });

        sut.ToggleCommand.Execute(null);
        Assert.True(sut.IsSelected);

        sut.ToggleCommand.Execute(null);
        Assert.False(sut.IsSelected);
    }

    [Fact]
    public void ToggleCommand_InvokesCallback()
    {
        TagFilterChipViewModel? callbackArg = null;
        var tag = new PrayerTag { Id = 1, Name = "Test" };
        var sut = new TagFilterChipViewModel(tag, vm => callbackArg = vm);

        sut.ToggleCommand.Execute(null);

        Assert.Same(sut, callbackArg);
    }

    [Fact]
    public void IsSelected_RaisesPropertyChanged()
    {
        var tag = new PrayerTag { Id = 1, Name = "Test" };
        var sut = new TagFilterChipViewModel(tag, _ => { });
        var raised = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TagFilterChipViewModel.IsSelected))
                raised = true;
        };

        sut.ToggleCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ToggleCommand_CallbackReceivesUpdatedState()
    {
        bool selectedInCallback = false;
        var tag = new PrayerTag { Id = 1, Name = "Test" };
        var sut = new TagFilterChipViewModel(tag, vm => selectedInCallback = vm.IsSelected);

        sut.ToggleCommand.Execute(null);

        Assert.True(selectedInCallback);
    }

    [Fact]
    public void AccessibleDescription_UnselectedByDefault_ReadsNotSelected()
    {
        var tag = new PrayerTag { Id = 1, Name = "Family" };
        var sut = new TagFilterChipViewModel(tag, _ => { });

        Assert.Equal("Family, not selected", sut.AccessibleDescription);
    }

    [Fact]
    public void AccessibleDescription_WhenSelected_ReadsSelected()
    {
        var tag = new PrayerTag { Id = 1, Name = "Urgent" };
        var sut = new TagFilterChipViewModel(tag, _ => { });

        sut.ToggleCommand.Execute(null);

        Assert.Equal("Urgent, selected", sut.AccessibleDescription);
    }

    [Fact]
    public void AccessibleDescription_FiresPropertyChanged_OnIsSelectedChange()
    {
        var tag = new PrayerTag { Id = 1, Name = "Test" };
        var sut = new TagFilterChipViewModel(tag, _ => { });
        var raised = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TagFilterChipViewModel.AccessibleDescription))
                raised = true;
        };

        sut.ToggleCommand.Execute(null);

        Assert.True(raised);
    }
}
