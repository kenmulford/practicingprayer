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

    /// <summary>1-based index within the import prayer list.</summary>
    public int ItemIndex { get; private set; }

    public int ItemCount { get; private set; }

    public string TitleSemanticDescription => FormatPositionLabel("Prayer title");

    public string DetailsSemanticDescription => FormatPositionLabel("Prayer details");

    public string RemoveSemanticDescription => FormatPositionLabel("Remove prayer");

    public void UpdatePosition(int itemIndex, int itemCount)
    {
        if (ItemIndex == itemIndex && ItemCount == itemCount)
            return;

        ItemIndex = itemIndex;
        ItemCount = itemCount;
        OnPropertyChanged(nameof(TitleSemanticDescription));
        OnPropertyChanged(nameof(DetailsSemanticDescription));
        OnPropertyChanged(nameof(RemoveSemanticDescription));
    }

    private string FormatPositionLabel(string label) =>
        ItemCount > 0 ? $"{label}, item {ItemIndex} of {ItemCount}" : label;
}
