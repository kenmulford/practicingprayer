using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using SQLite;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class TagDetailViewModel : ObservableObject, IQueryAttributable, IEditGuard
    {
        private readonly ITagService _tagService;
        private readonly IUserColorService _userColorService;
        private readonly IColorPickerService _colorPickerService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private PrayerTag _tag = new();
        private string _selectedColorHex = TagColorPalette.Swatches[0].Light;
        private string _firstDefaultHex = string.Empty;
        private CancellationTokenSource _cts = new();

        // Dirty-tracking originals (set after load/save)
        private string _originalName = string.Empty;
        private string _originalColorHex = string.Empty;

        public string Name
        {
            get => _tag.Name;
            set
            {
                if (_tag.Name != value)
                {
                    _tag.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>True when editing a system tag — name is read-only, delete is hidden.</summary>
        public bool IsSystem { get; private set; }

        public bool IsNameEditable => !IsSystem;

        public bool IsDirty =>
            Name != _originalName ||
            SelectedColorHex != _originalColorHex;

        public async Task<bool> CanLeaveAsync()
        {
            if (!IsDirty) return true;
            return await _navigationService.DisplayConfirmAsync(
                "Unsaved Changes", "Discard changes?", "Discard", "Cancel");
        }

        public string SelectedColorHex
        {
            get => _selectedColorHex;
            set
            {
                if (SetProperty(ref _selectedColorHex, value))
                {
                    foreach (var s in Swatches)
                        s.NotifySelectionChanged();
                }
            }
        }

        /// <summary>Dynamic swatch items — loaded from UserColor table.</summary>
        public ObservableCollection<ColorSwatchViewModel> Swatches { get; } = new();

        public bool IsExisting => _tag.Id > 0;

        /// <summary>
        /// True while SaveAsync is in flight. Drives the page-level ActivityIndicator
        /// and gates SaveCommand canExecute against double-tap.
        /// </summary>
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                    (SaveCommand as IAsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand AddColorCommand { get; }
        public ICommand ViewPrayersCommand { get; }

        public TagDetailViewModel(ITagService tagService, IUserColorService userColorService,
            IColorPickerService colorPickerService, INavigationService navigationService,
            IAccessibilityService accessibilityService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            _userColorService = userColorService ?? throw new ArgumentNullException(nameof(userColorService));
            _colorPickerService = colorPickerService ?? throw new ArgumentNullException(nameof(colorPickerService));
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            _firstDefaultHex = _userColorService.GetFirstDefaultHex();
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
            AddColorCommand = new AsyncRelayCommand(AddColorAsync);
            ViewPrayersCommand = new AsyncRelayCommand(ViewPrayersAsync);

            CaptureOriginals();

            LoadSwatchesAsync().SafeFireAndForget();
        }

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("load", out var loadVal) &&
                int.TryParse(loadVal?.ToString(), out int id))
            {
                LoadAsync(id).SafeFireAndForget();
            }
        }

        /// <summary>
        /// Called by TagDetailPage.OnDisappearing to stop any in-flight async work.
        /// Prevents ObservableCollection modifications after the page's native
        /// handlers have been torn down (which causes SIGABRT on iOS).
        /// Disposes the old CTS and creates a fresh one so the ViewModel can be
        /// reused if the page is navigated to again.
        /// </summary>
        internal void CancelPendingWork()
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        private async Task LoadSwatchesAsync()
        {
            var token = _cts.Token;
            var userColors = await _userColorService.GetColorsAsync();
            if (token.IsCancellationRequested) return;

            Swatches.Clear();
            foreach (var uc in userColors)
            {
                if (token.IsCancellationRequested) return;
                var darkHex = TagColorPalette.GetDarkVariant(uc.HexValue) ?? uc.HexValue;
                Swatches.Add(new ColorSwatchViewModel(
                    uc.HexValue, darkHex, string.Empty, this,
                    userColorId: uc.Id, isDefault: uc.IsDefault));
            }
        }

        /// <summary>Called by ColorSwatchViewModel when user confirms delete.</summary>
        internal async Task RequestDeleteSwatchAsync(ColorSwatchViewModel swatch)
        {
            if (swatch.IsDefault) return;

            var confirm = await _navigationService.DisplayConfirmAsync(
                "Delete Color", "Remove this color from your palette?", "Delete", "Cancel");
            if (!confirm) return;

            // Delete the UserColor
            await _userColorService.DeleteColorAsync(swatch.UserColorId);

            // Reassign any tags using this color to the first default
            await _tagService.ReassignColorAsync(swatch.LightHex, _firstDefaultHex);

            // If the deleted color was selected, switch to the default
            if (string.Equals(SelectedColorHex, swatch.LightHex, StringComparison.OrdinalIgnoreCase))
                SelectedColorHex = _firstDefaultHex;

            // Rebuild swatches
            await LoadSwatchesAsync();
        }

        private async Task LoadAsync(int id)
        {
            var result = await PrayerTag.LoadAsync(id);
            if (result is null)
            {
                await _navigationService.GoToAsync("..");
                return;
            }
            _tag = result;
            IsSystem = result.IsSystem;
            OnPropertyChanged(nameof(IsSystem));
            OnPropertyChanged(nameof(IsExisting));
            OnPropertyChanged(nameof(IsNameEditable));
            OnPropertyChanged(nameof(Name));
            SelectedColorHex = _tag.Color ?? TagColorPalette.Swatches[0].Light;
            CaptureOriginals();
        }

        private async Task SaveAsync()
        {
            if (IsBusy) return;
            if (string.IsNullOrWhiteSpace(Name))
            {
                await _navigationService.DisplayAlertAsync("Validation", "Tag name cannot be empty.", "OK");
                return;
            }

            _tag.Color = SelectedColorHex;
            IsBusy = true;
            try
            {
                await _tagService.SaveTagAsync(_tag);
                CaptureOriginals();
                _accessibilityService.Announce("Tag saved");

                // Cancel any in-flight async work (LoadSwatchesAsync, etc.) BEFORE
                // navigating. This prevents ObservableCollection modifications during
                // page teardown — the root cause of the iOS SIGABRT crash (BUG-1).
                CancelPendingWork();

                await _navigationService.GoToAsync("..");
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.Constraint)
            {
                await _navigationService.DisplayAlertAsync(
                    "Duplicate Tag Name",
                    $"A tag named '{Name}' already exists. Please choose a different name.",
                    "OK");
            }
            finally { IsBusy = false; }
        }

        private void CaptureOriginals()
        {
            _originalName = Name ?? string.Empty;
            _originalColorHex = SelectedColorHex;
        }

        private async Task AddColorAsync()
        {
            var hex = await _colorPickerService.PickColorAsync();
            if (string.IsNullOrWhiteSpace(hex)) return;

            hex = hex.ToUpperInvariant();

            // Save to the user's palette (returns existing if duplicate)
            var saved = await _userColorService.SaveColorAsync(hex);

            // Add swatch if not already present
            if (!Swatches.Any(s => string.Equals(s.LightHex, hex, StringComparison.OrdinalIgnoreCase)))
            {
                var darkHex = TagColorPalette.GetDarkVariant(hex) ?? hex;
                Swatches.Add(new ColorSwatchViewModel(
                    hex, darkHex, string.Empty, this,
                    userColorId: saved.Id, isDefault: false));
            }

            // Auto-select the new color
            SelectedColorHex = hex;
        }

        private async Task ViewPrayersAsync()
        {
            await _navigationService.GoToAsync($"{Routes.PrayersTab}?tagId={_tag.Id}");
        }
    }

    public class ColorSwatchViewModel : ObservableObject
    {
        private readonly TagDetailViewModel _parent;
        private readonly string _lightHex;
        private readonly string _darkHex;

        public string LightHex => _lightHex;
        public string Label { get; }
        public bool IsDefault { get; }
        public int UserColorId { get; }
        public Color SwatchColor =>
            Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb(_darkHex)
                : Color.FromArgb(_lightHex);

        public bool IsSelected => string.Equals(_parent.SelectedColorHex, _lightHex, StringComparison.OrdinalIgnoreCase);

        public ICommand SelectCommand { get; }
        public ICommand DeleteCommand { get; }

        public ColorSwatchViewModel(string lightHex, string darkHex, string label,
            TagDetailViewModel parent, int userColorId = 0, bool isDefault = false)
        {
            _lightHex = lightHex;
            _darkHex = darkHex;
            Label = label;
            _parent = parent;
            UserColorId = userColorId;
            IsDefault = isDefault;
            SelectCommand = new RelayCommand(Select);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsDefault);
        }

        private void Select()
        {
            _parent.SelectedColorHex = _lightHex;
        }

        private async Task DeleteAsync()
        {
            if (IsDefault) return;
            await _parent.RequestDeleteSwatchAsync(this);
        }

        public void NotifySelectionChanged() => OnPropertyChanged(nameof(IsSelected));
    }
}
