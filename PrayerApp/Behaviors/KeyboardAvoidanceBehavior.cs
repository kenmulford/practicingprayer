using CommunityToolkit.Maui.Behaviors;

namespace PrayerApp.Behaviors;

/// <summary>
/// Scrolls the focused <see cref="InputView"/> into view inside its nearest
/// <see cref="ScrollView"/> ancestor when it gains focus, so the on-screen
/// keyboard never covers the active editor.
/// </summary>
/// <remarks>
/// Layered on top of the platform default. iOS' <c>KeyboardAutoManagerScroll</c>
/// runs first; we yield two frames before our own additive
/// <see cref="ScrollView.ScrollToAsync(Element, ScrollToPosition, bool)"/> so the
/// platform pass completes before we adjust. Re-entrancy on rapid focus changes
/// is bounded by a single <see cref="CancellationTokenSource"/>.
///
/// Per-control opt-in via <c>InputView.Behaviors</c> until we have ≥3
/// concrete consumers — at that point promote to an implicit <c>Style</c>.
///
/// Citations:
///   https://learn.microsoft.com/dotnet/communitytoolkit/maui/behaviors/
///   https://learn.microsoft.com/dotnet/api/microsoft.maui.controls.scrollview.scrolltoasync?view=net-maui-10.0
/// </remarks>
public class KeyboardAvoidanceBehavior : BaseBehavior<InputView>
{
    /// <summary>
    /// Visual padding (px) intended between the focused input and the scroll
    /// viewport edge. Currently a no-op — the algorithm uses
    /// <see cref="ScrollToPosition.Center"/>, which provides natural padding
    /// from the keyboard edge. The property is on the surface so consumers
    /// can pre-set it before the algorithm grows a tunable mode (e.g.
    /// <c>MakeVisible</c> with explicit margin) without an API break.
    /// </summary>
    public static readonly BindableProperty MarginProperty =
        BindableProperty.Create(
            propertyName: nameof(Margin),
            returnType: typeof(double),
            declaringType: typeof(KeyboardAvoidanceBehavior),
            defaultValue: 40.0);

    public double Margin
    {
        get => (double)GetValue(MarginProperty);
        set => SetValue(MarginProperty, value);
    }

    public static readonly BindableProperty AnimateProperty =
        BindableProperty.Create(
            propertyName: nameof(Animate),
            returnType: typeof(bool),
            declaringType: typeof(KeyboardAvoidanceBehavior),
            defaultValue: true);

    public bool Animate
    {
        get => (bool)GetValue(AnimateProperty);
        set => SetValue(AnimateProperty, value);
    }

    public static readonly BindableProperty IsEnabledProperty =
        BindableProperty.Create(
            propertyName: nameof(IsEnabled),
            returnType: typeof(bool),
            declaringType: typeof(KeyboardAvoidanceBehavior),
            defaultValue: true);

    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    private CancellationTokenSource? _cts;

    protected override void OnAttachedTo(InputView bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.Focused += OnFocused;
    }

    protected override void OnDetachingFrom(InputView bindable)
    {
        bindable.Focused -= OnFocused;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDetachingFrom(bindable);
    }

    private async void OnFocused(object? sender, FocusEventArgs e)
    {
        if (!IsEnabled || View is null) return;
        await ScrollIfNeededAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Internal seam for unit testing the IsEnabled gate without spinning up
    /// a MAUI handler. Returns a completed task with no-op when there is no
    /// ScrollView ancestor.
    /// </summary>
    internal async Task ScrollIfNeededAsync()
    {
        if (!IsEnabled || View is null) return;

        // Cancel any in-flight scroll from a prior rapid Focused.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var scrollView = WalkToScrollView(View);
        if (scrollView is null) return;

        // Yield twice so iOS KeyboardAutoManagerScroll's default pass
        // completes before our additive scroll. Without this, the platform
        // can overwrite our scroll position one frame later.
        await Task.Yield();
        await Task.Yield();
        if (token.IsCancellationRequested) return;

        try
        {
            await scrollView.ScrollToAsync(View, ScrollToPosition.Center, Animate);
        }
        catch (TaskCanceledException)
        {
            // Animation cancelled by the next focused-event scroll. Expected.
        }
    }

    /// <summary>
    /// Walks the visual tree upward from <paramref name="start"/>, returning
    /// the nearest <see cref="ScrollView"/> ancestor or <c>null</c> if none.
    /// </summary>
    internal static ScrollView? WalkToScrollView(Element? start)
    {
        for (var element = start?.Parent; element is not null; element = element.Parent)
        {
            if (element is ScrollView scrollView) return scrollView;
        }
        return null;
    }
}
