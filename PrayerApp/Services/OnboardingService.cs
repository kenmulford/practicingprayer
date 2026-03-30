using PrayerApp.Models;

namespace PrayerApp.Services;

public class OnboardingService : IOnboardingService
{
    // Full new-user sequence
    private static readonly OnboardingStep[] _sequence =
    {
        OnboardingStep.Welcome,
        OnboardingStep.CreateCard,
        OnboardingStep.NameCard,
        OnboardingStep.AddRequest,
        OnboardingStep.NameRequest,
        OnboardingStep.ShareIntro,
        OnboardingStep.SharePrayer,
        OnboardingStep.PrayerTimeHighlight,
        OnboardingStep.Complete
    };

    // Existing-user share tutorial sequence (subset)
    private static readonly OnboardingStep[] _shareTutorialSequence =
    {
        OnboardingStep.Welcome,       // "New Feature" popup
        OnboardingStep.ShareIntro,
        OnboardingStep.SharePrayer,
        OnboardingStep.PrayerTimeHighlight,
        OnboardingStep.Complete
    };

    private OnboardingStep _currentStep;

    public OnboardingStep CurrentStep => _currentStep;

    // True for Welcome through PrayerTimeHighlight inclusive; false for None and Complete
    public bool IsActive =>
        _currentStep != OnboardingStep.None &&
        _currentStep != OnboardingStep.Complete;

    /// <summary>True when the current session is a share-tutorial-only flow for existing users.</summary>
    public bool IsShareTutorial { get; private set; }

    public bool WelcomeShownThisSession { get; private set; }

    public event EventHandler? StepChanged;

    public OnboardingService()
    {
        var persisted = Preferences.Get(nameof(OnboardingStep), nameof(OnboardingStep.None));
        if (Enum.TryParse<OnboardingStep>(persisted, out var step))
            _currentStep = step;

        // Migration: old PrayerTime/PrayerTimeActive steps → ShareIntro
        if (_currentStep == OnboardingStep.PrayerTime || _currentStep == OnboardingStep.PrayerTimeActive)
            _currentStep = OnboardingStep.ShareIntro;

        // First install: no persisted step + onboarding not complete → start at Welcome
        if (_currentStep == OnboardingStep.None && !Settings.OnboardingComplete)
            _currentStep = OnboardingStep.Welcome;

        // Existing user who completed onboarding but hasn't seen share tutorial
        if (_currentStep == OnboardingStep.None && Settings.OnboardingComplete && !Settings.ShareTutorialShown)
        {
            _currentStep = OnboardingStep.Welcome;
            IsShareTutorial = true;
        }
    }

    public void Advance()
    {
        if (!IsActive) return;

        var seq = IsShareTutorial ? _shareTutorialSequence : _sequence;
        var idx = Array.IndexOf(seq, _currentStep);
        if (idx < 0 || idx >= seq.Length - 1) return;

        _currentStep = seq[idx + 1];
        Persist();
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Skip()
    {
        if (_currentStep == OnboardingStep.Complete) return;
        _currentStep = OnboardingStep.Complete;

        // Mark share tutorial as shown so it doesn't re-trigger
        if (IsShareTutorial)
            Settings.ShareTutorialShown = true;

        Persist();
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        Preferences.Remove(nameof(OnboardingStep));
        Settings.OnboardingComplete = false;
        Settings.ShareTutorialShown = false;
        WelcomeShownThisSession = false;
        IsShareTutorial = false;
        _currentStep = OnboardingStep.Welcome;
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkWelcomeShown()
    {
        WelcomeShownThisSession = true;
    }

    private void Persist()
    {
        Preferences.Set(nameof(OnboardingStep), _currentStep.ToString());
    }
}
