using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IOnboardingService
{
    OnboardingStep CurrentStep { get; }
    /// <summary>True when step is Welcome through PrayerTimeHighlight inclusive; false for None and Complete.</summary>
    bool IsActive { get; }
    /// <summary>True when the current session is a share-tutorial-only flow for existing users.</summary>
    bool IsShareTutorial { get; }
    /// <summary>In-memory only. Prevents welcome popup re-showing on each OnAppearing.</summary>
    bool WelcomeShownThisSession { get; }
    void Advance();
    void Skip();
    /// <summary>Clears persisted step key, sets OnboardingComplete=false, resets WelcomeShownThisSession, sets CurrentStep=Welcome in memory.</summary>
    void Reset();
    /// <summary>Called by MainPage immediately before showing the welcome popup. Sets WelcomeShownThisSession=true.</summary>
    void MarkWelcomeShown();
    event EventHandler StepChanged;
}
