using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;
using SQLite;

namespace PrayerApp.Tests.ViewModels;

public class BoxDetailViewModelTests
{
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();

    public BoxDetailViewModelTests()
    {
        CardBox.SetDBService(Substitute.For<IDBService>());
    }

    private BoxDetailViewModel CreateSut() =>
        new(_boxService, _navigationService, _accessibilityService);

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
        sut.Name = "Changed";
        Assert.True(sut.IsDirty);
    }

    // ── SaveCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_EmptyName_ShowsValidation()
    {
        var sut = CreateSut();
        sut.Name = "  ";

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync(
            "Validation", Arg.Any<string>(), "OK");
        await _boxService.DidNotReceive().SaveBoxAsync(Arg.Any<CardBox>());
    }

    [Fact]
    public async Task SaveCommand_ValidName_SavesAndNavigates()
    {
        _boxService.SaveBoxAsync(Arg.Any<CardBox>()).Returns(ci => ci.Arg<CardBox>());
        var sut = CreateSut();
        sut.Name = "Family";

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _boxService.Received(1).SaveBoxAsync(Arg.Is<CardBox>(b => b.Name == "Family"));
        await _navigationService.Received(1).GoToAsync("..");
    }

    [Fact]
    public async Task SaveCommand_DuplicateName_ShowsAlert_DoesNotNavigate()
    {
        _boxService.SaveBoxAsync(Arg.Any<CardBox>()).Returns<CardBox>(_ =>
            throw SQLiteException.New(SQLite3.Result.Constraint,
                "UNIQUE constraint failed: CardBox.Name"));
        var sut = CreateSut();
        sut.Name = "Family";

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync(
            Arg.Is<string>(t => t.Contains("Duplicate")),
            Arg.Is<string>(m => m.Contains("Family")),
            "OK");
        await _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SaveCommand_ResetsDirtyState()
    {
        _boxService.SaveBoxAsync(Arg.Any<CardBox>()).Returns(ci => ci.Arg<CardBox>());
        var sut = CreateSut();
        sut.Name = "Test";

        await ((AsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        Assert.False(sut.IsDirty);
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
        _navigationService.DisplayConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        var sut = CreateSut();
        sut.Name = "Changed";

        var result = await sut.CanLeaveAsync();

        Assert.False(result);
        await _navigationService.Received(1).DisplayConfirmAsync(
            "Unsaved Changes", Arg.Any<string>(), "Discard", "Cancel");
    }

    // ── ApplyQueryAttributes ──────────────────────────────────────────

    [Fact]
    public async Task ApplyQueryAttributes_LoadsExistingBox()
    {
        var box = new CardBox { Id = 5, Name = "Family" };
        var db = Substitute.For<IDBService>();
        CardBox.SetDBService(db);
        db.GetByIdAsync<CardBox>(5).Returns(box);

        var sut = CreateSut();
        ((IQueryAttributable)sut).ApplyQueryAttributes(
            new Dictionary<string, object> { { "load", "5" } });

        // Give the async load time to complete
        await Task.Delay(100);

        Assert.Equal("Family", sut.Name);
        Assert.True(sut.IsExisting);
    }

    // ── IsSystem ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SystemBox_SetsIsSystemTrue()
    {
        var box = new CardBox { Id = 2, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem };
        var db = Substitute.For<IDBService>();
        CardBox.SetDBService(db);
        db.GetByIdAsync<CardBox>(2).Returns(box);

        var sut = CreateSut();
        ((IQueryAttributable)sut).ApplyQueryAttributes(
            new Dictionary<string, object> { { "load", "2" } });

        await Task.Delay(100);

        Assert.True(sut.IsSystem);
        Assert.False(sut.IsNameEditable);
    }
}
