using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    /// <summary>
    /// ViewModel for multi-select tag selection UI.
    /// Handles toggling tags on/off and exposing selected tags for binding.
    /// </summary>
    public class PrayerTagSelectionViewModel : ObservableObject
    {
        private readonly ITagService _tagService;
        private int _prayerCardId;
        private ObservableCollection<PrayerTagItemViewModel> _allTags;
        private ObservableCollection<PrayerTagItemViewModel> _selectedTags;

        public ICommand ClearSelectionCommand { get; private set; }

        public ObservableCollection<PrayerTagItemViewModel> AllTags
        {
            get => _allTags;
            private set => SetProperty(ref _allTags, value);
        }

        public ObservableCollection<PrayerTagItemViewModel> SelectedTags
        {
            get => _selectedTags;
            private set => SetProperty(ref _selectedTags, value);
        }

        public PrayerTagSelectionViewModel(ITagService tagService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            _allTags = new ObservableCollection<PrayerTagItemViewModel>();
            _selectedTags = new ObservableCollection<PrayerTagItemViewModel>();

            ClearSelectionCommand = new RelayCommand(ClearSelection);
        }

        public async Task InitializeForCardAsync(int prayerCardId)
        {
            _prayerCardId = prayerCardId;
            await LoadTagsAsync();
            await LoadSelectedTagsAsync();
        }

        private async Task LoadTagsAsync()
        {
            try
            {
                var tags = await _tagService.GetTagsAsync();
                var tagItems = tags.Select(t => new PrayerTagItemViewModel(t, OnTagToggled)).ToList();

                AllTags.Clear();
                foreach (var item in tagItems)
                {
                    AllTags.Add(item);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load tags: {ex.Message}", "OK");
            }
        }

        private async Task LoadSelectedTagsAsync()
        {
            try
            {
                var selectedTags = await _tagService.GetTagsByCardIdAsync(_prayerCardId);
                var selectedIds = selectedTags.Select(t => t.Id).ToHashSet();

                // Update AllTags to mark selected ones
                foreach (var tagItem in AllTags)
                {
                    tagItem.IsSelected = selectedIds.Contains(tagItem.Id);
                }

                RefreshSelectedTags();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to load card tags: {ex.Message}", "OK");
            }
        }

        private void OnTagToggled(int tagId, bool isSelected)
        {
            // Queue the tag operation but don't await here to keep UI responsive
            _ = ToggleTagAsync(tagId, isSelected);
            RefreshSelectedTags();
        }

        private async Task ToggleTagAsync(int tagId, bool isSelected)
        {
            try
            {
                if (isSelected)
                {
                    await _tagService.AddTagToCardAsync(_prayerCardId, tagId);
                }
                else
                {
                    await _tagService.RemoveTagFromCardAsync(_prayerCardId, tagId);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to update tag: {ex.Message}", "OK");
            }
        }

        private void RefreshSelectedTags()
        {
            SelectedTags.Clear();
            foreach (var tag in AllTags.Where(t => t.IsSelected))
            {
                SelectedTags.Add(tag);
            }
        }

        private void ClearSelection()
        {
            foreach (var tag in AllTags)
            {
                tag.IsSelected = false;
            }
            RefreshSelectedTags();
        }
    }

    /// <summary>
    /// Individual tag item for selection UI
    /// </summary>
    public class PrayerTagItemViewModel : ObservableObject
    {
        private bool _isSelected;
        private Action<int, bool> _onToggleCallback;

        public int Id { get; }
        public string Name { get; }
        public string? Color { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    _onToggleCallback?.Invoke(Id, value);
                }
            }
        }

        public PrayerTagItemViewModel(PrayerTag tag, Action<int, bool> onToggleCallback)
        {
            Id = tag.Id;
            Name = tag.Name;
            Color = tag.Color;
            _onToggleCallback = onToggleCallback;
        }
    }
}
