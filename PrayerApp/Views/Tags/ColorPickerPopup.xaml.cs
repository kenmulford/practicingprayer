using CommunityToolkit.Maui.Views;
using System.Text.RegularExpressions;

namespace PrayerApp.Views.Tags;

public partial class ColorPickerPopup : Popup
{
    private static readonly Regex HexPattern = new(@"^#[0-9A-Fa-f]{6}$");

    /// <summary>The selected hex string, or null if cancelled.</summary>
    public string? SelectedHex { get; private set; }

    public ColorPickerPopup()
    {
        InitializeComponent();
    }

    private void OnHexTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue?.Trim() ?? string.Empty;

        // Auto-prepend # if user types raw hex
        if (text.Length > 0 && text[0] != '#')
            text = "#" + text;

        if (HexPattern.IsMatch(text))
        {
            try
            {
                var color = Color.FromArgb(text);
                PreviewCircle.Fill = new SolidColorBrush(color);
                return;
            }
            catch { }
        }

        PreviewCircle.Fill = new SolidColorBrush(Colors.Gray);
    }

    private async void OnCancel(object? sender, EventArgs e)
    {
        SelectedHex = null;
        await CloseAsync(CancellationToken.None);
    }

    private async void OnAccept(object? sender, EventArgs e)
    {
        var text = HexEntry.Text?.Trim() ?? string.Empty;
        if (text.Length > 0 && text[0] != '#')
            text = "#" + text;

        if (!HexPattern.IsMatch(text))
        {
            await Shell.Current.DisplayAlertAsync("Invalid Color", "Please enter a valid hex color (e.g. #B84040).", "OK");
            return;
        }

        SelectedHex = text.ToUpperInvariant();
        await CloseAsync(CancellationToken.None);
    }
}
