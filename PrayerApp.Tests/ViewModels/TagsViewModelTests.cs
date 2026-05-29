using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using PrayerApp.Messages;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class TagsViewModelTests
{
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();

    private TagsViewModel CreateSut() => new(_tagService, _navigationService, _accessibilityService, _messenger);

    private static PrayerTag MakeTag(int id, string name = "Tag") =>
        new() { Id = id, Name = name };

    // ── SyncAsync — full population ───────────────────────────────────

    [Fact]
    public async Task SyncAsync_PopulatesTagsFromService()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Faith"),
            MakeTag(2, "Family")
        }.AsReadOnly());

        var sut = CreateSut();
        await sut.SyncAsync();

        Assert.Equal(2, sut.Tags.Count);
        Assert.Contains(sut.Tags, t => t.Name == "Faith");
        Assert.Contains(sut.Tags, t => t.Name == "Family");
    }

    [Fact]
    public async Task SyncAsync_EmptyService_ProducesEmptyCollection()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());

        var sut = CreateSut();
        await sut.SyncAsync();

        Assert.Empty(sut.Tags);
    }

    [Fact]
    public async Task SyncAsync_SetsIsLoadingFalse_AfterCompletion()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());

        var sut = CreateSut();
        await sut.SyncAsync();

        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task SyncAsync_SecondCall_DiffsToNewState()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "First Load")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.SyncAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(2, "Second Load")
        }.AsReadOnly());
        await sut.SyncAsync();

        Assert.Single(sut.Tags);
        Assert.Equal("Second Load", sut.Tags[0].Name);
    }

    // ── SyncAsync — incremental diff ──────────────────────────────────

    [Fact]
    public async Task SyncAsync_AddsNewTags()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Existing")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.SyncAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Existing"),
            MakeTag(2, "New Tag")
        }.AsReadOnly());
        await sut.SyncAsync();

        Assert.Equal(2, sut.Tags.Count);
        Assert.Contains(sut.Tags, t => t.Name == "New Tag");
    }

    [Fact]
    public async Task SyncAsync_RemovesDeletedTags()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Keep"),
            MakeTag(2, "Tag To Delete")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.SyncAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Keep")
        }.AsReadOnly());
        await sut.SyncAsync();

        Assert.Single(sut.Tags);
        Assert.Equal("Keep", sut.Tags[0].Name);
    }

    [Fact]
    public async Task SyncAsync_UpdatesExistingTagName()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "Old Name")
        }.AsReadOnly());
        var sut = CreateSut();
        await sut.SyncAsync();

        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "New Name")
        }.AsReadOnly());
        await sut.SyncAsync();

        Assert.Single(sut.Tags);
        Assert.Equal("New Name", sut.Tags[0].Name);
    }

    [Fact]
    public async Task SyncAsync_DoesNotInvalidateServiceCache()
    {
        // Slice 3: VMs no longer call InvalidateCache. Services auto-invalidate on
        // mutation (Slice 2). The cache stays warm during sibling-page sync.
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>().AsReadOnly());
        var sut = CreateSut();

        await sut.SyncAsync();
        await sut.SyncAsync();

        _tagService.DidNotReceive().InvalidateCache();
    }

    // ── Messenger-driven sync ─────────────────────────────────────────

    [Fact]
    public async Task TagChangedMessage_TriggersSyncAsync()
    {
        var sut = CreateSut();
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "FromMessenger")
        }.AsReadOnly());

        _messenger.Send(new TagChangedMessage(1, ChangeKind.Created));

        // SyncAsync runs fire-and-forget from the registered handler — yield once
        // so the await chain completes before assertion.
        await Task.Yield();
        await Task.Yield();

        Assert.Contains(sut.Tags, t => t.Name == "FromMessenger");
    }

    [Fact]
    public async Task BulkChangedMessage_TriggersSyncAsync()
    {
        var sut = CreateSut();
        _tagService.GetTagsAsync().Returns(new List<PrayerTag>
        {
            MakeTag(1, "FromBulk")
        }.AsReadOnly());

        _messenger.Send(new BulkChangedMessage());

        await Task.Yield();
        await Task.Yield();

        Assert.Contains(sut.Tags, t => t.Name == "FromBulk");
    }

    // ── Selection (BUG-7 inline chips) ──────────────────────────────

    [Fact]
    public async Task SelectCommand_TogglesIsSelected()
    {
        _tagService.GetTagsAsync().Returns(new List<PrayerTag> { MakeTag(1) }.AsReadOnly());
        var sut = CreateSut();
        await sut.SyncAsync();
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
        await sut.SyncAsync();

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
        await sut.SyncAsync();

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
        await sut.SyncAsync();
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
        await sut.SyncAsync();
        var item = sut.Tags[0];

        sut.RemoveTag(item);

        Assert.Empty(sut.Tags);
    }

    // ── Slice 6a — single-flight + coalesce-pending SyncAsync ─────────

    [Fact]
    public async Task SyncAsync_BurstOfThreeConcurrent_CoalescesToTwoFetches()
    {
        // See PrayerCardsViewModelTests for full context. Same coalesce contract.
        var gate = new TaskCompletionSource<IReadOnlyList<PrayerTag>>();
        _tagService.GetTagsAsync().Returns(gate.Task);

        var sut = CreateSut();

        var t1 = sut.SyncAsync();
        var t2 = sut.SyncAsync();
        var t3 = sut.SyncAsync();

        gate.SetResult(new List<PrayerTag>().AsReadOnly());
        await Task.WhenAll(t1, t2, t3);

        await _tagService.Received(2).GetTagsAsync();
    }
}
