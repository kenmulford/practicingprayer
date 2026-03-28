using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class QuickAddViewModelTests
{
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly ISettings _settings = Substitute.For<ISettings>();

    private QuickAddViewModel CreateSut() =>
        new(_cardService, _prayerService, _navigationService, _accessibilityService, _settings);

    [Fact]
    public void Constructor_SetsDefaultTitle()
    {
        var sut = CreateSut();
        Assert.Equal(string.Empty, sut.Title);
    }

    [Fact]
    public async Task SaveCommand_EmptyTitle_ShowsAlert()
    {
        var sut = CreateSut();
        sut.Title = "   ";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync("Required", Arg.Any<string>(), "OK");
        await _prayerService.DidNotReceive().SavePrayerAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task SaveCommand_ValidTitle_SavesAndPopsModal()
    {
        var sut = CreateSut();
        sut.Title = "Test prayer";
        _cardService.GetOrCreateQuickAddCardAsync().Returns(new PrayerCard { Id = 1 });

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _prayerService.Received(1).SavePrayerAsync(Arg.Is<Prayer>(p => p.Title == "Test prayer" && p.PrayerCardId == 1));
        _prayerService.Received(1).InvalidateCache();
        _accessibilityService.Received(1).Announce("Prayer saved");
        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task SaveCommand_ServiceThrows_ShowsError()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        _cardService.GetOrCreateQuickAddCardAsync().Returns<PrayerCard>(_ => throw new Exception("DB error"));

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync("Error", Arg.Any<string>(), "OK");
        await _navigationService.DidNotReceive().PopModalAsync();
    }

    [Fact]
    public async Task CancelCommand_PopsModal()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.CancelCommand).ExecuteAsync(null);

        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public void ShowTip_TipNotDismissed_ReturnsTrue()
    {
        _settings.QuickAddTipDismissed.Returns(false);
        var sut = CreateSut();

        Assert.True(sut.ShowTip);
    }

    [Fact]
    public void ShowTip_TipAlreadyDismissed_ReturnsFalse()
    {
        _settings.QuickAddTipDismissed.Returns(true);
        var sut = CreateSut();

        Assert.False(sut.ShowTip);
    }

    [Fact]
    public void DismissTipCommand_SetsShowTipFalseAndPersists()
    {
        _settings.QuickAddTipDismissed.Returns(false);
        var sut = CreateSut();
        Assert.True(sut.ShowTip);

        sut.DismissTipCommand.Execute(null);

        Assert.False(sut.ShowTip);
        _settings.Received().QuickAddTipDismissed = true;
    }
}
