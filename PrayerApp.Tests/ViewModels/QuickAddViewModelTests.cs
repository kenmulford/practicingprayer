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
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();

    private QuickAddViewModel CreateSut() =>
        new(_cardService, _prayerService, _navigationService, _accessibilityService, _settings, _boxService);

    [Fact]
    public void Constructor_DefaultsToExistingCardMode()
    {
        var sut = CreateSut();

        Assert.True(sut.IsExistingCardMode);
        Assert.False(sut.IsNewCardMode);
    }

    [Fact]
    public async Task LoadDestinationAsync_SelectsQuickAddCardByDefault()
    {
        var quickAdd = new PrayerCard { Id = 5, Title = PrayerCard.TitleQuickAdd, IsSystem = true, BoxId = 10 };
        _cardService.GetOrCreateQuickAddCardAsync().Returns(quickAdd);
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { quickAdd }.AsReadOnly());
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem },
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadDestinationAsync();

        Assert.NotNull(sut.SelectedCard);
        Assert.Equal(5, sut.SelectedCard!.CardId);
        Assert.True(sut.SelectedCard.IsSelected);
    }

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
    public async Task SaveCommand_ValidTitle_SavesToQuickAddAndPopsModal()
    {
        var sut = CreateSut();
        sut.Title = "Test prayer";
        sut.SelectCardCommand.Execute(new CardPickerItem { CardId = 1, Title = "Quick Add" });

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _prayerService.Received(1).SavePrayerAsync(Arg.Is<Prayer>(p => p.Title == "Test prayer" && p.PrayerCardId == 1));
        _prayerService.Received(1).InvalidateCache();
        _accessibilityService.Received(1).Announce("Prayer saved");
        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task SaveCommand_ExistingMode_NonDefaultDestination_SavesToSelectedCard()
    {
        var sut = CreateSut();
        sut.Title = "Family prayer";
        sut.SelectCardCommand.Execute(new CardPickerItem { CardId = 42, Title = "Family" });

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _prayerService.Received(1).SavePrayerAsync(Arg.Is<Prayer>(p => p.PrayerCardId == 42));
    }

    [Fact]
    public async Task SaveCommand_NewCardMode_CreatesCardAndSavesPrayer()
    {
        _cardService.SaveCardAsync(Arg.Any<PrayerCard>()).Returns(call =>
        {
            var card = call.Arg<PrayerCard>();
            card.Id = 99;
            return card;
        });

        var sut = CreateSut();
        sut.SetNewCardModeCommand.Execute(null);
        sut.Title = "Morning prayer";
        sut.CardTitle = "New card";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _cardService.Received(1).SaveCardAsync(Arg.Is<PrayerCard>(c => c.Title == "New card" && c.BoxId == 0));
        await _prayerService.Received(1).SavePrayerAsync(Arg.Is<Prayer>(p => p.PrayerCardId == 99));
    }

    [Fact]
    public async Task SaveCommand_NewCardMode_EmptyCardTitle_ShowsAlert()
    {
        var sut = CreateSut();
        sut.SetNewCardModeCommand.Execute(null);
        sut.Title = "Test";
        sut.CardTitle = "  ";

        await ((IAsyncRelayCommand)sut.SaveCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync("Required", "Please enter a card title.", "OK");
        await _cardService.DidNotReceive().SaveCardAsync(Arg.Any<PrayerCard>());
    }

    [Fact]
    public async Task SaveCommand_ServiceThrows_ShowsError()
    {
        var sut = CreateSut();
        sut.Title = "Test";
        sut.SelectCardCommand.Execute(new CardPickerItem { CardId = 1, Title = "Quick Add" });
        _prayerService.SavePrayerAsync(Arg.Any<Prayer>()).Returns(_ => throw new Exception("DB error"));

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
