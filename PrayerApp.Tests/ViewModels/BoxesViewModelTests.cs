using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class BoxesViewModelTests
{
    private readonly IBoxService _boxService = Substitute.For<IBoxService>();
    private readonly ICardService _cardService = Substitute.For<ICardService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();

    public BoxesViewModelTests()
    {
        CardBox.SetDBService(Substitute.For<IDBService>());
    }

    private BoxesViewModel CreateSut() =>
        new(_boxService, _cardService, _navigationService, _accessibilityService);

    private void SetupBoxes(params CardBox[] boxes)
    {
        _boxService.GetBoxesAsync().Returns(boxes.ToList().AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
    }

    // ── LoadAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_PopulatesBoxesFromService()
    {
        SetupBoxes(
            new CardBox { Id = 1, Name = "Family" },
            new CardBox { Id = 2, Name = "Ministry" });

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.Boxes.Count);
        Assert.Contains(sut.Boxes, b => b.Name == "Family");
        Assert.Contains(sut.Boxes, b => b.Name == "Ministry");
    }

    [Fact]
    public async Task LoadAsync_EmptyService_ProducesEmptyCollection()
    {
        SetupBoxes();

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Empty(sut.Boxes);
    }

    [Fact]
    public async Task LoadAsync_SetsIsLoadingFalse_AfterCompletion()
    {
        SetupBoxes();

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task LoadAsync_CalculatesCardCounts()
    {
        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 5, Name = "Family" }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>
        {
            new() { Id = 1, Title = "Card A", BoxId = 5 },
            new() { Id = 2, Title = "Card B", BoxId = 5 },
            new() { Id = 3, Title = "Card C", BoxId = 0 } // different box
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.Boxes[0].CardCount);
    }

    // ── RefreshAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_AddsNewBoxes()
    {
        SetupBoxes(new CardBox { Id = 1, Name = "Existing" });
        var sut = CreateSut();
        await sut.LoadAsync();

        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "Existing" },
            new() { Id = 2, Name = "New Box" }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        await sut.RefreshAsync();

        Assert.Equal(2, sut.Boxes.Count);
        Assert.Contains(sut.Boxes, b => b.Name == "New Box");
    }

    [Fact]
    public async Task RefreshAsync_RemovesDeletedBoxes()
    {
        SetupBoxes(
            new CardBox { Id = 1, Name = "Keep" },
            new CardBox { Id = 2, Name = "Delete Me" });
        var sut = CreateSut();
        await sut.LoadAsync();

        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "Keep" }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        await sut.RefreshAsync();

        Assert.Single(sut.Boxes);
        Assert.Equal("Keep", sut.Boxes[0].Name);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesExistingBoxName()
    {
        SetupBoxes(new CardBox { Id = 1, Name = "Old Name" });
        var sut = CreateSut();
        await sut.LoadAsync();

        _boxService.GetBoxesAsync().Returns(new List<CardBox>
        {
            new() { Id = 1, Name = "New Name" }
        }.AsReadOnly());
        _cardService.GetCardsAsync().Returns(new List<PrayerCard>().AsReadOnly());
        await sut.RefreshAsync();

        Assert.Equal("New Name", sut.Boxes[0].Name);
    }

    // ── Selection ─────────────────────────────────────────────────────

    [Fact]
    public async Task SelectCommand_TogglesIsSelected()
    {
        SetupBoxes(new CardBox { Id = 1, Name = "Test" });
        var sut = CreateSut();
        await sut.LoadAsync();
        var item = sut.Boxes[0];

        Assert.False(item.IsSelected);
        item.SelectCommand.Execute(null);
        Assert.True(item.IsSelected);
        item.SelectCommand.Execute(null);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public async Task SelectCommand_DeselectsOtherItems()
    {
        SetupBoxes(
            new CardBox { Id = 1, Name = "A" },
            new CardBox { Id = 2, Name = "B" });
        var sut = CreateSut();
        await sut.LoadAsync();

        sut.Boxes[0].SelectCommand.Execute(null);
        Assert.True(sut.Boxes[0].IsSelected);

        sut.Boxes[1].SelectCommand.Execute(null);
        Assert.False(sut.Boxes[0].IsSelected);
        Assert.True(sut.Boxes[1].IsSelected);
    }

    [Fact]
    public async Task DeselectAll_DeselectsAllItems()
    {
        SetupBoxes(new CardBox { Id = 1, Name = "A" });
        var sut = CreateSut();
        await sut.LoadAsync();

        sut.Boxes[0].SelectCommand.Execute(null);
        sut.DeselectAll();

        Assert.False(sut.Boxes[0].IsSelected);
    }

    // ── Delete ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommand_Unassign_CallsDeleteBoxAsyncWithFalse()
    {
        SetupBoxes(new CardBox { Id = 5, Name = "Family" });
        var sut = CreateSut();
        await sut.LoadAsync();

        _navigationService.DisplayActionSheetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string[]>())
            .Returns(BoxItemViewModel.ActionUnassign);

        await ((AsyncRelayCommand)sut.Boxes[0].DeleteCommand).ExecuteAsync(null);

        await _boxService.Received(1).DeleteBoxAsync(5, false);
    }

    [Fact]
    public async Task DeleteCommand_DeleteAll_CallsDeleteBoxAsyncWithTrue()
    {
        SetupBoxes(new CardBox { Id = 5, Name = "Family" });
        var sut = CreateSut();
        await sut.LoadAsync();

        _navigationService.DisplayActionSheetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string[]>())
            .Returns(BoxItemViewModel.ActionDeleteAll);

        await ((AsyncRelayCommand)sut.Boxes[0].DeleteCommand).ExecuteAsync(null);

        await _boxService.Received(1).DeleteBoxAsync(5, true);
    }

    [Fact]
    public async Task DeleteCommand_Cancel_DoesNotDelete()
    {
        SetupBoxes(new CardBox { Id = 5, Name = "Family" });
        var sut = CreateSut();
        await sut.LoadAsync();

        _navigationService.DisplayActionSheetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string[]>())
            .Returns("Cancel");

        await ((AsyncRelayCommand)sut.Boxes[0].DeleteCommand).ExecuteAsync(null);

        await _boxService.DidNotReceive().DeleteBoxAsync(Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task DeleteCommand_BackdropDismiss_DoesNotDelete()
    {
        SetupBoxes(new CardBox { Id = 5, Name = "Family" });
        var sut = CreateSut();
        await sut.LoadAsync();

        // Backdrop tap returns null on both Android and iOS
        _navigationService.DisplayActionSheetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string[]>())
            .Returns((string?)null);

        await ((AsyncRelayCommand)sut.Boxes[0].DeleteCommand).ExecuteAsync(null);

        await _boxService.DidNotReceive().DeleteBoxAsync(Arg.Any<int>(), Arg.Any<bool>());
    }

    // ── RemoveBox ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveBox_RemovesFromCollection()
    {
        SetupBoxes(new CardBox { Id = 1, Name = "Remove Me" });
        var sut = CreateSut();
        await sut.LoadAsync();
        var item = sut.Boxes[0];

        sut.RemoveBox(item);

        Assert.Empty(sut.Boxes);
    }
}
