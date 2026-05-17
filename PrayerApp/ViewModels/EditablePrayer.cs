using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    private bool _isDetailsExpanded;
    /// <summary>
    /// When false, empty rows show a "+ details" affordance instead of the Editor.
    /// Defaults to expanded when <see cref="Details"/> is non-empty after parse.
    /// </summary>
    public bool IsDetailsExpanded
    {
        get => _isDetailsExpanded;
        set
        {
            if (SetProperty(ref _isDetailsExpanded, value))
                OnPropertyChanged(nameof(ShowDetailsLink));
        }
    }

    public bool ShowDetailsLink => !IsDetailsExpanded;

    public IRelayCommand ExpandDetailsCommand { get; }

    public EditablePrayer()
    {
        ExpandDetailsCommand = new RelayCommand(() => IsDetailsExpanded = true);
    }

    public static EditablePrayer FromParsed(string title, string? details) =>
        new()
        {
            Title = title,
            Details = details,
            IsDetailsExpanded = !string.IsNullOrWhiteSpace(details),
        };
}
