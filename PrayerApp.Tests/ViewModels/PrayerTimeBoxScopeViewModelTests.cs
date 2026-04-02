using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class PrayerTimeBoxScopeViewModelTests
{
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly IPrayerService _prayerService = Substitute.For<IPrayerService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    private PrayerTimeBoxScopeViewModel CreateSut() =>
        new(_boxService, _cardService, _prayerService, _navigationService);

    // ── LoadBoxesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task LoadBoxesAsync_ExcludesSystemAndArchivedBoxes()
    {
        var boxes = new List<CardBox>
        {
            new() { Id = 1, Name = "Family", IsSystem = false },
            new() { Id = 2, Name = "System", IsSystem = true, SystemKey = CardBox.SystemKeySystem },
            new() { Id = 3, Name = "Archived", IsSystem = true, SystemKey = CardBox.SystemKeyArchived },
            new() { Id = 4, Name = "Work", IsSystem = false }
        };
        _boxService.GetBoxesAsync().Returns(boxes.AsReadOnly());

        // Family has cards with prayers, Work has cards with prayers
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, BoxId = 1 },
            new() { Id = 20, BoxId = 4 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 },
            new() { Id = 200, PrayerCardId = 20 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        Assert.Equal(2, sut.Boxes.Count);
        Assert.Equal("Family", sut.Boxes[0].Box.Name);
        Assert.Equal("Work", sut.Boxes[1].Box.Name);
    }

    [Fact]
    public async Task LoadBoxesAsync_ExcludesBoxesWithNoActiveCards()
    {
        var boxes = new List<CardBox>
        {
            new() { Id = 1, Name = "Family", IsSystem = false },
            new() { Id = 2, Name = "Empty Collection", IsSystem = false }
        };
        _boxService.GetBoxesAsync().Returns(boxes.AsReadOnly());

        // Only Family has a card with an active prayer
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, BoxId = 1 },
            new() { Id = 20, BoxId = 2 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 }
            // No active prayers for card 20
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        Assert.Single(sut.Boxes);
        Assert.Equal("Family", sut.Boxes[0].Box.Name);
    }

    // ── Selection ─────────────────────────────────────────────────────

    [Fact]
    public async Task StartCommand_WithSelection_NavigatesToPrayerTimeWithBoxScope()
    {
        var boxes = new List<CardBox>
        {
            new() { Id = 5, Name = "Family", IsSystem = false }
        };
        _boxService.GetBoxesAsync().Returns(boxes.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, BoxId = 5 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        // Select the first box via RadioButton binding
        sut.Boxes[0].IsSelected = true;
        await ((IAsyncRelayCommand)sut.StartCommand).ExecuteAsync(null);

        await _navigationService.Received(1).PopModalAsync();
        await _navigationService.Received(1).GoToAsync($"{Routes.PrayerTimePage}?scope={Routes.ScopeBox}&boxId=5");
    }

    [Fact]
    public async Task StartCommand_NoneSelected_ShowsAlert()
    {
        var boxes = new List<CardBox>
        {
            new() { Id = 5, Name = "Family", IsSystem = false }
        };
        _boxService.GetBoxesAsync().Returns(boxes.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 10, BoxId = 5 }
        }.AsReadOnly());
        _prayerService.GetAllActivePrayersAsync().Returns(new List<Prayer>
        {
            new() { Id = 100, PrayerCardId = 10 }
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadBoxesAsync();

        // Don't select anything
        await ((IAsyncRelayCommand)sut.StartCommand).ExecuteAsync(null);

        await _navigationService.Received(1).DisplayAlertAsync(
            "No Collection Selected", "Please select a collection.", "OK");
        await _navigationService.DidNotReceive().PopModalAsync();
    }

    // ── Cancel ────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelCommand_PopsModal()
    {
        var sut = CreateSut();

        await sut.CancelAsync();

        await _navigationService.Received(1).PopModalAsync();
    }
}
