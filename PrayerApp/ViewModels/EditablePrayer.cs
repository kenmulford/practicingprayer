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

    // Position (1-based) of this row within the Prayers collection and the
    // current total row count, stamped by ConfirmImportViewModel whenever the
    // collection changes. They fold into the accessible descriptions below so a
    // screen-reader user hears a positional cue ("item 2 of 3") instead of the
    // same label on every row (#15 / A11Y-5).
    private int _position;
    public int Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
                RaiseAccessibleDescriptionsChanged();
        }
    }

    private int _total;
    public int Total
    {
        get => _total;
        set
        {
            if (SetProperty(ref _total, value))
                RaiseAccessibleDescriptionsChanged();
        }
    }

    // Computed accessible labels — the visible field has no text label, so
    // SemanticProperties.Description is the correct accessible name. The
    // "{n} of {count}" phrasing mirrors PrayerTimeViewModel.ProgressDisplay.
    public string TitleAccessibleDescription => $"Prayer title, item {Position} of {Total}";
    public string DetailsAccessibleDescription => $"Prayer details, item {Position} of {Total}";
    public string RemoveAccessibleDescription => $"Remove prayer, item {Position} of {Total}";

    private void RaiseAccessibleDescriptionsChanged()
    {
        OnPropertyChanged(nameof(TitleAccessibleDescription));
        OnPropertyChanged(nameof(DetailsAccessibleDescription));
        OnPropertyChanged(nameof(RemoveAccessibleDescription));
    }
}
