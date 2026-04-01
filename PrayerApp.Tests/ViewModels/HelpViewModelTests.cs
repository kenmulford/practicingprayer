using NSubstitute;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class HelpViewModelTests
{
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();

    [Fact]
    public void Constructor_PopulatesFaqItems()
    {
        var sut = new HelpViewModel(_accessibilityService);

        Assert.NotEmpty(sut.FaqItems);
    }

    [Fact]
    public void FaqItems_ContainsExpectedCategories()
    {
        var sut = new HelpViewModel(_accessibilityService);
        var questions = sut.FaqItems.Select(f => f.Question).ToList();

        // Getting Started
        Assert.Contains(questions, q => q.Contains("prayer card", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(questions, q => q.Contains("prayer request", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(questions, q => q.Contains("Quick Add", StringComparison.OrdinalIgnoreCase));

        // Organization
        Assert.Contains(questions, q => q.Contains("tags", StringComparison.OrdinalIgnoreCase));

        // Prayer Time
        Assert.Contains(questions, q => q.Contains("Prayer Time", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(questions, q => q.Contains("auto-mode", StringComparison.OrdinalIgnoreCase));

        // Notifications
        Assert.Contains(questions, q => q.Contains("reminders", StringComparison.OrdinalIgnoreCase));

        // Data
        Assert.Contains(questions, q => q.Contains("back up", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(questions, q => q.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FaqItems_AllHaveNonEmptyAnswers()
    {
        var sut = new HelpViewModel(_accessibilityService);

        Assert.All(sut.FaqItems, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Question));
            Assert.False(string.IsNullOrWhiteSpace(item.Answer));
        });
    }

    [Fact]
    public void FaqItems_AllStartCollapsed()
    {
        var sut = new HelpViewModel(_accessibilityService);

        Assert.All(sut.FaqItems, item => Assert.False(item.IsExpanded));
    }

    [Fact]
    public void FaqItems_ToggleOneDoesNotAffectOthers()
    {
        var sut = new HelpViewModel(_accessibilityService);

        sut.FaqItems[0].ToggleCommand.Execute(null);

        Assert.True(sut.FaqItems[0].IsExpanded);
        Assert.All(sut.FaqItems.Skip(1), item => Assert.False(item.IsExpanded));
    }
}
