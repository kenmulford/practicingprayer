using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

/// <summary>
/// ViewModel for the tag picker modal. Manages tag search, suggestion filtering,
/// comma-delimited auto-save, and tag creation. Presented modally from PrayerDetailPage.
/// </summary>
public class TagPickerViewModel : ObservableObject
{
    private readonly ITagService _tagService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private List<PrayerTag> _allTags = new();
    private int _prayerId;
    private readonly TaskCompletionSource _dismissed = new();

    public ObservableCollection<TagChipViewModel> SelectedTags { get; } = new();
    public ObservableCollection<PrayerTag> SuggestedTags { get; } = new();

    public bool HasTags => SelectedTags.Count > 0;
    public bool HasSuggestions => SuggestedTags.Count > 0;

    public ICommand AddSuggestedTagCommand { get; }
    public ICommand SubmitTagEntryCommand { get; }
    public ICommand DoneCommand { get; }

    private string _tagSearchText = string.Empty;
    public string TagSearchText
    {
        get => _tagSearchText;
        set
        {
            if (!SetProperty(ref _tagSearchText, value)) return;

            if (value.Contains(','))
                ProcessCommaInputAsync(value).SafeFireAndForget();
            else
                UpdateSuggestions();
        }
    }

    public TagPickerViewModel(ITagService tagService, INavigationService navigationService,
        IAccessibilityService accessibilityService)
    {
        _tagService = tagService;
        _navigationService = navigationService;
        _accessibilityService = accessibilityService;

        AddSuggestedTagCommand = new AsyncRelayCommand<int>(AddSuggestedTagAsync);
        SubmitTagEntryCommand = new AsyncRelayCommand(SubmitTagEntryAsync);
        DoneCommand = new AsyncRelayCommand(DoneAsync);

        SelectedTags.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTags));
        SuggestedTags.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSuggestions));
    }

    // Service locator fallback (required for XAML parameterless constructor convention)
    public TagPickerViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>())
    { }

    /// <summary>
    /// Initialize the picker with the prayer's current state.
    /// </summary>
    /// <param name="prayerId">Prayer ID (0 for new/unsaved prayers — tags staged locally).</param>
    /// <param name="allTags">Full tag list from the parent VM's cached _allTags.</param>
    /// <param name="selectedTagIds">IDs of tags currently assigned to this prayer.</param>
    public void Initialize(int prayerId, List<PrayerTag> allTags, List<int> selectedTagIds)
    {
        _prayerId = prayerId;
        _allTags = allTags;

        SelectedTags.Clear();
        var selectedSet = selectedTagIds.ToHashSet();
        foreach (var tag in _allTags.Where(t => selectedSet.Contains(t.Id)).OrderBy(t => t.Name))
            SelectedTags.Add(new TagChipViewModel(tag, RemoveTagAsync));
    }

    /// <summary>
    /// Returns the IDs of currently selected tags so the parent VM can sync state.
    /// </summary>
    public List<int> GetSelectedTagIds() =>
        SelectedTags.Select(t => t.Id).ToList();

    private async Task ProcessCommaInputAsync(string text)
    {
        // Split on comma — process each complete segment sequentially
        var segments = text.Split(',');
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i].Trim();
            if (!string.IsNullOrWhiteSpace(segment))
                await AddOrCreateTagAsync(segment);
        }

        // Keep any text after the last comma as the new search text
        var remainder = segments[^1].Trim();
        _tagSearchText = remainder;
        OnPropertyChanged(nameof(TagSearchText));
        UpdateSuggestions();
    }

    private void UpdateSuggestions()
    {
        SuggestedTags.Clear();
        if (string.IsNullOrWhiteSpace(_tagSearchText)) return;

        var assignedIds = SelectedTags.Select(t => t.Id).ToHashSet();
        var filtered = _allTags
            .Where(t => !t.IsSystem && !assignedIds.Contains(t.Id) &&
                        (t.Name?.Contains(_tagSearchText, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(t => t.Name)
            .Take(6);

        foreach (var tag in filtered)
            SuggestedTags.Add(tag);
    }

    private async Task AddSuggestedTagAsync(int tagId)
    {
        if (SelectedTags.Any(t => t.Id == tagId)) return;
        var tag = _allTags.FirstOrDefault(t => t.Id == tagId);
        if (tag is null) return;

        if (_prayerId > 0)
            await _tagService.AddTagToRequestAsync(_prayerId, tagId);

        SelectedTags.Add(new TagChipViewModel(tag, RemoveTagAsync));
        _tagSearchText = string.Empty;
        OnPropertyChanged(nameof(TagSearchText));
        UpdateSuggestions();
        _accessibilityService.Announce($"Added tag {tag.Name}");
    }

    private async Task RemoveTagAsync(int tagId)
    {
        if (_prayerId > 0)
            await _tagService.RemoveTagFromRequestAsync(_prayerId, tagId);

        var chip = SelectedTags.FirstOrDefault(t => t.Id == tagId);
        if (chip is not null)
        {
            SelectedTags.Remove(chip);
            _accessibilityService.Announce($"Removed tag {chip.Name}");
        }
        UpdateSuggestions();
    }

    private async Task SubmitTagEntryAsync()
    {
        var text = _tagSearchText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        await AddOrCreateTagAsync(text);
    }

    private async Task AddOrCreateTagAsync(string text)
    {
        // If an existing tag matches exactly, assign it
        var exactMatch = _allTags.FirstOrDefault(
            t => string.Equals(t.Name, text, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
        {
            await AddSuggestedTagAsync(exactMatch.Id);
            return;
        }

        // Prevent duplicate by display name (case-insensitive)
        if (SelectedTags.Any(t => string.Equals(t.Name, text, StringComparison.OrdinalIgnoreCase)))
            return;

        // Create a new tag
        var newTag = new PrayerTag { Name = text };
        try
        {
            newTag = await _tagService.SaveTagAsync(newTag);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tag creation failed: {ex.Message}");
            return;
        }
        _allTags.Add(newTag);

        if (_prayerId > 0)
            await _tagService.AddTagToRequestAsync(_prayerId, newTag.Id);

        SelectedTags.Add(new TagChipViewModel(newTag, RemoveTagAsync));
        _tagSearchText = string.Empty;
        OnPropertyChanged(nameof(TagSearchText));
        UpdateSuggestions();
        _accessibilityService.Announce($"Created tag {newTag.Name}");
    }

    /// <summary>
    /// Awaitable signal that resolves when the picker modal is dismissed.
    /// </summary>
    public Task WaitForDismissAsync() => _dismissed.Task;

    /// <summary>Signal dismissal from outside (e.g., back gesture).</summary>
    public void SignalDismiss() => _dismissed.TrySetResult();

    private async Task DoneAsync()
    {
        _dismissed.TrySetResult();
        await _navigationService.PopModalAsync();
    }
}
