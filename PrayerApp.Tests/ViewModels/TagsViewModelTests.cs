using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class TagsViewModelTests
{
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();

    private TagsViewModel CreateSut() => new(_tagService, _navigationService, _accessibilityService);

    private static PrayerTag MakeTag(int id, string name = "Tag") =>
        new() { Id = id, Name = name };

    // ── LoadAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_PopulatesTagsFromService()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Faith"),
            MakeTag(2, "Family")
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Equal(2, sut.Tags.Count);
        Assert.Contains(sut.Tags, t => t.Name == "Faith");
        Assert.Contains(sut.Tags, t => t.Name == "Family");
    }

    [Fact]
    public async Task LoadAsync_EmptyService_ProducesEmptyCollection()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.Empty(sut.Tags);
    }

    [Fact]
    public async Task LoadAsync_SetsIsLoadingFalse_AfterCompletion()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());

        var sut = CreateSut();
        await sut.LoadAsync();

        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task LoadAsync_ReplacesExistingCollection()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "First Load")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(2, "Second Load")
        }.AsReadOnly());
        await sut.LoadAsync();

        Assert.Single(sut.Tags);
        Assert.Equal("Second Load", sut.Tags[0].Name);
    }

    // ── RefreshAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_AddsNewTags()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Existing")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Existing"),
            MakeTag(2, "New Tag")
        }.AsReadOnly());
        await sut.RefreshAsync();

        Assert.Equal(2, sut.Tags.Count);
        Assert.Contains(sut.Tags, t => t.Name == "New Tag");
    }

    [Fact]
    public async Task RefreshAsync_RemovesDeletedTags()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Keep"),
            MakeTag(2, "Delete Me")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Keep")
        }.AsReadOnly());
        await sut.RefreshAsync();

        Assert.Single(sut.Tags);
        Assert.Equal("Keep", sut.Tags[0].Name);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesExistingTagName()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Old Name")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "New Name")
        }.AsReadOnly());
        await sut.RefreshAsync();

        Assert.Single(sut.Tags);
        Assert.Equal("New Name", sut.Tags[0].Name);
    }

    [Fact]
    public async Task RefreshAsync_InvalidatesCache()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        await sut.RefreshAsync();

        _tagService.Received().InvalidateCache();
    }

    // ── Selection (BUG-7 inline chips) ──────────────────────────────

    [Fact]
    public async Task SelectCommand_TogglesIsSelected()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { MakeTag(1) }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();
        var item = sut.Tags[0];

        Assert.False(item.IsSelected);

        item.SelectCommand.Execute(null);
        Assert.True(item.IsSelected);

        item.SelectCommand.Execute(null);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public async Task SelectCommand_DeselectsOtherItems()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "A"), MakeTag(2, "B")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        sut.Tags[0].SelectCommand.Execute(null);
        Assert.True(sut.Tags[0].IsSelected);

        sut.Tags[1].SelectCommand.Execute(null);
        Assert.False(sut.Tags[0].IsSelected);
        Assert.True(sut.Tags[1].IsSelected);
    }

    [Fact]
    public async Task DeselectAll_DeselectsAllItems()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "A"), MakeTag(2, "B")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();

        sut.Tags[0].SelectCommand.Execute(null);
        sut.DeselectAll();

        Assert.False(sut.Tags[0].IsSelected);
        Assert.False(sut.Tags[1].IsSelected);
    }

    [Fact]
    public async Task ShowActions_TrueWhenSelected()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { MakeTag(1) }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();
        var item = sut.Tags[0];

        Assert.False(item.ShowActions);

        item.SelectCommand.Execute(null);
        Assert.True(item.ShowActions);
    }

    // ── RemoveTag ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveTag_RemovesItemFromCollection()
    {
        var tag = MakeTag(1, "Remove Me");
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { tag }.AsReadOnly());
        var sut = CreateSut();
        await sut.LoadAsync();
        var item = sut.Tags[0];

        sut.RemoveTag(item);

        Assert.Empty(sut.Tags);
    }
}
