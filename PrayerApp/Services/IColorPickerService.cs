namespace PrayerApp.Services;

/// <summary>
/// Abstracts the platform-specific color picker. iOS uses the native UIColorPickerViewController;
/// Android uses a hex-entry popup.
/// </summary>
public interface IColorPickerService
{
    /// <summary>
    /// Shows a color picker and returns the selected hex string (e.g. "#B84040"),
    /// or null if the user cancelled.
    /// </summary>
    Task<string?> PickColorAsync();
}
