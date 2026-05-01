using CommunityToolkit.Mvvm.ComponentModel;

namespace PrayerApp.ViewModels;

public class EditablePrayer : ObservableObject
{
    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? string.Empty);
    }

    private string? _details;
    public string? Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }
}
