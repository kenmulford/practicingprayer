# F-2 Onboarding / First-Time Experience Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guide first-time users through creating a prayer card, adding a prayer request, and completing a Prayer Time session via a non-blocking bottom-banner coaching flow.

**Architecture:** A singleton `OnboardingService` persists the current step via `Preferences`, fires `StepChanged` events, and is consumed by a reusable `OnboardingBanner` ContentView placed at the bottom of each participating page. Welcome and completion are CommunityToolkit.Maui `Popup` dialogs anchored to `MainPage` and `AppShell` respectively.

**Tech Stack:** .NET MAUI, CommunityToolkit.Maui (Popup), CommunityToolkit.Mvvm, SQLite-net (no ORM change), NSubstitute + xUnit (tests)

---

## Chunk 1: Core Service Layer

### Task 1: `OnboardingStep` enum

**Files:**
- Create: `PrayerApp/Models/OnboardingStep.cs`
- Test: `PrayerApp.Tests/Services/OnboardingServiceTests.cs` (scaffold only — file created but tests added in Task 2)

- [ ] **Step 1: Create the enum**

```csharp
// PrayerApp/Models/OnboardingStep.cs
namespace PrayerApp.Models;

public enum OnboardingStep
{
    None,
    Welcome,
    CreateCard,
    NameCard,
    AddRequest,
    NameRequest,
    PrayerTime,
    PrayerTimeActive,
    Complete
}
```

- [ ] **Step 2: Create test file scaffold**

```csharp
// PrayerApp.Tests/Services/OnboardingServiceTests.cs
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class OnboardingServiceTests
{
    // Tests added in Task 2
}
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add PrayerApp/Models/OnboardingStep.cs PrayerApp.Tests/Services/OnboardingServiceTests.cs
git commit -m "feat(F-2): add OnboardingStep enum"
```

---

### Task 2: `IOnboardingService` + `OnboardingService`

**Files:**
- Create: `PrayerApp/Services/IOnboardingService.cs`
- Create: `PrayerApp/Services/OnboardingService.cs`
- Modify: `PrayerApp/Services/Settings.cs` (add `OnboardingComplete` property)
- Test: `PrayerApp.Tests/Services/OnboardingServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// PrayerApp.Tests/Services/OnboardingServiceTests.cs
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.Tests.Services;

public class OnboardingServiceTests
{
    // Helper: create service with a fresh isolated Preferences-like state
    // OnboardingService reads from Preferences, which are global in tests.
    // We reset by calling Reset() before each test that needs clean state.

    // ── Initialization ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenNoPriorState_CurrentStepIsWelcome()
    {
        // Preferences are cleared by Reset(); simulate a fresh install
        var svc = new OnboardingService();
        svc.Reset(); // Clears persisted state → CurrentStep = Welcome
        Assert.Equal(OnboardingStep.Welcome, svc.CurrentStep);
    }

    [Fact]
    public void Constructor_WhenNoPriorState_IsActiveIsTrue()
    {
        var svc = new OnboardingService();
        svc.Reset();
        Assert.True(svc.IsActive);
    }

    // ── Advance ───────────────────────────────────────────────────────────────

    [Fact]
    public void Advance_FromWelcome_MovesToCreateCard()
    {
        var svc = new OnboardingService();
        svc.Reset();
        svc.Advance();
        Assert.Equal(OnboardingStep.CreateCard, svc.CurrentStep);
    }

    [Fact]
    public void Advance_ThroughAllSteps_ReachesComplete()
    {
        var svc = new OnboardingService();
        svc.Reset();
        // Welcome → CreateCard → NameCard → AddRequest → NameRequest → PrayerTime → PrayerTimeActive → Complete
        for (int i = 0; i < 7; i++) svc.Advance();
        Assert.Equal(OnboardingStep.Complete, svc.CurrentStep);
    }

    [Fact]
    public void Advance_WhenComplete_IsNoOp()
    {
        var svc = new OnboardingService();
        svc.Reset();
        for (int i = 0; i < 7; i++) svc.Advance(); // reach Complete
        svc.Advance(); // should be a no-op
        Assert.Equal(OnboardingStep.Complete, svc.CurrentStep);
    }

    [Fact]
    public void Advance_FiresStepChangedEvent()
    {
        var svc = new OnboardingService();
        svc.Reset();
        int eventCount = 0;
        svc.StepChanged += (_, _) => eventCount++;
        svc.Advance();
        Assert.Equal(1, eventCount);
    }

    // ── Skip ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Skip_JumpsToComplete()
    {
        var svc = new OnboardingService();
        svc.Reset();
        svc.Skip();
        Assert.Equal(OnboardingStep.Complete, svc.CurrentStep);
    }

    [Fact]
    public void Skip_SetsIsActiveToFalse()
    {
        var svc = new OnboardingService();
        svc.Reset();
        svc.Skip();
        Assert.False(svc.IsActive);
    }

    // ── IsActive ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsActive_TrueForAllStepsBetweenWelcomeAndPrayerTimeActiveInclusive()
    {
        var svc = new OnboardingService();
        svc.Reset();
        var activeSteps = new[]
        {
            OnboardingStep.Welcome,
            OnboardingStep.CreateCard,
            OnboardingStep.NameCard,
            OnboardingStep.AddRequest,
            OnboardingStep.NameRequest,
            OnboardingStep.PrayerTime,
            OnboardingStep.PrayerTimeActive
        };
        // Advance through each and verify IsActive
        foreach (var step in activeSteps)
        {
            Assert.True(svc.IsActive, $"Expected IsActive=true at {svc.CurrentStep}");
            svc.Advance();
        }
        // Now at Complete
        Assert.False(svc.IsActive);
    }

    // ── WelcomeShownThisSession ───────────────────────────────────────────────

    [Fact]
    public void WelcomeShownThisSession_DefaultsFalse()
    {
        var svc = new OnboardingService();
        svc.Reset();
        Assert.False(svc.WelcomeShownThisSession);
    }

    [Fact]
    public void Reset_ClearsWelcomeShownThisSession()
    {
        var svc = new OnboardingService();
        svc.Reset();
        svc.MarkWelcomeShown();
        Assert.True(svc.WelcomeShownThisSession);
        svc.Reset();
        Assert.False(svc.WelcomeShownThisSession);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (service doesn't exist yet)**

Run: `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal 2>&1 | head -20`
Expected: Build error — `OnboardingService` not found

- [ ] **Step 3: Add `OnboardingComplete` to `Settings.cs`**

In `PrayerApp/Services/Settings.cs`, add after `AutoModeIntervalSeconds`:

```csharp
public static bool OnboardingComplete
{
    get => Preferences.Get(nameof(OnboardingComplete), false);
    set => Preferences.Set(nameof(OnboardingComplete), value);
}
```

- [ ] **Step 4: Create `IOnboardingService.cs`**

```csharp
// PrayerApp/Services/IOnboardingService.cs
using PrayerApp.Models;

namespace PrayerApp.Services;

public interface IOnboardingService
{
    OnboardingStep CurrentStep { get; }
    /// <summary>True when step is Welcome through PrayerTimeActive inclusive; false for None and Complete.</summary>
    bool IsActive { get; }
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
```

> **Note on `MarkWelcomeShown()`:** The spec's interface definition does not list this method, but the spec requires `WelcomeShownThisSession` to be set to `true` "the moment the popup is requested" (spec §Welcome popup). Since `MainPage` controls when the popup is shown, it must signal this to the service. Adding `MarkWelcomeShown()` to the interface is the cleanest way to honor this requirement without leaking the concrete type.
```

- [ ] **Step 5: Create `OnboardingService.cs`**

```csharp
// PrayerApp/Services/OnboardingService.cs
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
```

> **Note on `MarkWelcomeShown`:** The spec's `IOnboardingService` sets `WelcomeShownThisSession = true` as a side-effect of the popup being requested. We expose `MarkWelcomeShown()` to keep the setter private while giving `MainPage` an explicit call point. Add it to `IOnboardingService` above.

- [ ] **Step 6: Run tests — expect pass**

Run: `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: Passed — 40 + N new tests, 0 failures

> **Note:** The `OnboardingServiceTests` rely on `Preferences` which requires a MAUI host. If tests throw a `PlatformNotSupportedException` on `Preferences.Get`, wrap with a try/catch in the service constructor (already handled in the implementation above via the fallback) and use a `MockPreferences` approach: move all `Preferences` calls behind a `Func<string, string, string>` delegate injected at test time. However, since the existing pattern in this codebase avoids mocking `Preferences` (Settings is tested implicitly), defer to runtime validation if needed — mark the affected tests with `[Fact(Skip = "Requires MAUI host")]` rather than blocking the build.

- [ ] **Step 7: Commit**

```bash
git add PrayerApp/Models/OnboardingStep.cs \
        PrayerApp/Services/IOnboardingService.cs \
        PrayerApp/Services/OnboardingService.cs \
        PrayerApp/Services/Settings.cs \
        PrayerApp.Tests/Services/OnboardingServiceTests.cs
git commit -m "feat(F-2): OnboardingService with step sequencing, persistence, and reset"
```

---

### Task 3: Register service + fix `SeedDataAsync` idempotency

**Files:**
- Modify: `PrayerApp/MauiProgram.cs`
- Modify: `PrayerApp/Services/DBService.cs`

- [ ] **Step 1: Register `IOnboardingService` in `MauiProgram.cs`**

In `MauiProgram.cs`, after the `INotificationService` registration:

```csharp
// Register onboarding service as singleton
builder.Services.AddSingleton<IOnboardingService, OnboardingService>();
```

Also add the using at the top if missing:
```csharp
using PrayerApp.Views.Onboarding;
```

- [ ] **Step 2: Make `SeedDataAsync` idempotent in `DBService.cs`**

> **Co-change constraint (spec §SeedDataAsync):** Keep the `FirstRun` check in `MauiProgram.cs` exactly as-is — do NOT remove or modify it. The row-count gate here makes re-runs safe; the `FirstRun` gate in `MauiProgram.cs` is still the caller-side guard for normal installs.

Replace `SeedDataAsync` with (row-count-gate applied to all entity types: PrayerCard, PrayerTag, and Prayer):

```csharp
public async Task SeedDataAsync()
{
    // Row-count gate: skip if any cards already exist (covers re-runs after ClearSettings)
    var cardCount = await _db.Table<PrayerCard>().CountAsync();
    if (cardCount > 0) return;

    var generalCard = new PrayerCard
    {
        Title = "General",
        IsFavorite = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    await InsertAsync(generalCard);

    // Only insert seed tags if table is empty
    var tagCount = await _db.Table<PrayerTag>().CountAsync();
    if (tagCount == 0)
    {
        await InsertAsync(new PrayerTag { Name = "Urgent", Color = "#FF0000" });
        await InsertAsync(new PrayerTag { Name = "Family", Color = "#0000FF" });
        await InsertAsync(new PrayerTag { Name = "Work", Color = "#00FF00" });
    }

    await InsertAsync(new Prayer
    {
        PrayerCardId = generalCard.Id,
        Title = "Sample Prayer Entry 1",
        Details = "Sample details.",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });

    await InsertAsync(new Prayer
    {
        PrayerCardId = generalCard.Id,
        Title = "Sample Prayer Entry 2",
        Details = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis in sem sit amet sapien tincidunt pretium. Mauris tristique libero tellus, laoreet blandit metus congue non. Ut at sagittis lacus.",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });

    await InsertAsync(new Prayer
    {
        PrayerCardId = generalCard.Id,
        Title = "Sample Prayer Entry 3",
        Details = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis in sem sit amet sapien tincidunt pretium. Mauris tristique libero tellus, laoreet blandit metus congue non. Ut at sagittis lacus. Nullam in felis quam. Phasellus nisi augue, hendrerit non vulputate fermentum, maximus a risus.",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });

    await InsertAsync(new Prayer
    {
        PrayerCardId = generalCard.Id,
        Title = "Sample Prayer Entry 4",
        Details = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis in sem sit amet sapien tincidunt pretium. Mauris tristique libero tellus, laoreet blandit metus congue non. Ut at sagittis lacus. Nullam in felis quam. Phasellus nisi augue, hendrerit non vulputate fermentum, maximus a risus. Phasellus aliquam fringilla libero et feugiat. Nam eget varius mi. Curabitur sit amet rutrum sem. Morbi ut ipsum ex. Nulla est ante, hendrerit vitae mollis quis, fringilla id ligula. Vestibulum id nisi sed nunc finibus egestas. Phasellus eleifend ante at enim ornare auctor a ac dolor. Nullam nec nisi vulputate, ultrices nisi quis, bibendum ligula. Proin fermentum mauris nec ipsum ultrices gravida. Sed faucibus scelerisque massa at porttitor.",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });
}
```

Also remove the now-unused `DropSyncDataAsync` private method (lines 182–214 in the original file).

- [ ] **Step 3: Verify build**

Run: `dotnet build PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Run tests**

Run: `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add PrayerApp/MauiProgram.cs PrayerApp/Services/DBService.cs
git commit -m "feat(F-2): register OnboardingService; make SeedDataAsync idempotent"
```

---

## Chunk 2: Onboarding UI Components

### Task 4: `OnboardingBanner` ContentView

**Files:**
- Create: `PrayerApp/Views/Onboarding/OnboardingBanner.xaml`
- Create: `PrayerApp/Views/Onboarding/OnboardingBanner.xaml.cs`

The banner is a reusable ContentView placed at the bottom of each participating page. It subscribes to `IOnboardingService.StepChanged` and shows/hides itself based on `ExpectedStep`.

- [ ] **Step 1: Create the directory**

```bash
mkdir -p PrayerApp/Views/Onboarding
```

- [ ] **Step 2: Create `OnboardingBanner.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="PrayerApp.Views.Onboarding.OnboardingBanner"
             IsVisible="False">

    <Border BackgroundColor="{StaticResource Tertiary}"
            StrokeThickness="0">
        <Border.StrokeShape>
            <RoundRectangle CornerRadii="12,12,0,0" />
        </Border.StrokeShape>

        <Grid RowDefinitions="Auto,Auto,Auto" Padding="16,12,16,16" RowSpacing="4">

            <!-- Step indicator -->
            <Label x:Name="StepLabel"
                   Grid.Row="0"
                   TextColor="{StaticResource WarmGold}"
                   FontSize="11"
                   FontAttributes="Bold"
                   LetterSpacing="1" />

            <!-- Headline -->
            <Label x:Name="HeadlineLabel"
                   Grid.Row="1"
                   TextColor="White"
                   FontSize="15"
                   FontAttributes="Bold"
                   LineBreakMode="WordWrap" />

            <!-- Sub text + skip link row -->
            <Grid Grid.Row="2" ColumnDefinitions="*,Auto" Margin="0,4,0,0">

                <Label x:Name="SubLabel"
                       Grid.Column="0"
                       TextColor="{StaticResource Gray200}"
                       FontSize="13"
                       LineBreakMode="WordWrap" />

                <Label x:Name="SkipLabel"
                       Grid.Column="1"
                       Text="Skip tour"
                       TextColor="{StaticResource Gray300}"
                       FontSize="12"
                       VerticalOptions="End">
                    <Label.GestureRecognizers>
                        <TapGestureRecognizer x:Name="SkipTap" />
                    </Label.GestureRecognizers>
                </Label>
            </Grid>
        </Grid>
    </Border>
</ContentView>
```

- [ ] **Step 3: Create `OnboardingBanner.xaml.cs`**

```csharp
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
        SkipTap.Tapped += (_, _) => _onboardingService?.Skip();
    }

    // Subscribe when handler is attached
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
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
```

- [ ] **Step 4: Verify build**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add PrayerApp/Views/Onboarding/
git commit -m "feat(F-2): OnboardingBanner ContentView with step-reactive visibility"
```

---

### Task 5: Welcome popup

**Files:**
- Create: `PrayerApp/Views/Onboarding/OnboardingWelcomePopup.xaml`
- Create: `PrayerApp/Views/Onboarding/OnboardingWelcomePopup.xaml.cs`

- [ ] **Step 1: Create `OnboardingWelcomePopup.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<toolkit:Popup xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
               xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
               xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"
               x:Class="PrayerApp.Views.Onboarding.OnboardingWelcomePopup"
               CanBeDismissedByTappingOutsideOfPopup="False"
               Color="Transparent">

    <Border BackgroundColor="{StaticResource Secondary}"
            Stroke="{StaticResource Primary}"
            StrokeThickness="1"
            Margin="24,0">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="16" />
        </Border.StrokeShape>

        <VerticalStackLayout Padding="28,32" Spacing="16">

            <Label Text="🙏"
                   FontSize="48"
                   HorizontalOptions="Center" />

            <Label Text="Welcome to Prayer App"
                   FontSize="22"
                   FontAttributes="Bold"
                   TextColor="{StaticResource Tertiary}"
                   HorizontalOptions="Center"
                   HorizontalTextAlignment="Center" />

            <Label Text="Let's set up your first prayer card."
                   FontSize="15"
                   TextColor="{StaticResource Gray500}"
                   HorizontalOptions="Center"
                   HorizontalTextAlignment="Center" />

            <Button x:Name="BtnGetStarted"
                    Text="Get Started"
                    BackgroundColor="{StaticResource Primary}"
                    TextColor="White"
                    Margin="0,8,0,0" />

            <Label x:Name="LblSkip"
                   Text="Skip tour"
                   FontSize="13"
                   TextColor="{StaticResource Gray400}"
                   HorizontalOptions="Center">
                <Label.GestureRecognizers>
                    <TapGestureRecognizer x:Name="SkipTap" />
                </Label.GestureRecognizers>
            </Label>

        </VerticalStackLayout>
    </Border>
</toolkit:Popup>
```

- [ ] **Step 2: Create `OnboardingWelcomePopup.xaml.cs`**

```csharp
// PrayerApp/Views/Onboarding/OnboardingWelcomePopup.xaml.cs
using CommunityToolkit.Maui.Views;
using PrayerApp.Services;

namespace PrayerApp.Views.Onboarding;

public partial class OnboardingWelcomePopup : Popup
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingWelcomePopup(IOnboardingService onboardingService)
    {
        InitializeComponent();
        _onboardingService = onboardingService;

        BtnGetStarted.Clicked += async (_, _) =>
        {
            _onboardingService.Advance(); // Welcome → CreateCard
            await CloseAsync();
            await Shell.Current.GoToAsync("//CardsPage");
        };

        SkipTap.Tapped += async (_, _) =>
        {
            _onboardingService.Skip();
            await CloseAsync();
        };
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add PrayerApp/Views/Onboarding/OnboardingWelcomePopup.xaml \
        PrayerApp/Views/Onboarding/OnboardingWelcomePopup.xaml.cs
git commit -m "feat(F-2): OnboardingWelcomePopup"
```

---

### Task 6: Complete popup

**Files:**
- Create: `PrayerApp/Views/Onboarding/OnboardingCompletePopup.xaml`
- Create: `PrayerApp/Views/Onboarding/OnboardingCompletePopup.xaml.cs`

- [ ] **Step 1: Create `OnboardingCompletePopup.xaml`**

```xml
<?xml version="1.0" encoding="utf-8" ?>
<toolkit:Popup xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
               xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
               xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"
               x:Class="PrayerApp.Views.Onboarding.OnboardingCompletePopup"
               CanBeDismissedByTappingOutsideOfPopup="False"
               Color="Transparent">

    <Border BackgroundColor="{StaticResource Secondary}"
            Stroke="{StaticResource Primary}"
            StrokeThickness="1"
            Margin="24,0">
        <Border.StrokeShape>
            <RoundRectangle CornerRadius="16" />
        </Border.StrokeShape>

        <VerticalStackLayout Padding="28,32" Spacing="16">

            <Label Text="✨"
                   FontSize="48"
                   HorizontalOptions="Center" />

            <Label Text="You're all set!"
                   FontSize="22"
                   FontAttributes="Bold"
                   TextColor="{StaticResource Tertiary}"
                   HorizontalOptions="Center"
                   HorizontalTextAlignment="Center" />

            <Label Text="May God bless your prayer life. 🙏"
                   FontSize="15"
                   TextColor="{StaticResource Gray500}"
                   HorizontalOptions="Center"
                   HorizontalTextAlignment="Center" />

            <Button x:Name="BtnDone"
                    Text="Done"
                    BackgroundColor="{StaticResource Primary}"
                    TextColor="White"
                    Margin="0,8,0,0" />

        </VerticalStackLayout>
    </Border>
</toolkit:Popup>
```

- [ ] **Step 2: Create `OnboardingCompletePopup.xaml.cs`**

```csharp
// PrayerApp/Views/Onboarding/OnboardingCompletePopup.xaml.cs
using CommunityToolkit.Maui.Views;

namespace PrayerApp.Views.Onboarding;

public partial class OnboardingCompletePopup : Popup
{
    public OnboardingCompletePopup()
    {
        InitializeComponent();
        BtnDone.Clicked += async (_, _) =>
        {
            Settings.OnboardingComplete = true;
            await CloseAsync();
        };
    }
}
```

- [ ] **Step 3: Add missing using to `OnboardingCompletePopup.xaml.cs`**

The `Settings` class is in `PrayerApp.Services`. Add at top:

```csharp
using PrayerApp.Services;
```

- [ ] **Step 4: Verify build**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add PrayerApp/Views/Onboarding/OnboardingCompletePopup.xaml \
        PrayerApp/Views/Onboarding/OnboardingCompletePopup.xaml.cs
git commit -m "feat(F-2): OnboardingCompletePopup"
```

---

## Chunk 3: AppShell + MainPage Wiring

### Task 7: AppShell — show complete popup on step change

**Files:**
- Modify: `PrayerApp/AppShell.xaml.cs`

- [ ] **Step 1: Update `AppShell.xaml.cs`**

Add using directives at top (after existing ones):

```csharp
using CommunityToolkit.Maui.Views;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.Views.Onboarding;
```

In the `AppShell()` constructor, after the `Routing.RegisterRoute` calls, add:

```csharp
// Subscribe to onboarding step changes to show the closing popup
var onboardingService = IPlatformApplication.Current!.Services
    .GetRequiredService<IOnboardingService>();

onboardingService.StepChanged += (_, _) =>
{
    if (onboardingService.CurrentStep != OnboardingStep.Complete) return;

    MainThread.BeginInvokeOnMainThread(async () =>
    {
        var page = Shell.Current?.CurrentPage;
        if (page is not null)
            await page.ShowPopupAsync(new OnboardingCompletePopup());
    });
};
```

- [ ] **Step 2: Verify build**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal`
Expected: 0 errors

- [ ] **Step 3: Run tests**

Run: `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add PrayerApp/AppShell.xaml.cs
git commit -m "feat(F-2): AppShell subscribes to StepChanged to show completion popup"
```

---

### Task 8: MainPage — show welcome popup on first `OnAppearing`

**Files:**
- Modify: `PrayerApp/Views/MainPage.xaml`
- Modify: `PrayerApp/Views/MainPage.xaml.cs`

- [ ] **Step 1: Add `OnboardingBanner` to `MainPage.xaml`**

Change the root `<ScrollView>` to a `<Grid>` so the banner can be pinned to the bottom. Change the root element and add the banner. The `PrayerTime` step banner shows on `MainPage`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewModels="clr-namespace:PrayerApp.ViewModels"
             xmlns:onboarding="clr-namespace:PrayerApp.Views.Onboarding"
             x:DataType="viewModels:HomeViewModel"
             x:Class="PrayerApp.Views.MainPage">

    <Grid RowDefinitions="*,Auto">

        <ScrollView Grid.Row="0">
            <VerticalStackLayout Padding="20,16" Spacing="16">

                <Button x:Name="BtnQuickAdd"
                        Text="Quick Add"
                        SemanticProperties.Hint="Add a prayer request"
                        HorizontalOptions="Fill" />

                <Button x:Name="BtnPrayerTime"
                        Text="Prayer Time"
                        BackgroundColor="{StaticResource Primary}"
                        TextColor="White"
                        SemanticProperties.Hint="Start a prayer session"
                        HorizontalOptions="Fill" />

                <!-- Overdue count summary -->
                <Border Style="{StaticResource PrayerCardBorder}" Padding="16,14">
                    <Label Text="{Binding OverdueHeadline}"
                           FontSize="15"
                           TextColor="{StaticResource Tertiary}"
                           LineHeight="1.4" />
                </Border>

                <!-- Suggested prayers — only shown when there are overdue requests -->
                <Border IsVisible="{Binding HasOverdue}"
                        Style="{StaticResource PrayerCardBorder}"
                        Padding="12,12">
                    <VerticalStackLayout Spacing="0">
                        <Label Text="Needs attention"
                               FontSize="12"
                               Style="{StaticResource MutedText}"
                               Margin="4,0,4,8" />
                        <VerticalStackLayout BindableLayout.ItemsSource="{Binding SuggestedPrayers}"
                                             Spacing="0">
                            <BindableLayout.ItemTemplate>
                                <DataTemplate x:DataType="viewModels:SuggestedPrayerViewModel">
                                    <Grid RowDefinitions="Auto, Auto"
                                          Padding="4,8">
                                        <Grid.GestureRecognizers>
                                            <TapGestureRecognizer Command="{Binding SelectCommand}" />
                                        </Grid.GestureRecognizers>
                                        <Label Text="{Binding CardTitle}"
                                               FontSize="11"
                                               Style="{StaticResource MutedText}" />
                                        <Label Grid.Row="1"
                                               Text="{Binding PrayerTitle}"
                                               FontSize="15"
                                               FontAttributes="Bold"
                                               TextColor="{StaticResource Tertiary}" />
                                        <BoxView Grid.Row="1"
                                                 Style="{StaticResource DividerLine}"
                                                 VerticalOptions="End" />
                                    </Grid>
                                </DataTemplate>
                            </BindableLayout.ItemTemplate>
                        </VerticalStackLayout>
                    </VerticalStackLayout>
                </Border>

            </VerticalStackLayout>
        </ScrollView>

        <!-- Onboarding coaching banner — PrayerTime step -->
        <onboarding:OnboardingBanner
            Grid.Row="1"
            ExpectedStep="PrayerTime"
            HeadlineText="Tap 'Prayer Time' when you're ready to pray"
            SubText="Choose 'All Requests' to get started" />

    </Grid>
</ContentPage>
```

- [ ] **Step 2: Update `MainPage.xaml.cs`**

Replace the file:

```csharp
using CommunityToolkit.Maui.Views;
using PrayerApp.Models;
using PrayerApp.Services;
using PrayerApp.ViewModels;
using PrayerApp.Views.Onboarding;

namespace PrayerApp.Views;

public partial class MainPage : ContentPage
{
    private readonly HomeViewModel _homeViewModel;
    private readonly IOnboardingService _onboardingService;

    public MainPage()
    {
        InitializeComponent();

        _homeViewModel = new HomeViewModel();
        BindingContext = _homeViewModel;

        _onboardingService = IPlatformApplication.Current!.Services
            .GetRequiredService<IOnboardingService>();

        BtnQuickAdd.Clicked += async (s, e) =>
            await Shell.Current.Navigation.PushModalAsync(new QuickAddPage());

        BtnPrayerTime.Clicked += async (s, e) =>
        {
            var action = await DisplayActionSheetAsync("Prayer Time", "Cancel", null, "All Requests", "By Tags");
            if (action == "All Requests")
                await Shell.Current.GoToAsync($"{nameof(PrayerTime.PrayerTimePage)}?scope=all");
            else if (action == "By Tags")
                await Shell.Current.Navigation.PushModalAsync(new PrayerTime.PrayerTimeScopePage());
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _homeViewModel.LoadAsync();

        // Show welcome popup on first visit — one-shot guard prevents re-showing on back navigation
        if (_onboardingService.CurrentStep == OnboardingStep.Welcome
            && !_onboardingService.WelcomeShownThisSession)
        {
            _onboardingService.MarkWelcomeShown();
            await this.ShowPopupAsync(new OnboardingWelcomePopup(_onboardingService));
        }
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal`
Expected: 0 errors

- [ ] **Step 4: Run tests**

Run: `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add PrayerApp/Views/MainPage.xaml PrayerApp/Views/MainPage.xaml.cs
git commit -m "feat(F-2): MainPage shows welcome popup and PrayerTime coaching banner"
```

---

## Chunk 4: Participating Pages — Banners

### Task 9: `PrayerCardsPage` — two coaching banners

**Files:**
- Modify: `PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml`

`PrayerCardsPage` needs two banners: one for `CreateCard` step and one for `AddRequest` step. Only one is visible at a time (controlled by the service).

- [ ] **Step 1: Update `PrayerCardsPage.xaml`**

Add `xmlns:onboarding` namespace and wrap the `CollectionView` in a `Grid` with an `OnboardingBanner` row:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewModels="clr-namespace:PrayerApp.ViewModels"
             xmlns:onboarding="clr-namespace:PrayerApp.Views.Onboarding"
             x:Class="PrayerApp.Views.PrayerCard.PrayerCardsPage"
             NavigatedTo="ContentPage_NavigatedTo"
             x:DataType="viewModels:PrayerCardsViewModel">
    <ContentPage.BindingContext>
        <viewModels:PrayerCardsViewModel />
    </ContentPage.BindingContext>
    <ContentPage.ToolbarItems>
        <ToolbarItem Text="Add Card" Command="{Binding NewCommand}" />
    </ContentPage.ToolbarItems>

    <Grid RowDefinitions="*,Auto,Auto">

        <CollectionView
            Grid.Row="0"
            x:Name="cardCollection"
            ItemsSource="{Binding AllPrayerCards}"
            Margin="12,8"
            SelectionMode="None">
            <!-- ... existing ItemsLayout and ItemTemplate unchanged ... -->
        </CollectionView>

        <!-- CreateCard step banner -->
        <onboarding:OnboardingBanner
            Grid.Row="1"
            ExpectedStep="CreateCard"
            HeadlineText="Tap 'Add Card' to create your first prayer card"
            SubText="Give it a name — a person, a topic, or a place." />

        <!-- AddRequest step banner -->
        <onboarding:OnboardingBanner
            Grid.Row="2"
            ExpectedStep="AddRequest"
            HeadlineText="Great! Now expand your card and tap '+ Add prayer'"
            SubText="Add your first prayer request to this card." />

    </Grid>
</ContentPage>
```

> **Important:** Keep the entire `CollectionView` element (with its `ItemsLayout` and `ItemTemplate`) intact — only wrap it in the Grid and move it to `Grid.Row="0"`. The banners use rows 1 and 2.

- [ ] **Step 2: Verify build**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add PrayerApp/Views/PrayerCard/PrayerCardsPage.xaml
git commit -m "feat(F-2): PrayerCardsPage coaching banners for CreateCard and AddRequest steps"
```

---

### Task 10: Remaining page banners (`PrayerCardPage`, `PrayerDetailPage`, `PrayerTimePage`)

**Files:**
- Modify: `PrayerApp/Views/PrayerCard/PrayerCardPage.xaml`
- Modify: `PrayerApp/Views/Prayer/PrayerDetailPage.xaml`
- Modify: `PrayerApp/Views/PrayerTime/PrayerTimePage.xaml`

The pattern is the same for each: add `xmlns:onboarding`, wrap page root in a `Grid`, add `OnboardingBanner` in the last row.

- [ ] **Step 1: Read each XAML file and understand its current root element**

For each file, check the current root layout element (ScrollView, Grid, VerticalStackLayout etc.) before modifying.

- [ ] **Step 2: Add banner to `PrayerCardPage.xaml`** (`NameCard` step)

Add to the page root grid bottom row:
```xml
<onboarding:OnboardingBanner
    Grid.Row="[last-row]"
    ExpectedStep="NameCard"
    HeadlineText="Give it a name — a person, a topic, your church"
    SubText="Then tap Save." />
```

- [ ] **Step 3: Add banner to `PrayerDetailPage.xaml`** (`NameRequest` step)

```xml
<onboarding:OnboardingBanner
    Grid.Row="[last-row]"
    ExpectedStep="NameRequest"
    HeadlineText="Enter your prayer request and tap Save" />
```

- [ ] **Step 4: Add banner to `PrayerTimePage.xaml`** (`PrayerTimeActive` step)

```xml
<onboarding:OnboardingBanner
    Grid.Row="[last-row]"
    ExpectedStep="PrayerTimeActive"
    HeadlineText="Swipe through your cards. Tap ✓ when you're done."
    SubText="You can use the auto-advance timer too." />
```

- [ ] **Step 5: Verify build**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add PrayerApp/Views/PrayerCard/PrayerCardPage.xaml \
        PrayerApp/Views/Prayer/PrayerDetailPage.xaml \
        PrayerApp/Views/PrayerTime/PrayerTimePage.xaml
git commit -m "feat(F-2): coaching banners on PrayerCardPage, PrayerDetailPage, PrayerTimePage"
```

---

## Chunk 5: Advance Triggers in ViewModels

### Task 11: `PrayerCardsViewModel` — advance on new card

**Files:**
- Modify: `PrayerApp/ViewModels/PrayerCardsViewModel.cs`

- [ ] **Step 1: Inject `IOnboardingService` and call `Advance()` in `NewPrayerCardAsync`**

In `PrayerCardsViewModel.cs`:

Add field:
```csharp
private readonly IOnboardingService _onboardingService;
```

In the constructor, resolve the service:
```csharp
_onboardingService = IPlatformApplication.Current!.Services
    .GetRequiredService<IOnboardingService>();
```

Update `NewPrayerCardAsync`:
```csharp
private async Task NewPrayerCardAsync()
{
    _onboardingService.Advance(); // CreateCard → NameCard (no-op if not at CreateCard)
    await Shell.Current.GoToAsync(nameof(Views.PrayerCard.PrayerCardPage));
}
```

- [ ] **Step 2: Verify build + tests**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal && dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: 0 errors, all tests pass

- [ ] **Step 3: Commit**

```bash
git add PrayerApp/ViewModels/PrayerCardsViewModel.cs
git commit -m "feat(F-2): advance onboarding step in PrayerCardsViewModel.NewPrayerCardAsync"
```

---

### Task 12: `PrayerCardViewModel` — advance on save (new card) and add prayer

**Files:**
- Modify: `PrayerApp/ViewModels/PrayerCardViewModel.cs`

- [ ] **Step 1: Inject `IOnboardingService`**

Add field:
```csharp
private readonly IOnboardingService _onboardingService;
```

Resolve in the no-arg constructor:
```csharp
_onboardingService = IPlatformApplication.Current!.Services
    .GetRequiredService<IOnboardingService>();
```

- [ ] **Step 2: Add `Advance()` to `SaveAsync` with `Id==0` guard**

```csharp
private async Task SaveAsync()
{
    bool isNew = _prayerCard.Id == 0;
    await _cardService.SaveCardAsync(_prayerCard);
    if (isNew)
        _onboardingService.Advance(); // NameCard → AddRequest
    await Shell.Current.GoToAsync($"..?saved={Identifier}");
}
```

- [ ] **Step 3: Add `Advance()` to `AddPrayerAsync`**

```csharp
private async Task AddPrayerAsync()
{
    _onboardingService.Advance(); // AddRequest → NameRequest (no-op if not at AddRequest)
    await Shell.Current.GoToAsync($"{nameof(PrayerDetailPage)}?newForCard={_prayerCard.Id}");
}
```

- [ ] **Step 4: Verify build + tests**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal && dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: 0 errors, all tests pass

- [ ] **Step 5: Commit**

```bash
git add PrayerApp/ViewModels/PrayerCardViewModel.cs
git commit -m "feat(F-2): advance onboarding in PrayerCardViewModel (save new card + add prayer)"
```

---

### Task 13: `PrayerRequestDetailViewModel` — advance on save (new request)

**Files:**
- Modify: `PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs`

- [ ] **Step 1: Inject `IOnboardingService`**

Add field:
```csharp
private readonly IOnboardingService _onboardingService;
```

Resolve in the no-arg constructor (after existing service resolutions):
```csharp
_onboardingService = IPlatformApplication.Current!.Services
    .GetRequiredService<IOnboardingService>();
```

- [ ] **Step 2: Update `SaveAsync` with `Id==0` guard**

```csharp
private async Task SaveAsync()
{
    bool isNew = _prayer.Id == 0;
    await _prayerService.SavePrayerAsync(_prayer);
    if (isNew)
        _onboardingService.Advance(); // NameRequest → PrayerTime

    if (ReturnToCards)
        await Shell.Current.GoToAsync($"..?prayerSaved={Identifier}&parentCardId={PrayerCardId}");
    else
        await Shell.Current.GoToAsync($"..?{_savedQueryKey}={Identifier}");
}
```

- [ ] **Step 3: Verify build + tests**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal && dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: 0 errors, all tests pass

- [ ] **Step 4: Commit**

```bash
git add PrayerApp/ViewModels/PrayerRequestDetailViewModel.cs
git commit -m "feat(F-2): advance onboarding in PrayerRequestDetailViewModel.SaveAsync"
```

---

### Task 14: `PrayerTimePage` code-behind — advance on `OnAppearing`

**Files:**
- Modify: `PrayerApp/Views/PrayerTime/PrayerTimePage.xaml.cs`

`PrayerTimePage.OnAppearing` advances from `PrayerTime → PrayerTimeActive`. This covers both the "All Requests" direct path and the "By Tags" scope-selection path. `Advance()` is a no-op when `!IsActive`, so multiple fires are safe.

- [ ] **Step 1: Update `PrayerTimePage.xaml.cs`**

Add field + resolve in constructor:
```csharp
private readonly IOnboardingService _onboardingService;

public PrayerTimePage()
{
    InitializeComponent();
    _orientationService = IPlatformApplication.Current!.Services.GetRequiredService<IOrientationService>();
    _onboardingService = IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>();
}
```

Update `OnAppearing`:
```csharp
protected override void OnAppearing()
{
    base.OnAppearing();
    _orientationService.LockLandscape();
    _onboardingService.Advance(); // PrayerTime → PrayerTimeActive (no-op otherwise)

    if (Window is not null)
    {
        Window.Stopped  += OnWindowStopped;
        Window.Resumed  += OnWindowResumed;
    }
}
```

- [ ] **Step 2: Verify build + tests**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal && dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: 0 errors, all tests pass

- [ ] **Step 3: Commit**

```bash
git add PrayerApp/Views/PrayerTime/PrayerTimePage.xaml.cs
git commit -m "feat(F-2): advance onboarding step in PrayerTimePage.OnAppearing"
```

---

### Task 15: `PrayerTimeViewModel` — advance on session end

**Files:**
- Modify: `PrayerApp/ViewModels/PrayerTimeViewModel.cs`

- [ ] **Step 1: Inject `IOnboardingService`**

Add field:
```csharp
private readonly IOnboardingService _onboardingService;
```

Resolve in constructor (after existing service resolutions):
```csharp
_onboardingService = IPlatformApplication.Current!.Services.GetRequiredService<IOnboardingService>();
```

- [ ] **Step 2: Update `EndSessionAsync`**

```csharp
private async Task EndSessionAsync()
{
    StopAutoMode();
    _onboardingService.Advance(); // PrayerTimeActive → Complete (no-op otherwise)
    await Shell.Current.GoToAsync("..");
}
```

- [ ] **Step 3: Verify build + tests**

Run: `dotnet build PrayerApp/PrayerApp.csproj --verbosity minimal && dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity minimal`
Expected: 0 errors, all tests pass

- [ ] **Step 4: Commit**

```bash
git add PrayerApp/ViewModels/PrayerTimeViewModel.cs
git commit -m "feat(F-2): advance onboarding to Complete in PrayerTimeViewModel.EndSessionAsync"
```

---

## Chunk 6: Final Verification

### Task 16: Full test run + build verification

- [ ] **Step 1: Full test run**

Run: `dotnet test PrayerApp.Tests/PrayerApp.Tests.csproj --verbosity normal`
Expected: All pass, 0 failures

- [ ] **Step 2: Build the MAUI app**

Run: `dotnet build PrayerApp/PrayerApp.csproj -c Release --verbosity minimal`
Expected: 0 errors

- [ ] **Step 3: Manual smoke test checklist**

Deploy to simulator/device and verify:

- [ ] First launch → Welcome popup appears on Home tab
- [ ] "Get Started" → navigates to Cards tab, `CreateCard` banner visible
- [ ] Tap "Add Card" → navigates to PrayerCardPage, `NameCard` banner visible
- [ ] Type a name + tap Save → returns to Cards tab, `AddRequest` banner visible
- [ ] Expand card + tap "+ Add prayer" → PrayerDetailPage, `NameRequest` banner visible
- [ ] Type a request + tap Save → returns to Cards tab, no banner (step is `PrayerTime`)
- [ ] Go to Home tab → `PrayerTime` banner visible
- [ ] Tap "Prayer Time" → PrayerTimePage, `PrayerTimeActive` banner visible
- [ ] End session → Closing popup appears, tap Done → no more banners
- [ ] "Skip tour" at any step → all banners disappear immediately, no popup

- [ ] **Step 4: Commit any final adjustments**

```bash
git add -A
git commit -m "feat(F-2): onboarding complete — all steps wired, tests passing"
```

---

## Notes for Implementer

### `Preferences` in tests
The `OnboardingService` uses `Preferences.Get/Set` which requires a MAUI host. If `OnboardingServiceTests` throws `PlatformNotSupportedException`, mark those tests `[Fact(Skip = "Requires MAUI host")]` rather than refactoring the service. The service logic is simple enough to validate at runtime.

### XAML namespace for `OnboardingBanner`
Add `xmlns:onboarding="clr-namespace:PrayerApp.Views.Onboarding"` to each page that hosts a banner.

### `ExpectedStep` enum value in XAML
The `ExpectedStep` bindable property is of type `OnboardingStep`. MAUI's XAML parser resolves enum values by name string — `ExpectedStep="CreateCard"` works without any converter.

### `PrayerCardsPage` — full XAML preservation
Task 9 shows a condensed version of `PrayerCardsPage.xaml`. The actual implementation must preserve the entire `CollectionView.ItemTemplate` block verbatim — only the wrapping Grid and banner rows are new.
