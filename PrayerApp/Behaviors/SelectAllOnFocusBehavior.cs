namespace PrayerApp.Behaviors;

/// <summary>
/// Selects all text in an Entry when it receives focus.
/// </summary>
/// <remarks>
/// Defers selection by one dispatcher tick — on iOS the native cursor is
/// positioned after the Focused event fires, so a synchronous selection is
/// immediately overwritten by the platform.
/// </remarks>
public class SelectAllOnFocusBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry entry)
    {
        base.OnAttachedTo(entry);
        entry.Focused += OnFocused;
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.Focused -= OnFocused;
        base.OnDetachingFrom(entry);
    }

    private static void OnFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;
        entry.Dispatcher.Dispatch(() =>
        {
            entry.CursorPosition = 0;
            entry.SelectionLength = entry.Text?.Length ?? 0;
        });
    }
}
