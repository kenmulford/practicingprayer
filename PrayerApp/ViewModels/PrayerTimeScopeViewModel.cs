using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class SelectableTag : ObservableObject
{
    private bool _isSelected;
    public PrayerTag Tag { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public SelectableTag(PrayerTag tag) => Tag = tag;
}

public class PrayerTimeScopeViewModel : ObservableObject
{
    private readonly ITagService _tagService;
    private readonly INavigationService _navigationService;

    public ObservableCollection<SelectableTag> Tags { get; } = new();
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }

    public PrayerTimeScopeViewModel(ITagService tagService, INavigationService navigationService)
    {
        _tagService = tagService;
        _navigationService = navigationService;
        StartCommand = new AsyncRelayCommand(StartAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        LoadTagsAsync().SafeFireAndForget();
    }

    public PrayerTimeScopeViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ITagService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>())
    { }

    private async Task LoadTagsAsync()
    {
        try
        {
            var tags = await _tagService.GetTagsAsync();
            Tags.Clear();
            foreach (var tag in tags)
                Tags.Add(new SelectableTag(tag));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load tags: {ex.Message}");
            await _navigationService.DisplayAlertAsync("Error", "Unable to load tags.", "OK");
        }
    }

    private async Task StartAsync()
    {
        var selectedIds = Tags
            .Where(t => t.IsSelected)
            .Select(t => t.Tag.Id)
            .ToList();

        if (!selectedIds.Any())
        {
            await _navigationService.DisplayAlertAsync("No Tags Selected", "Please select at least one tag.", "OK");
            return;
        }

        var tagIdsParam = string.Join(",", selectedIds);
        await _navigationService.PopModalAsync();
        await _navigationService.GoToAsync($"{Routes.PrayerTimePage}?scope={Routes.ScopeTags}&tagIds={tagIdsParam}");
    }

    private async Task CancelAsync()
    {
        await _navigationService.PopModalAsync();
    }
}
