using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;
using SQLite;

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

    [Fact]
    public async Task SaveCommand_DuplicateName_ShowsAlert_DoesNotNavigate()
    {
        _tagService.SaveTagAsync(Arg.Any<PrayerTag>()).Returns<PrayerTag>(_ =>
            throw SQLiteException.New(SQLite3.Result.Constraint,
                "UNIQUE constraint failed: PrayerTag.Name"));
        var sut = CreateSut();
        sut.Name = "Family";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync(
            Arg.Is<string>(t => t.Contains("Duplicate")),
            Arg.Is<string>(m => m.Contains("Family")),
            "OK");
        await _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>());
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

    // ── IsBusy on Save ─────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_SetsIsBusyDuringExecution_AndResetsAfter()
    {
        var sut = CreateSut();
        sut.Name = "Test";
        bool capturedIsBusy = false;
        _tagService.SaveTagAsync(Arg.Any<PrayerTag>()).Returns(call =>
        {
            capturedIsBusy = sut.IsBusy;
            return Task.FromResult((PrayerTag)call[0]);
        });

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        Assert.True(capturedIsBusy);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_ResetsIsBusyAfterException()
    {
        var sut = CreateSut();
        sut.Name = "Test";
        _tagService.SaveTagAsync(Arg.Any<PrayerTag>())
            .Returns(_ => Task.FromException<PrayerTag>(new InvalidOperationException("boom")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null));

        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_DoubleInvoke_RunsServiceOnlyOnce()
    {
        var sut = CreateSut();
        sut.Name = "Test";
        var tcs = new TaskCompletionSource<PrayerTag>();
        _tagService.SaveTagAsync(Arg.Any<PrayerTag>()).Returns(tcs.Task);

        var first = ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);
        var second = ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);
        tcs.SetResult(new PrayerTag());
        await Task.WhenAll(first, second);

        await _tagService.Received(1).SaveTagAsync(Arg.Any<PrayerTag>());
    }
}
