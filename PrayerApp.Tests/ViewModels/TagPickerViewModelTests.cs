using CommunityToolkit.Mvvm.Input;
using NSubstitute;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class TagPickerViewModelTests
{
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();
    private readonly IDBService _db = Substitute.For<IDBService>();

    public TagPickerViewModelTests()
    {
        PrayerTag.SetDBService(_db);
        PrayerCardTag.SetDBService(_db);
    }

    private TagPickerViewModel CreateSut() =>
        new(_tagService, _navigationService, _accessibilityService);

    private static List<PrayerTag> MakeTags() =>
    [
        new() { Id = 1, Name = "Worship" },
        new() { Id = 2, Name = "Healing" },
        new() { Id = 3, Name = "Family" },
        new() { Id = 4, Name = "Work" },
        new() { Id = 5, Name = "Recently Notified", IsSystem = true },
    ];

    // ── Initialize ───────────────────────────────────────────────────

    [Fact]
    public void Initialize_PopulatesSelectedTags()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), [1, 3]);

        Assert.Equal(2, sut.SelectedTags.Count);
        Assert.Contains(sut.SelectedTags, t => t.Name == "Family");
        Assert.Contains(sut.SelectedTags, t => t.Name == "Worship");
    }

    [Fact]
    public void Initialize_EmptySelection_NoTags()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);

        Assert.Empty(sut.SelectedTags);
        Assert.False(sut.HasTags);
    }

    // ── TagSearchText filters suggestions ────────────────────────────

    [Fact]
    public void TagSearchText_FiltersSuggestions()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), []);

        sut.TagSearchText = "Wor";

        Assert.Equal(2, sut.SuggestedTags.Count);
        Assert.Contains(sut.SuggestedTags, t => t.Name == "Worship");
        Assert.Contains(sut.SuggestedTags, t => t.Name == "Work");
    }

    [Fact]
    public void TagSearchText_Empty_ClearsSuggestions()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), []);

        sut.TagSearchText = "Wor";
        Assert.True(sut.HasSuggestions);

        sut.TagSearchText = "";
        Assert.False(sut.HasSuggestions);
    }

    [Fact]
    public void TagSearchText_ExcludesAlreadySelected()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), [1]); // Worship already selected

        sut.TagSearchText = "Wor";

        Assert.Single(sut.SuggestedTags);
        Assert.Equal("Work", sut.SuggestedTags[0].Name);
    }

    [Fact]
    public void TagSearchText_ExcludesSystemTags()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), []);

        sut.TagSearchText = "Recent";

        Assert.Empty(sut.SuggestedTags);
    }

    [Fact]
    public void TagSearchText_CaseInsensitive()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), []);

        sut.TagSearchText = "worship";

        Assert.Single(sut.SuggestedTags);
        Assert.Equal("Worship", sut.SuggestedTags[0].Name);
    }

    // ── AddSuggestedTagCommand ───────────────────────────────────────

    [Fact]
    public async Task AddSuggestedTagCommand_AddsToSelected()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), []);
        sut.TagSearchText = "Wor";

        await ((IAsyncRelayCommand<int>)sut.AddSuggestedTagCommand).ExecuteAsync(1);

        Assert.Single(sut.SelectedTags);
        Assert.Equal("Worship", sut.SelectedTags[0].Name);
        Assert.Equal(string.Empty, sut.TagSearchText);
    }

    [Fact]
    public async Task AddSuggestedTagCommand_ClearsSuggestions()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), []);
        sut.TagSearchText = "Wor";
        Assert.True(sut.HasSuggestions);

        await ((IAsyncRelayCommand<int>)sut.AddSuggestedTagCommand).ExecuteAsync(1);

        Assert.False(sut.HasSuggestions); // search text cleared → suggestions cleared
    }

    [Fact]
    public async Task AddSuggestedTagCommand_Duplicate_NoOp()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), [1]); // Worship already selected

        await ((IAsyncRelayCommand<int>)sut.AddSuggestedTagCommand).ExecuteAsync(1);

        Assert.Single(sut.SelectedTags); // still just one
    }

    [Fact]
    public async Task AddSuggestedTagCommand_SavedPrayer_PersistsImmediately()
    {
        var sut = CreateSut();
        sut.Initialize(42, MakeTags(), []); // prayerId = 42 (saved)

        await ((IAsyncRelayCommand<int>)sut.AddSuggestedTagCommand).ExecuteAsync(1);

        await _tagService.Received(1).AddTagToRequestAsync(42, 1);
    }

    [Fact]
    public async Task AddSuggestedTagCommand_NewPrayer_StagesLocally()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []); // prayerId = 0 (unsaved)

        await ((IAsyncRelayCommand<int>)sut.AddSuggestedTagCommand).ExecuteAsync(1);

        await _tagService.DidNotReceive().AddTagToRequestAsync(Arg.Any<int>(), Arg.Any<int>());
        Assert.Single(sut.SelectedTags);
    }

    [Fact]
    public async Task AddSuggestedTagCommand_Announces()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);

        await ((IAsyncRelayCommand<int>)sut.AddSuggestedTagCommand).ExecuteAsync(1);

        _accessibilityService.Received(1).Announce("Added tag Worship");
    }

    // ── RemoveTag ────────────────────────────────────────────────────

    [Fact]
    public void RemoveTag_RemovesFromSelected()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), [1, 2]);

        // Execute remove via the chip's RemoveCommand
        sut.SelectedTags.First(t => t.Id == 1).RemoveCommand.Execute(null);

        // Allow async to complete
        Thread.Sleep(100);

        Assert.Single(sut.SelectedTags);
        Assert.Equal("Healing", sut.SelectedTags[0].Name);
    }

    [Fact]
    public async Task RemoveTag_SavedPrayer_PersistsRemoval()
    {
        var sut = CreateSut();
        sut.Initialize(42, MakeTags(), [1]);

        sut.SelectedTags[0].RemoveCommand.Execute(null);
        await Task.Delay(100); // let async complete

        await _tagService.Received(1).RemoveTagFromRequestAsync(42, 1);
    }

    // ── SubmitTagEntryCommand ────────────────────────────────────────

    [Fact]
    public async Task SubmitTagEntryCommand_ExactMatch_AddsExistingTag()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);
        sut.TagSearchText = "Worship";

        await ((IAsyncRelayCommand)sut.SubmitTagEntryCommand).ExecuteAsync(null);

        Assert.Single(sut.SelectedTags);
        Assert.Equal("Worship", sut.SelectedTags[0].Name);
        await _tagService.DidNotReceive().SaveTagAsync(Arg.Any<PrayerTag>());
    }

    [Fact]
    public async Task SubmitTagEntryCommand_NoMatch_CreatesNewTag()
    {
        var newTag = new PrayerTag { Id = 99, Name = "Gratitude" };
        _tagService.SaveTagAsync(Arg.Any<PrayerTag>()).Returns(newTag);

        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);
        sut.TagSearchText = "Gratitude";

        await ((IAsyncRelayCommand)sut.SubmitTagEntryCommand).ExecuteAsync(null);

        await _tagService.Received(1).SaveTagAsync(Arg.Is<PrayerTag>(t => t.Name == "Gratitude"));
        Assert.Single(sut.SelectedTags);
        Assert.Equal("Gratitude", sut.SelectedTags[0].Name);
    }

    [Fact]
    public async Task SubmitTagEntryCommand_Empty_NoOp()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);
        sut.TagSearchText = "   ";

        await ((IAsyncRelayCommand)sut.SubmitTagEntryCommand).ExecuteAsync(null);

        Assert.Empty(sut.SelectedTags);
    }

    // ── Comma auto-save ──────────────────────────────────────────────

    [Fact]
    public async Task CommaAutoSave_SingleTag_AddsAndClears()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);

        sut.TagSearchText = "Worship,";
        await Task.Delay(200); // let async ProcessCommaInputAsync complete

        Assert.Single(sut.SelectedTags);
        Assert.Equal("Worship", sut.SelectedTags[0].Name);
        Assert.Equal(string.Empty, sut.TagSearchText);
    }

    [Fact]
    public async Task CommaAutoSave_MultipleTags_AddsBoth()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);

        sut.TagSearchText = "Worship, Healing,";
        await Task.Delay(200);

        Assert.Equal(2, sut.SelectedTags.Count);
        Assert.Contains(sut.SelectedTags, t => t.Name == "Worship");
        Assert.Contains(sut.SelectedTags, t => t.Name == "Healing");
    }

    [Fact]
    public async Task CommaAutoSave_WithSpaces_TrimsCorrectly()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);

        sut.TagSearchText = "  Worship , ";
        await Task.Delay(200);

        Assert.Single(sut.SelectedTags);
        Assert.Equal("Worship", sut.SelectedTags[0].Name);
    }

    [Fact]
    public async Task CommaAutoSave_NewTag_CreatesViaService()
    {
        var newTag = new PrayerTag { Id = 99, Name = "NewTag" };
        _tagService.SaveTagAsync(Arg.Any<PrayerTag>()).Returns(newTag);

        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);

        sut.TagSearchText = "NewTag,";
        await Task.Delay(200);

        await _tagService.Received(1).SaveTagAsync(Arg.Is<PrayerTag>(t => t.Name == "NewTag"));
        Assert.Single(sut.SelectedTags);
    }

    [Fact]
    public async Task CommaAutoSave_KeepsRemainder()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);

        sut.TagSearchText = "Worship, Hea";
        await Task.Delay(200);

        // "Worship" should be added, "Hea" should remain as search text
        Assert.Single(sut.SelectedTags);
        Assert.Equal("Worship", sut.SelectedTags[0].Name);
        Assert.Equal("Hea", sut.TagSearchText);
    }

    // ── DoneCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task DoneCommand_WithPendingText_SubmitsTagBeforeDismissing()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);
        sut.TagSearchText = "Worship";

        await ((IAsyncRelayCommand)sut.DoneCommand).ExecuteAsync(null);

        Assert.Single(sut.SelectedTags);
        Assert.Equal("Worship", sut.SelectedTags[0].Name);
        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task DoneCommand_WithPendingNewTag_CreatesTagBeforeDismissing()
    {
        var newTag = new PrayerTag { Id = 99, Name = "Gratitude" };
        _tagService.SaveTagAsync(Arg.Any<PrayerTag>()).Returns(newTag);

        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);
        sut.TagSearchText = "Gratitude";

        await ((IAsyncRelayCommand)sut.DoneCommand).ExecuteAsync(null);

        await _tagService.Received(1).SaveTagAsync(Arg.Is<PrayerTag>(t => t.Name == "Gratitude"));
        Assert.Single(sut.SelectedTags);
        Assert.Equal("Gratitude", sut.SelectedTags[0].Name);
        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task DoneCommand_WithWhitespaceOnly_DoesNotSubmitTag()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), []);
        sut.TagSearchText = "   ";

        await ((IAsyncRelayCommand)sut.DoneCommand).ExecuteAsync(null);

        Assert.Empty(sut.SelectedTags);
        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task DoneCommand_DismissesModal()
    {
        var sut = CreateSut();

        await ((IAsyncRelayCommand)sut.DoneCommand).ExecuteAsync(null);

        await _navigationService.Received(1).PopModalAsync();
    }

    [Fact]
    public async Task DoneCommand_SignalsDismiss()
    {
        var sut = CreateSut();
        var dismissTask = sut.WaitForDismissAsync();

        await ((IAsyncRelayCommand)sut.DoneCommand).ExecuteAsync(null);

        Assert.True(dismissTask.IsCompleted);
    }

    // ── GetSelectedTagIds ────────────────────────────────────────────

    [Fact]
    public void GetSelectedTagIds_ReturnsCurrentIds()
    {
        var sut = CreateSut();
        sut.Initialize(1, MakeTags(), [1, 3]);

        var ids = sut.GetSelectedTagIds();

        Assert.Equal(2, ids.Count);
        Assert.Contains(1, ids);
        Assert.Contains(3, ids);
    }

    [Fact]
    public async Task GetSelectedTagIds_ReflectsAdditions()
    {
        var sut = CreateSut();
        sut.Initialize(0, MakeTags(), [1]);

        await ((IAsyncRelayCommand<int>)sut.AddSuggestedTagCommand).ExecuteAsync(2);

        var ids = sut.GetSelectedTagIds();
        Assert.Equal(2, ids.Count);
        Assert.Contains(1, ids);
        Assert.Contains(2, ids);
    }
}
