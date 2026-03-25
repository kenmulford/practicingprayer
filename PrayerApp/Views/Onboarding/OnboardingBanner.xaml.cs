// PrayerApp/Views/Onboarding/OnboardingBanner.xaml.cs
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Views.Onboarding;

public partial class OnboardingBanner : ContentView
{
    private IOnboardingService? _onboardingService;

    // ── Bindable properties ───────────────────────────────────────────────────

    public static readonly BindableProperty ExpectedStepProperty =
        BindableProperty.Create(nameof(ExpectedStep), typeof(OnboardingStep), typeof(OnboardingBanner),
            OnboardingStep.None, propertyChanged: (b, _, _) => ((OnboardingBanner)b).UpdateVisibility());

    public static readonly BindableProperty HeadlineTextProperty =
        BindableProperty.Create(nameof(HeadlineText), typeof(string), typeof(OnboardingBanner), string.Empty,
            propertyChanged: (b, _, n) => ((OnboardingBanner)b).HeadlineLabel.Text = (string)n);

    public static readonly BindableProperty SubTextProperty =
        BindableProperty.Create(nameof(SubText), typeof(string), typeof(OnboardingBanner), string.Empty,
            propertyChanged: (b, _, n) =>
            {
                var banner = (OnboardingBanner)b;
                banner.SubLabel.Text = (string)n;
                banner.SubLabel.IsVisible = !string.IsNullOrWhiteSpace((string)n);
            });

    public OnboardingStep ExpectedStep
    {
        get => (OnboardingStep)GetValue(ExpectedStepProperty);
        set => SetValue(ExpectedStepProperty, value);
    }

    public string HeadlineText
    {
        get => (string)GetValue(HeadlineTextProperty);
        set => SetValue(HeadlineTextProperty, value);
    }

    public string SubText
    {
        get => (string)GetValue(SubTextProperty);
        set => SetValue(SubTextProperty, value);
    }

    public OnboardingBanner()
    {
        InitializeComponent();
        SkipButton.Clicked += (_, _) => _onboardingService?.Skip();
    }

    // Subscribe when handler is attached (unsubscribe first to prevent double-subscribe on re-parent)
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Unsubscribe from any previous service instance
        if (_onboardingService is not null)
            _onboardingService.StepChanged -= OnStepChanged;

        _onboardingService = IPlatformApplication.Current?.Services
            .GetService<IOnboardingService>();

        if (_onboardingService is not null)
            _onboardingService.StepChanged += OnStepChanged;

        UpdateVisibility();
    }

    // Unsubscribe in OnHandlerChanging (fires before handler is replaced — correct MAUI teardown point)
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (_onboardingService is not null)
            _onboardingService.StepChanged -= OnStepChanged;
    }

    private void OnStepChanged(object? sender, EventArgs e) => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (_onboardingService is null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = _onboardingService.IsActive &&
                    _onboardingService.CurrentStep == ExpectedStep;

        if (IsVisible)
            UpdateStepLabel();
    }

    private void UpdateStepLabel()
    {
        var stepNumber = ExpectedStep switch
        {
            OnboardingStep.CreateCard or OnboardingStep.NameCard => 1,
            OnboardingStep.AddRequest or OnboardingStep.NameRequest => 2,
            OnboardingStep.PrayerTime or OnboardingStep.PrayerTimeActive => 3,
            _ => 0
        };
        StepLabel.Text = stepNumber > 0 ? $"STEP {stepNumber} OF 3" : string.Empty;
    }
}
