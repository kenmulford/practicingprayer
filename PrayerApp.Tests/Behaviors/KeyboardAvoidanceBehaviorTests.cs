using Microsoft.Maui.Controls;
using PrayerApp.Behaviors;

namespace PrayerApp.Tests.Behaviors;

/// <summary>
/// xUnit coverage for <see cref="KeyboardAvoidanceBehavior"/>.
/// Focused on BindableProperty defaults and the static
/// <c>WalkToScrollView</c> ancestor walker. Behavior interaction with the
/// MAUI handler is covered manually on-device — there is no test harness
/// for animated <see cref="ScrollView.ScrollToAsync(Element, ScrollToPosition, bool)"/>
/// in unit tests.
/// </summary>
public class KeyboardAvoidanceBehaviorTests
{
    [Fact]
    public void Margin_DefaultsTo40()
    {
        var sut = new KeyboardAvoidanceBehavior();
        Assert.Equal(40.0, sut.Margin);
    }

    [Fact]
    public void Animate_DefaultsToTrue()
    {
        var sut = new KeyboardAvoidanceBehavior();
        Assert.True(sut.Animate);
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        var sut = new KeyboardAvoidanceBehavior();
        Assert.True(sut.IsEnabled);
    }

    [Fact]
    public void WalkToScrollView_ReturnsNull_WhenNoScrollViewAncestor()
    {
        var entry = new Entry();
        var stack = new VerticalStackLayout();
        stack.Add(entry);

        var result = KeyboardAvoidanceBehavior.WalkToScrollView(entry);

        Assert.Null(result);
    }

    [Fact]
    public void WalkToScrollView_ReturnsNearestScrollView()
    {
        var entry = new Entry();
        var stack = new VerticalStackLayout();
        stack.Add(entry);
        var scroll = new ScrollView { Content = stack };

        var result = KeyboardAvoidanceBehavior.WalkToScrollView(entry);

        Assert.Same(scroll, result);
    }

    [Fact]
    public async Task ScrollIfNeededAsync_DoesNothing_WhenIsEnabledFalse()
    {
        // Arrange — input not attached to any ScrollView; with IsEnabled=true
        // this path also returns no-op (no ancestor), so to isolate the
        // IsEnabled gate we attach the behavior and flip it before invoking.
        var entry = new Entry();
        var sut = new KeyboardAvoidanceBehavior { IsEnabled = false };
        entry.Behaviors.Add(sut);

        // Act + Assert — should complete synchronously, throw nothing.
        await sut.ScrollIfNeededAsync();
    }

    [Fact]
    public async Task ScrollIfNeededAsync_DoesNotRequestScroll_WhenIsEnabledFalse()
    {
        // Companion to the positive-path test below: with the gate OFF, focusing
        // must NOT ask the ScrollView to scroll. The contrast between this and
        // ScrollIfNeededAsync_RequestsScroll_WhenEnabledAndHasScrollViewAncestor
        // is what makes the positive test a genuine guard rather than a tautology.
        var entry = new Entry();
        var stack = new VerticalStackLayout();
        stack.Add(entry);
        var scroll = new ScrollView { Content = stack };
        var sut = new KeyboardAvoidanceBehavior { IsEnabled = false };
        entry.Behaviors.Add(sut);

        var requested = false;
        scroll.ScrollToRequested += (_, _) => requested = true;

        await sut.ScrollIfNeededAsync();

        Assert.False(requested);
    }

    [Fact]
    public async Task ScrollIfNeededAsync_RequestsScroll_WhenEnabledAndHasScrollViewAncestor()
    {
        // Regression guard for Issue #54: KeyboardAvoidanceBehavior must scroll the
        // focused Entry into view inside its ScrollView ancestor so the keyboard
        // never occludes it (e.g. the ConfirmImportPage Card Title Entry, xaml:159).
        // The on-device pixel-band approach could not demonstrate this on a Pixel 9 /
        // API 36 portrait emulator — the short form keeps the field above the keyboard
        // even with the behavior removed, so there was no observable delta to assert.
        // This unit test guards the behavior's contract directly and deterministically:
        // when enabled and parented by a ScrollView, focusing requests a scroll.
        var entry = new Entry();
        var stack = new VerticalStackLayout();
        stack.Add(entry);
        var scroll = new ScrollView { Content = stack };
        var sut = new KeyboardAvoidanceBehavior(); // IsEnabled defaults true
        entry.Behaviors.Add(sut);

        // ScrollView.ScrollToAsync raises ScrollToRequested synchronously, then awaits
        // a platform "scroll finished" signal that never arrives without a handler — so
        // the behavior's awaited ScrollToAsync never completes in a unit test. Observe
        // the synchronous request event instead of awaiting completion; fire-and-forget
        // the (intentionally non-completing) scroll task.
        var requested = new TaskCompletionSource();
        scroll.ScrollToRequested += (_, _) => requested.TrySetResult();

        _ = sut.ScrollIfNeededAsync();

        var fired = await Task.WhenAny(requested.Task, Task.Delay(2000)) == requested.Task;
        Assert.True(fired,
            "Expected KeyboardAvoidanceBehavior to request a scroll on the ScrollView ancestor " +
            "when enabled — the Issue #54 regression is the behavior NOT scrolling the focused field.");
    }
}
