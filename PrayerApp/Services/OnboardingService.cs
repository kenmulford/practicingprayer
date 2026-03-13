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
        OnboardingStep.PrayerTime,
        OnboardingStep.PrayerTimeActive,
        OnboardingStep.Complete
    };

    private OnboardingStep _currentStep;

    public OnboardingStep CurrentStep => _currentStep;

    // True for Welcome through PrayerTimeActive inclusive; false for None and Complete
    public bool IsActive =>
        _currentStep != OnboardingStep.None &&
        _currentStep != OnboardingStep.Complete;

    public bool WelcomeShownThisSession { get; private set; }

    public event EventHandler? StepChanged;

    public OnboardingService()
    {
        var persisted = Preferences.Get(nameof(OnboardingStep), nameof(OnboardingStep.None));
        if (Enum.TryParse<OnboardingStep>(persisted, out var step))
            _currentStep = step;

        // First install: no persisted step + onboarding not complete → start at Welcome
        // Note: Settings.FirstRun is NOT referenced here (per spec).
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
        // Per spec: (1) clear persisted step key, (2) set OnboardingComplete=false,
        // (3) set WelcomeShownThisSession=false, (4) set in-memory CurrentStep=Welcome.
        // Welcome popup appears naturally on next MainPage.OnAppearing — NOT shown here.
        Preferences.Remove(nameof(OnboardingStep)); // key: nameof(OnboardingStep)
        Settings.OnboardingComplete = false;
        WelcomeShownThisSession = false;
        _currentStep = OnboardingStep.Welcome;
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkWelcomeShown()
    {
        WelcomeShownThisSession = true;
    }

    private void Persist()
    {
        // Persistence key: nameof(OnboardingStep) — consistent with Settings class convention
        // NOTE: Settings.OnboardingComplete is NOT set here. It is set exclusively in
        // OnboardingCompletePopup's Done button handler, which is the spec-defined write site.
        Preferences.Set(nameof(OnboardingStep), _currentStep.ToString());
    }
}
