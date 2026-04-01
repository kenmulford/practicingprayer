using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Helpers;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Windows.Input;

namespace PrayerApp.ViewModels
{
    public class BoxDetailViewModel : ObservableObject, IQueryAttributable, IEditGuard
    {
        private readonly IBoxService _boxService;
        private readonly INavigationService _navigationService;
        private readonly IAccessibilityService _accessibilityService;
        private CardBox _box = new();
        private string _originalName = string.Empty;

        public string Name
        {
            get => _box.Name;
            set
            {
                if (_box.Name != value)
                {
                    _box.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSystem { get; private set; }
        public bool IsNameEditable => !IsSystem;
        public bool IsExisting => _box.Id > 0;

        public bool IsDirty => Name != _originalName;

        public async Task<bool> CanLeaveAsync()
        {
            if (!IsDirty) return true;
            return await _navigationService.DisplayConfirmAsync(
                "Unsaved Changes", "Discard changes?", "Discard", "Cancel");
        }

        public ICommand SaveCommand { get; }

        public BoxDetailViewModel(IBoxService boxService, INavigationService navigationService,
            IAccessibilityService accessibilityService)
        {
            _boxService = boxService ?? throw new ArgumentNullException(nameof(boxService));
            _navigationService = navigationService;
            _accessibilityService = accessibilityService;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CaptureOriginals();
        }

        public BoxDetailViewModel(IBoxService boxService) : this(
            boxService,
            IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
            IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>())
        { }

        void IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("load", out var loadVal) &&
                int.TryParse(loadVal?.ToString(), out int id))
            {
                LoadAsync(id).SafeFireAndForget();
            }
        }

        private async Task LoadAsync(int id)
        {
            var result = await CardBox.LoadAsync(id);
            if (result is null)
            {
                await _navigationService.GoToAsync("..");
                return;
            }
            _box = result;
            IsSystem = result.IsSystem;
            OnPropertyChanged(nameof(IsSystem));
            OnPropertyChanged(nameof(IsExisting));
            OnPropertyChanged(nameof(IsNameEditable));
            OnPropertyChanged(nameof(Name));
            CaptureOriginals();
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                await _navigationService.DisplayAlertAsync("Validation",
                    $"{BoxStrings.Word} name cannot be empty.", "OK");
                return;
            }

            await _boxService.SaveBoxAsync(_box);
            CaptureOriginals();
            _accessibilityService.Announce($"{BoxStrings.Word} saved");
            await _navigationService.GoToAsync("..");
        }

        private void CaptureOriginals()
        {
            _originalName = Name ?? string.Empty;
        }
    }
}
