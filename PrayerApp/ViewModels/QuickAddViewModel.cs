using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerApp.Models;
using PrayerApp.Services;
using System.Windows.Input;

namespace PrayerApp.ViewModels;

public class QuickAddViewModel : ObservableObject
{
    private readonly ICardService _cardService;
    private readonly IPrayerService _prayerService;
    private readonly INavigationService _navigationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly ISettings _settings;

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private bool _showTip;
    public bool ShowTip
    {
        get => _showTip;
        private set => SetProperty(ref _showTip, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DismissTipCommand { get; }

    public QuickAddViewModel(ICardService cardService, IPrayerService prayerService,
        INavigationService navigationService, IAccessibilityService accessibilityService,
        ISettings settings)
    {
        _cardService = cardService;
        _prayerService = prayerService;
        _navigationService = navigationService;
        _accessibilityService = accessibilityService;
        _settings = settings;
        _showTip = !settings.QuickAddTipDismissed;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
        DismissTipCommand = new RelayCommand(DismissTip);
    }

    public QuickAddViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IAccessibilityService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<ISettings>())
    { }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await _navigationService.DisplayAlertAsync("Required", "Please enter a prayer title.", "OK");
            return;
        }

        try
        {
            var systemCard = await _cardService.GetOrCreateQuickAddCardAsync();
            var prayer = new Prayer
            {
                Title = Title.Trim(),
                PrayerCardId = systemCard.Id
            };
            await _prayerService.SavePrayerAsync(prayer);
            _prayerService.InvalidateCache();
            _accessibilityService.Announce("Prayer saved");
            await _navigationService.PopModalAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Quick add save failed: {ex.Message}");
            await _navigationService.DisplayAlertAsync("Error", "Unable to save prayer. Please try again.", "OK");
        }
    }

    private void DismissTip()
    {
        _settings.QuickAddTipDismissed = true;
        ShowTip = false;
    }

    private async Task CancelAsync()
    {
        await _navigationService.PopModalAsync();
    }
}
