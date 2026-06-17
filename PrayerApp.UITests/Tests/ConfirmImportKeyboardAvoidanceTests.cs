using System.ComponentModel;
using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using SkiaSharp;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// Issue #54 — regression guard for <c>KeyboardAvoidanceBehavior</c> on
/// <c>ConfirmImportPage</c>'s Card Title <c>Entry</c>.
///
/// <para>
/// The behaviour (<c>PrayerApp/Behaviors/KeyboardAvoidanceBehavior.cs</c>,
/// attached at <c>ConfirmImportPage.xaml:159</c>) scrolls the focused
/// <c>CardTitleEntry</c> (<c>AutomationId="ConfirmImport_Entry_CardTitle"</c>,
/// XAML :151) into view inside its enclosing <c>ScrollView</c> on focus, so the
/// on-screen keyboard never covers the field. Without it, focusing the Card
/// Title field on a short screen leaves the field occluded behind the keyboard.
/// </para>
///
/// <para>
/// This test reaches that field via the Quick Add manual-entry flow (Home →
/// Quick Add → "New Card" segment, exactly as <c>QuickAddTests
/// .QuickAdd_SwitchToNewCard_SavesNewCard</c>), focuses the Entry, waits for the
/// keyboard, then screenshots and samples a pixel patch immediately BELOW the
/// Entry's on-screen bottom edge. If <c>KeyboardAvoidanceBehavior</c> is doing its
/// job, the Entry has been scrolled above the keyboard, so the patch just below it
/// is light page/content chrome. If the behaviour is removed, the field is occluded
/// by the keyboard and that same patch becomes (dark) keyboard chrome.
/// </para>
///
/// <para><b>REQUIRED PC-RUN GENUINENESS CHECKLIST — complete before merge.</b>
/// This is the proof the guard is genuine, not a tautology. The red-flip is
/// GEOMETRY-DEPENDENT: the <c>entryBottom + 16</c> sample (relative to the post-scroll
/// Entry rect) is NOT guaranteed to land on keyboard chrome in the occluded
/// (behaviour-removed) state, so the "MUST flip RED" claim is UNVERIFIED until a human
/// demonstrates it on a live device. Do NOT change the sample strategy without a device
/// to validate it. Run these steps on the Windows PC with a live Pixel 9 / API 36
/// emulator (light mode is forced by the test itself):</para>
/// <list type="number">
///   <item>Build + deploy <b>Debug</b> Android to a live emulator; run this test and
///   confirm it is <b>GREEN</b>.</item>
///   <item>Remove <c>&lt;behaviors:KeyboardAvoidanceBehavior /&gt;</c> from
///   <c>CardTitleEntry</c> (<c>ConfirmImportPage.xaml:159</c>), rebuild — the test
///   <b>MUST flip RED</b> (the field is occluded, so the sampled patch is keyboard
///   chrome and fails the light-band assertion).</item>
///   <item><b>If it does NOT flip red</b> (the <c>entryBottom + 16</c> sample did not
///   hit keyboard chrome in the occluded state): capture BOTH states' sampled
///   coordinates + RGB averages from the failure-message screenshots, then move the
///   sample to where it reliably discriminates — e.g. sample AT the Entry's reported
///   center (visible → Entry chrome; occluded → keyboard chrome) or at a fixed
///   keyboard-region point — and re-tune <c>LightFloor</c> against a real Gboard-light
///   capture. The test is NOT trustworthy until the red→green pivot is demonstrated.</item>
///   <item>Restore the behaviour — confirm the test is <b>GREEN</b> again.</item>
/// </list>
/// <para>That red→green pivot on toggling the single behaviour line is the proof the
/// assertion guards the real regression. The test must not be relied upon as a
/// regression guard until step 2 (or the step-3 retune) demonstrates the flip.</para>
///
/// <para><b>Cross-platform pixel sampling:</b> uses SkiaSharp (<c>SKBitmap.Decode</c>
/// + <c>GetPixel</c>) rather than <c>System.Drawing</c> (GDI+, Windows-only), so the
/// file compiles and the decode path verifies on macOS while still running the live
/// Android assertion on the Windows PC.</para>
///
/// <para><b>Android-only, PC-run:</b> the harness ties target platform to host OS
/// (<c>TestConfig.cs:14-15</c>): on macOS <c>IsIOS==true</c> and <c>AppiumSetup</c>
/// builds an iOS driver, so this Android-specific keyboard regression naturally
/// SKIPS here and RUNS on the Windows PC where <c>IsAndroid==true</c>. It also needs
/// <c>adb</c> reachable (to query keyboard state), mirroring the
/// <c>DarkModeRenderingTests</c> guard.</para>
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
[Trait("Host", "Windows")]
[Trait("Section", "2-Home")]
public class ConfirmImportKeyboardAvoidanceTests
{
    private readonly AppiumSetup _setup;
    public ConfirmImportKeyboardAvoidanceTests(AppiumSetup setup) => _setup = setup;

    /// <summary>
    /// Focuses the ConfirmImport Card Title Entry, waits for the keyboard, and
    /// asserts the region just below the Entry is page/content chrome (Entry
    /// scrolled ABOVE the keyboard by KeyboardAvoidanceBehavior) — NOT keyboard
    /// chrome. Designed to FAIL if the behaviour is removed (Issue #54 regression).
    /// </summary>
    [SkippableFact]
    public void ConfirmImport_CardTitleEntry_StaysAboveKeyboard_OnFocus()
    {
        if (TestConfig.IsIOS)
            throw new SkipException(
                "Android-only: KeyboardAvoidanceBehavior regression for ConfirmImportPage. " +
                "The harness ties target platform to host OS (TestConfig.cs:14-15) — on macOS " +
                "AppiumSetup builds an iOS driver, so this Android test runs only on the Windows PC.");

        // adb is used to query keyboard-shown state on Android. If unreachable from the
        // test process, skip — the test cannot confirm the keyboard is up. Mirrors the
        // DarkModeRenderingTests TryRunAdb guard.
        if (!TryRunAdb("get-state", out _))
            throw new SkipException("adb not reachable from test process — skipping Issue #54 regression");

        // The >= LightFloor discriminator assumes the page renders in LIGHT mode (the
        // ConfirmImport surface is a warm-journal off-white). In DARK mode the page is
        // PageDark (~RGB 13,14,12), so the patch below the Entry would FALSE-FAIL even
        // when KeyboardAvoidanceBehavior works. A killed DarkModeRenderingTests run can
        // also leave the device in night mode. So: record the entry night-mode state,
        // force light for the duration of this test, and restore it in finally{} —
        // mirroring the darkModeApplied/restore pattern in DarkModeRenderingTests.
        var wasNightModeOnEntry = IsNightModeOn();
        TryRunAdb("shell cmd uimode night no", out _);
        Thread.Sleep(TestConfig.DelayAfterNavigation);

        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        try
        {
            // 1) Open Quick Add (ConfirmImportPage in Manual mode), then switch to the
            //    "New Card" segment so the Card Title Entry becomes visible. Identical
            //    navigation to QuickAddTests.QuickAdd_SwitchToNewCard_SavesNewCard.
            driver.EnsureOnTab("Home", _setup);
            driver.WaitAndTap("Home_Btn_QuickAdd");
            driver.WaitForElement("ConfirmImport_Seg_ExistingCard", timeoutSeconds: 15);
            Thread.Sleep(TestConfig.DelayModalAnimation);

            driver.WaitAndTap("ConfirmImport_Seg_NewCard");
            Thread.Sleep(TestConfig.DelayAfterTap);

            var entry = driver.WaitForElement("ConfirmImport_Entry_CardTitle", timeoutSeconds: 10);

            // 2) Focus the Entry and wait for the on-screen keyboard. Click() (not
            //    EnterText, which Clear()s) so focus is retained and the Focused event
            //    fires — that is what triggers KeyboardAvoidanceBehavior's scroll.
            entry.Click();
            Thread.Sleep(TestConfig.DelayAfterTap);

            // Wait for the soft keyboard to actually be shown (UiAutomator2 mobile:
            // isKeyboardShown). Uses DefaultTimeout (10s) rather than ShortTimeout (3s):
            // a slow first-focus on the PC emulator could otherwise SkipException here
            // and silently mask the regression this test exists to catch.
            if (!WaitForKeyboard(driver, TestConfig.DefaultTimeout))
                throw new SkipException(
                    "Soft keyboard did not appear after focusing ConfirmImport_Entry_CardTitle — " +
                    "cannot evaluate keyboard-avoidance occlusion.");

            // Allow KeyboardAvoidanceBehavior's additive ScrollToAsync (Center) to settle.
            // The behaviour yields two frames after the platform pass, then animates.
            Thread.Sleep(TestConfig.DelayLongSettle);

            // 3) Read the Entry's on-screen rect AFTER the avoidance scroll has settled.
            var loc = entry.Location;
            var size = entry.Size;
            var entryBottom = loc.Y + size.Height;

            // 4) Screenshot + decode with SkiaSharp (cross-platform).
            var screenshotBytes = driver.GetScreenshot().AsByteArray;
            var screenshotPath = Path.Combine(Path.GetTempPath(),
                $"issue54-keyboard-avoidance-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            File.WriteAllBytes(screenshotPath, screenshotBytes);

            // 5) Sample a patch just BELOW the Entry's bottom edge, horizontally centered.
            //    If the field has been scrolled above the keyboard, this band is light
            //    page/content chrome (the form continues / page background). If the field
            //    is occluded, this band falls on the keyboard, which is dark grey chrome.
            //
            //    Offset: ~16 px below the field (one form-row gap) keeps the sample in the
            //    page content rather than on the Entry's own border. Clamped in
            //    AverageColorAt to the image bounds.
            var sampleX = loc.X + (size.Width / 2);
            var sampleY = entryBottom + 16;

            SampleColor avg = AverageColorAt(screenshotBytes, sampleX, sampleY, patchSize: 20);

            // 6) Discriminator (DOCUMENTED EXPECTATION — exact values to be confirmed/tuned
            //    on the PC against a live Pixel 9 / API 36 emulator). Light mode is FORCED
            //    above so this floor is deterministic regardless of device/global appearance:
            //
            //    - Light page/content chrome on the ConfirmImport form (PASS state): the
            //      app's light surface — near-white / warm-journal off-white. All channels
            //      run high (empirically ~225-255 on the existing light captures in /tmp).
            //    - Android soft-keyboard chrome (FAIL / occluded state): a distinctly darker
            //      grey panel (Gboard light theme ≈ RGB 220ish but the key rows + suggestion
            //      strip pull the AVERAGED patch well down; dark theme ≈ RGB 40-70). In every
            //      case the keyboard average sits clearly below the page background.
            //
            //    A page-background patch averages markedly brighter than a keyboard patch.
            //    Threshold: require the patch to be light (every channel >= 200). This is the
            //    same "is it bright page chrome vs darker keyboard chrome" discrimination the
            //    DarkModeRenderingTests "not white-ish" guard uses, inverted. The PC run
            //    confirms the brightness floor and the 16px/20px sample geometry against the
            //    real device; adjust the 200 floor there if emulator rendering variance
            //    demands it (keep it comfortably above the measured keyboard average).
            const int LightFloor = 200;
            var isLightPageChrome = avg.R >= LightFloor && avg.G >= LightFloor && avg.B >= LightFloor;

            Assert.True(isLightPageChrome,
                $"Region just below ConfirmImport_Entry_CardTitle sampled as NON-light chrome " +
                $"(R={avg.R}, G={avg.G}, B={avg.B}; light floor {LightFloor}) — the Card Title field " +
                $"appears OCCLUDED by the keyboard, indicating KeyboardAvoidanceBehavior did not scroll " +
                $"it above the keyboard (Issue #54 regression). " +
                $"Sample at ({sampleX},{sampleY}), {16}px below Entry bottom; Entry rect " +
                $"[{loc.X},{loc.Y}][{loc.X + size.Width},{entryBottom}]. Screenshot: {screenshotPath}");
        }
        finally
        {
            // Restore the device's pre-test night-mode state. Only force night mode back
            // ON if it was ON when we entered; otherwise leave it OFF (the value we set).
            if (wasNightModeOnEntry)
                TryRunAdb("shell cmd uimode night yes", out _);
        }
    }

    /// <summary>
    /// Returns true if the device is currently in night (dark) mode. Reads
    /// <c>cmd uimode night</c>, whose stdout contains "Night mode: yes" when dark mode
    /// is active. Returns false on any adb failure (best-effort; the test still forces
    /// light mode and restores to light, which is the safe default for the assertion).
    /// </summary>
    private static bool IsNightModeOn()
    {
        if (!TryRunAdb("shell cmd uimode night", out var mode))
            return false;
        return mode.Contains("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Poll UiAutomator2's <c>mobile: isKeyboardShown</c> until true or the timeout
    /// elapses. Returns false if the keyboard never appears (or the endpoint errors).
    /// </summary>
    private static bool WaitForKeyboard(AppiumDriver driver, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var shown = driver.ExecuteScript("mobile: isKeyboardShown");
                if (shown is bool b && b)
                    return true;
                if (shown is string s && bool.TryParse(s, out var parsed) && parsed)
                    return true;
            }
            catch (WebDriverException)
            {
                // Endpoint momentarily unavailable; retry until the deadline.
            }
            Thread.Sleep(TestConfig.DelayAfterTap);
        }
        return false;
    }

    /// <summary>Plain RGB triple, decoupled from System.Drawing.Color (Windows-only GDI+).</summary>
    private readonly record struct SampleColor(int R, int G, int B);

    /// <summary>
    /// Average RGB of a square patch centered at (x,y) in the screenshot bytes,
    /// decoded with SkiaSharp (cross-platform — works on the Windows PC for the live
    /// Android run and on this macOS host for the decode-path verification). Replaces
    /// the GDI+ <c>System.Drawing.Bitmap</c> path used by DarkModeRenderingTests.
    /// </summary>
    private static SampleColor AverageColorAt(byte[] pngBytes, int x, int y, int patchSize)
    {
        using var bmp = SKBitmap.Decode(pngBytes)
            ?? throw new InvalidOperationException("SkiaSharp failed to decode the screenshot PNG.");

        var half = patchSize / 2;
        var x0 = Math.Max(0, x - half);
        var y0 = Math.Max(0, y - half);
        var x1 = Math.Min(bmp.Width - 1, x + half);
        var y1 = Math.Min(bmp.Height - 1, y + half);

        long r = 0, g = 0, b = 0;
        long n = 0;
        for (var py = y0; py <= y1; py++)
        {
            for (var px = x0; px <= x1; px++)
            {
                var p = bmp.GetPixel(px, py);
                r += p.Red;
                g += p.Green;
                b += p.Blue;
                n++;
            }
        }

        if (n == 0)
            throw new InvalidOperationException(
                $"Sample patch ({x},{y}) ±{half} is outside the screenshot bounds ({bmp.Width}x{bmp.Height}).");

        return new SampleColor((int)(r / n), (int)(g / n), (int)(b / n));
    }

    /// <summary>
    /// Runs an adb command without throwing. Returns true if adb launched and exited 0;
    /// false if adb is missing or exited non-zero. <paramref name="stdout"/> is the
    /// captured stdout (empty on failure). Mirrors DarkModeRenderingTests.TryRunAdb.
    /// </summary>
    private static bool TryRunAdb(string arguments, out string stdout)
    {
        stdout = string.Empty;
        try
        {
            var psi = new ProcessStartInfo("adb", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;

            var outTask = proc.StandardOutput.ReadToEndAsync();
            _ = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(15_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return false;
            }
            stdout = outTask.Result;
            return proc.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // adb not on PATH
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
