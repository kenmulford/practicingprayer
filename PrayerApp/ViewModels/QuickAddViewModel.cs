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

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public QuickAddViewModel(ICardService cardService, IPrayerService prayerService)
    {
        _cardService = cardService;
        _prayerService = prayerService;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync);
    }

    public QuickAddViewModel() : this(
        IPlatformApplication.Current!.Services.GetRequiredService<ICardService>(),
        IPlatformApplication.Current!.Services.GetRequiredService<IPrayerService>())
    { }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            await Shell.Current.DisplayAlertAsync("Required", "Please enter a prayer title.", "OK");
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
            SemanticScreenReader.Announce("Prayer saved");
            await Shell.Current.Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Quick add save failed: {ex.Message}");
            await Shell.Current.DisplayAlertAsync("Error", "Unable to save prayer. Please try again.", "OK");
        }
    }

    private async Task CancelAsync()
    {
        await Shell.Current.Navigation.PopModalAsync();
    }
}
