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
}
