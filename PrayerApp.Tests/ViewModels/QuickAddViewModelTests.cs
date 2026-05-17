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

    private static PrayerCard QuickAddCard() => new()
    {
        Id = 1,
        Title = PrayerCard.TitleQuickAdd,
        IsSystem = true,
        SystemKey = PrayerCard.SystemKeyQuickAdd,
        BoxId = 10,
    };

    private void SetupDestinationMocks()
    {
        var quickAdd = QuickAddCard();
        _cardService.GetOrCreateQuickAddCardAsync().Returns(quickAdd);
        _cardService.GetCardsAsync().Returns(new List<PrayerCard> { quickAdd }.AsReadOnly());
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 10, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem },
        }.AsReadOnly());
    }

    [Fact]
    public void Constructor_DefaultsToExistingCardMode()
    {
        var sut = CreateSut();

        Assert.True(sut.IsExistingCardMode);
        Assert.False(sut.IsNewCardMode);
    }

    [Fact]
    public async Task InitializeAsync_SelectsQuickAddCardByDefault()
    {
        SetupDestinationMocks();
        var sut = CreateSut();

        await sut.InitializeAsync();

        Assert.NotNull(sut.SelectedCard);
        Assert.Equal(1, sut.SelectedCard!.CardId);
        Assert.Equal(PrayerCard.TitleQuickAdd, sut.SelectedCard.Title);
        Assert.True(sut.ShowSelectedCardSummary);
        Assert.False(sut.ShowCardPickerList);
    }

    [Fact]
    public async Task SaveCommand_EmptyTitle_ShowsAlert()
    {
        SetupDestinationMocks();
        var sut = CreateSut();
        await sut.InitializeAsync();
        sut.Title = "   ";

        await sut.SaveCommand.ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync("Required", Arg.Any<string>(), "OK");
        await _prayerService.DidNotReceive().SavePrayerAsync(Arg.Any<Prayer>());
    }

    [Fact]
    public async Task SaveCommand_ExistingDefault_SavesToQuickAddCard()
    {
        SetupDestinationMocks();
        var sut = CreateSut();
        await sut.InitializeAsync();
        sut.Title = "Test prayer";

        await sut.SaveCommand.ExecuteAsync(null);

        await _prayerService.Received(1).SavePrayerAsync(
            Arg.Is<Prayer>(p => p.Title == "Test prayer" && p.PrayerCardId == 1));
        _prayerService.Received(1).InvalidateCache();
        _accessibilityService.Received(1).Announce("Prayer saved");
        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task SaveCommand_NewCardMode_CreatesCardThenPrayer()
    {
        SetupDestinationMocks();
        _cardService.SaveCardAsync(Arg.Any<PrayerCard>()).Returns(call =>
        {
            var card = call.Arg<PrayerCard>();
            card.Id = 42;
            return card;
        });
        var sut = CreateSut();
        await sut.InitializeAsync();
        sut.DestinationMode = ImportMode.NewCard;
        sut.CardTitle = "Family";
        sut.Title = "Test prayer";

        await sut.SaveCommand.ExecuteAsync(null);

        await _cardService.Received(1).SaveCardAsync(
            Arg.Is<PrayerCard>(c => c.Title == "Family" && c.BoxId == 0));
        await _prayerService.Received(1).SavePrayerAsync(
            Arg.Is<Prayer>(p => p.Title == "Test prayer" && p.PrayerCardId == 42));
    }

    [Fact]
    public async Task SaveCommand_ServiceThrows_ShowsError()
    {
        SetupDestinationMocks();
        var sut = CreateSut();
        await sut.InitializeAsync();
        sut.Title = "Test";
        _prayerService.SavePrayerAsync(Arg.Any<Prayer>())
            .Returns<Prayer>(_ => throw new Exception("DB error"));

        await sut.SaveCommand.ExecuteAsync(null);

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
