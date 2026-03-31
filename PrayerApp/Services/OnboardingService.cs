using PrayerApp.Models;

namespace PrayerApp.Services;

public class OnboardingService : IOnboardingService
{
    private static readonly OnboardingStep[] _sequence =
    {
        OnboardingStep.Welcome,
        OnboardingStep.CreateCard,
        OnboardingStep.NameCard,
        OnboardingStep.AddRequest,
        OnboardingStep.NameRequest,
        OnboardingStep.PrayerTimeHighlight,
        OnboardingStep.Complete
    };

    private OnboardingStep _currentStep;

    public OnboardingStep CurrentStep => _currentStep;

    public bool IsActive =>
        _currentStep != OnboardingStep.None &&
        _currentStep != OnboardingStep.Complete;

    public bool IsDeepLinkSession { get; private set; }

    public bool WelcomeShownThisSession { get; private set; }

    public event EventHandler? StepChanged;

    public OnboardingService()
    {
        var persisted = Preferences.Get(nameof(OnboardingStep), nameof(OnboardingStep.None));
        if (Enum.TryParse<OnboardingStep>(persisted, out var step))
            _currentStep = step;

        // Migration: all legacy steps → PrayerTimeHighlight
        if (_currentStep is OnboardingStep.PrayerTime or OnboardingStep.PrayerTimeActive
            or OnboardingStep.ShareIntro or OnboardingStep.SharePrayer)
            _currentStep = OnboardingStep.PrayerTimeHighlight;

        // First install: no persisted step + onboarding not complete → start at Welcome
        if (_currentStep == OnboardingStep.None && !Settings.OnboardingComplete)
            _currentStep = OnboardingStep.Welcome;
    }

    public void Advance()
    {
        if (!IsActive) return;

        var idx = Array.IndexOf(_sequence, _currentStep);
        if (idx < 0 || idx >= _sequence.Length - 1) return;

        _currentStep = _sequence[idx + 1];
        Persist();
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Skip()
    {
        if (_currentStep == OnboardingStep.Complete) return;
        _currentStep = OnboardingStep.Complete;
        Persist();
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        Preferences.Remove(nameof(OnboardingStep));
        Settings.OnboardingComplete = false;
        WelcomeShownThisSession = false;
        IsDeepLinkSession = false;
        _currentStep = OnboardingStep.Welcome;
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkWelcomeShown()
    {
        WelcomeShownThisSession = true;
    }

    public void MarkDeepLinkSession()
    {
        IsDeepLinkSession = true;
    }

    private void Persist()
    {
        Preferences.Set(nameof(OnboardingStep), _currentStep.ToString());
    }
}
