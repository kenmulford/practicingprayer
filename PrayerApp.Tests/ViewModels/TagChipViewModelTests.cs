using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class TagChipViewModelTests
{
    [Fact]
    public void Constructor_SetsPropertiesFromTag()
    {
        var tag = new PrayerTag { Id = 5, Name = "Faith", Color = "#B84040" };

        var sut = new TagChipViewModel(tag, _ => Task.CompletedTask);

        Assert.Equal(5, sut.Id);
        Assert.Equal("Faith", sut.Name);
        Assert.NotNull(sut.ChipColor);
    }

    [Fact]
    public void Constructor_NullName_FallsBackToModelDefault()
    {
        var tag = new PrayerTag { Id = 1, Name = null! };

        var sut = new TagChipViewModel(tag, _ => Task.CompletedTask);

        // PrayerTag.Name setter converts null → "Unnamed Tag"
        Assert.Equal("Unnamed Tag", sut.Name);
    }

    [Fact]
    public void Constructor_NullColor_ResolvesToDefault()
    {
        var tag = new PrayerTag { Id = 1, Name = "Test", Color = null };

        var sut = new TagChipViewModel(tag, _ => Task.CompletedTask);

        Assert.NotNull(sut.ChipColor);
    }

    [Fact]
    public async Task RemoveCommand_InvokesCallbackWithId()
    {
        int removedId = -1;
        var tag = new PrayerTag { Id = 42, Name = "Remove Me" };
        var sut = new TagChipViewModel(tag, id =>
        {
            removedId = id;
            return Task.CompletedTask;
        });

        await ((IAsyncRelayCommand)sut.RemoveCommand).ExecuteAsync(null);

        Assert.Equal(42, removedId);
    }
}
