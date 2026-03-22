using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class TagDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly ITagService _tagService;
        private readonly IUserColorService _userColorService;
        private readonly IColorPickerService _colorPickerService;
        private PrayerTag _tag = new();
        private string _selectedColorHex = TagColorPalette.Swatches[0].Light;

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

        public ICommand SaveCommand { get; }
        public ICommand AddColorCommand { get; }

        public TagDetailViewModel(ITagService tagService, IUserColorService userColorService, IColorPickerService colorPickerService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            _userColorService = userColorService ?? throw new ArgumentNullException(nameof(userColorService));
            _colorPickerService = colorPickerService ?? throw new ArgumentNullException(nameof(colorPickerService));
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddColorCommand = new AsyncRelayCommand(AddColorAsync);

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

        private async Task LoadSwatchesAsync()
        {
            var userColors = await _userColorService.GetColorsAsync();

            Swatches.Clear();
            foreach (var uc in userColors)
            {
                var darkHex = TagColorPalette.GetDarkVariant(uc.HexValue) ?? uc.HexValue;
                Swatches.Add(new ColorSwatchViewModel(uc.HexValue, darkHex, string.Empty, this));
            }
        }

        private async Task LoadAsync(int id)
        {
            var result = await PrayerTag.LoadAsync(id);
            if (result is null)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
            _tag = result;
            OnPropertyChanged(nameof(Name));
            SelectedColorHex = _tag.Color ?? TagColorPalette.Swatches[0].Light;
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                await Shell.Current.DisplayAlertAsync("Validation", "Tag name cannot be empty.", "OK");
                return;
            }

            _tag.Color = SelectedColorHex;
            await _tagService.SaveTagAsync(_tag);
            await Shell.Current.GoToAsync("..");
        }

        private async Task AddColorAsync()
        {
            var hex = await _colorPickerService.PickColorAsync();
            if (string.IsNullOrWhiteSpace(hex)) return;

            hex = hex.ToUpperInvariant();

            // Save to the user's palette
            await _userColorService.SaveColorAsync(hex);

            // Add swatch if not already present
            if (!Swatches.Any(s => string.Equals(s.LightHex, hex, StringComparison.OrdinalIgnoreCase)))
            {
                var darkHex = TagColorPalette.GetDarkVariant(hex) ?? hex;
                Swatches.Add(new ColorSwatchViewModel(hex, darkHex, string.Empty, this));
            }

            // Auto-select the new color
            SelectedColorHex = hex;
        }
    }

    public class ColorSwatchViewModel : ObservableObject
    {
        private readonly TagDetailViewModel _parent;
        private readonly string _lightHex;
        private readonly string _darkHex;

        public string LightHex => _lightHex;
        public string Label { get; }
        public Color SwatchColor =>
            Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb(_darkHex)
                : Color.FromArgb(_lightHex);

        public bool IsSelected => string.Equals(_parent.SelectedColorHex, _lightHex, StringComparison.OrdinalIgnoreCase);

        public ICommand SelectCommand { get; }

        public ColorSwatchViewModel(string lightHex, string darkHex, string label, TagDetailViewModel parent)
        {
            _lightHex = lightHex;
            _darkHex = darkHex;
            Label = label;
            _parent = parent;
            SelectCommand = new RelayCommand(Select);
        }

        private void Select()
        {
            _parent.SelectedColorHex = _lightHex;
        }

        public void NotifySelectionChanged() => OnPropertyChanged(nameof(IsSelected));
    }
}
