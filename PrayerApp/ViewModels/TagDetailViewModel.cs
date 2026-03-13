using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Collections.Generic;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class TagDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly ITagService _tagService;
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
                    OnPropertyChanged(nameof(Swatches));
            }
        }

        /// <summary>Swatch items for the color picker — wraps the palette with IsSelected state.</summary>
        public IReadOnlyList<ColorSwatchViewModel> Swatches { get; }

        public ICommand SaveCommand { get; }

        public TagDetailViewModel(ITagService tagService)
        {
            _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
            SaveCommand = new AsyncRelayCommand(SaveAsync);

            Swatches = TagColorPalette.Swatches
                .Select(s => new ColorSwatchViewModel(s.Light, s.Dark, s.Label, this))
                .ToList();
        }

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("load", out var loadVal) &&
                int.TryParse(loadVal?.ToString(), out int id))
            {
                _ = LoadAsync(id);
            }
        }

        private async Task LoadAsync(int id)
        {
            _tag = await PrayerTag.LoadAsync(id);
            OnPropertyChanged(nameof(Name));
            SelectedColorHex = _tag.Color ?? TagColorPalette.Swatches[0].Light;
            foreach (var s in Swatches)
                s.NotifySelectionChanged();
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
    }

    public class ColorSwatchViewModel : ObservableObject
    {
        private readonly TagDetailViewModel _parent;
        private readonly string _lightHex;
        private readonly string _darkHex;

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
            foreach (var s in _parent.Swatches)
                s.NotifySelectionChanged();
        }

        public void NotifySelectionChanged() => OnPropertyChanged(nameof(IsSelected));
    }
}
