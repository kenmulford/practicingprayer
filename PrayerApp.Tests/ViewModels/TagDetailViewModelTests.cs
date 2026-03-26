using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class TagDetailViewModelTests
{
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly IUserColorService _userColorService = Substitute.For<IUserColorService>();
    private readonly IColorPickerService _colorPickerService = Substitute.For<IColorPickerService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly IDBService _db = Substitute.For<IDBService>();

    public TagDetailViewModelTests()
    {
        PrayerTag.SetDBService(_db);
        _userColorService.GetFirstDefaultHex().Returns("#B84040");
        _userColorService.GetColorsAsync().Returns(new List<UserColor>().AsReadOnly());
    }

    private TagDetailViewModel CreateSut() =>
        new(_tagService, _userColorService, _colorPickerService, _navigationService, _accessibilityService);

    // ── IsDirty ───────────────────────────────────────────────────────

    [Fact]
    public void IsDirty_InitialState_False()
    {
        var sut = CreateSut();
        Assert.False(sut.IsDirty);
    }

    [Fact]
    public void IsDirty_NameChanged_True()
    {
        var sut = CreateSut();
        sut.Name = "New Tag";
        Assert.True(sut.IsDirty);
    }

    [Fact]
    public void IsDirty_ColorChanged_True()
    {
        var sut = CreateSut();
        sut.SelectedColorHex = "#00FF00";
        Assert.True(sut.IsDirty);
    }

    // ── SaveAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_EmptyName_ShowsValidation()
    {
        var sut = CreateSut();
        sut.Name = "   ";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync("Validation", Arg.Any<string>(), "OK");
        await _tagService.DidNotReceive().SaveTagAsync(Arg.Any<PrayerTag>());
    }

    [Fact]
    public async Task SaveCommand_ValidName_SavesAndNavigates()
    {
        var sut = CreateSut();
        sut.Name = "Family";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _tagService.Received(1).SaveTagAsync(Arg.Any<PrayerTag>());
        _accessibilityService.Received(1).Announce("Tag saved");
        await _navigationService.Received(1).GoToAsync("..");
    }

    // ── CanLeaveAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CanLeaveAsync_NotDirty_ReturnsTrue()
    {
        var sut = CreateSut();
        Assert.True(await sut.CanLeaveAsync());
    }

    [Fact]
    public async Task CanLeaveAsync_Dirty_PromptsUser()
    {
        var sut = CreateSut();
        sut.Name = "Edited";
        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        var result = await sut.CanLeaveAsync();

        Assert.False(result);
    }

    // ── ViewPrayersCommand ────────────────────────────────────────────

    [Fact]
    public async Task ViewPrayersCommand_NavigatesToPrayersWithTagId()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.ViewPrayersCommand).ExecuteAsync(null);

        await _navigationService.Received(1).GoToAsync(Arg.Is<string>(s => s.Contains("PrayersPage")));
    }
}
