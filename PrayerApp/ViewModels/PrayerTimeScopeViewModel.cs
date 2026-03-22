using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.PrayerTime;
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

    public ObservableCollection<SelectableTag> Tags { get; } = new();
    public ICommand StartCommand { get; }
    public ICommand CancelCommand { get; }

    public PrayerTimeScopeViewModel()
    {
        _tagService = IPlatformApplication.Current!.Services.GetRequiredService<ITagService>();
        StartCommand = new AsyncRelayCommand(StartAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        LoadTagsAsync().SafeFireAndForget();
    }

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
            await Shell.Current.DisplayAlertAsync("Error", "Unable to load tags.", "OK");
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
            await Shell.Current.DisplayAlertAsync("No Tags Selected", "Please select at least one tag.", "OK");
            return;
        }

        var tagIdsParam = string.Join(",", selectedIds);
        await Shell.Current.Navigation.PopModalAsync();
        await Shell.Current.GoToAsync($"{nameof(PrayerTimePage)}?scope=tags&tagIds={tagIdsParam}");
    }

    private async Task CancelAsync()
    {
        await Shell.Current.Navigation.PopModalAsync();
    }
}
