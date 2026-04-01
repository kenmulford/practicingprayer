using NSubstitute;
using PrayerApp.Services;
using PrayerApp.ViewModels;

namespace PrayerApp.Tests.ViewModels;

public class FaqItemViewModelTests
{
    private readonly IAccessibilityService _accessibilityService = Substitute.For<IAccessibilityService>();

    [Fact]
    public void Constructor_SetsQuestionAndAnswer()
    {
        var sut = new FaqItemViewModel("Why?", "Because.", _accessibilityService);

        Assert.Equal("Why?", sut.Question);
        Assert.Equal("Because.", sut.Answer);
    }

    [Fact]
    public void IsExpanded_DefaultsFalse()
    {
        var sut = new FaqItemViewModel("Q", "A", _accessibilityService);

        Assert.False(sut.IsExpanded);
    }

    [Fact]
    public void ToggleCommand_TogglesIsExpanded()
    {
        var sut = new FaqItemViewModel("Q", "A", _accessibilityService);

        sut.ToggleCommand.Execute(null);
        Assert.True(sut.IsExpanded);

        sut.ToggleCommand.Execute(null);
        Assert.False(sut.IsExpanded);
    }

    [Fact]
    public void ToggleCommand_WhenExpanding_AnnouncesAnswer()
    {
        var sut = new FaqItemViewModel("Q", "The answer text", _accessibilityService);

        sut.ToggleCommand.Execute(null);

        _accessibilityService.Received(1).Announce("The answer text");
    }

    [Fact]
    public void ToggleCommand_WhenCollapsing_DoesNotAnnounce()
    {
        var sut = new FaqItemViewModel("Q", "A", _accessibilityService);
        sut.ToggleCommand.Execute(null); // expand
        _accessibilityService.ClearReceivedCalls();

        sut.ToggleCommand.Execute(null); // collapse

        _accessibilityService.DidNotReceive().Announce(Arg.Any<string>());
    }

    [Fact]
    public void ToggleCommand_NullAccessibilityService_DoesNotThrow()
    {
        var sut = new FaqItemViewModel("Q", "A", accessibilityService: null);

        var ex = Record.Exception(() => sut.ToggleCommand.Execute(null));

        Assert.Null(ex);
        Assert.True(sut.IsExpanded);
    }

    [Fact]
    public void IsExpanded_RaisesPropertyChanged()
    {
        var sut = new FaqItemViewModel("Q", "A", _accessibilityService);
        var raised = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FaqItemViewModel.IsExpanded))
                raised = true;
        };

        sut.ToggleCommand.Execute(null);

        Assert.True(raised);
    }
}
