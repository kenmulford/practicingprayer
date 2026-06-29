using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using PrayerApp.UITests.Helpers;
using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// Issue #52 — regression guard for the dark-mode switch-thumb white-paint bug.
///
/// Original bug: on cold launch in system dark mode, an <c>IsToggled=true</c>
/// <c>Switch</c> rendered with a white thumb instead of the theme's Primary color.
/// The shipped workaround in <c>MauiProgram.cs</c> (SwitchHandler mapper
/// "SyncInitialThumbColor") calls <c>VisualStateManager.GoToState(sw, "On")</c>
/// at handler creation so the correct VSM Setter applies on first paint.
///
/// This test cold-launches the app in dark mode with the notifications switch
/// pre-toggled ON, screenshots the App Settings page on first paint, samples
/// the thumb area, and asserts the rendered color is NOT white-ish. Manual
/// verification against build 1.4.2/120 sampled <c>#6B7D5A</c> at the thumb
/// center — the exact Primary token value from Colors.xaml.
///
/// Android-only:
///  - Requires <c>adb</c> shell access to drive <c>cmd uimode night yes</c> and
///    <c>am force-stop</c>.
///  - Pixel sampling uses <c>System.Drawing.Bitmap</c> (GDI+), which is Windows-only.
/// </summary>
[Collection("Appium")]
[Trait("Platform", "Android")]
// Host=Windows is a filter label, NOT a host gate. The adb work (dark-mode + cold
// launch) is cross-platform, so the Android driver can attach from macOS too
// (UITEST_PLATFORM=android). The genuine Windows requirement is the GDI+ pixel
// sampling (AverageColorAt / System.Drawing) — off Windows the test SkipExceptions
// rather than failing. Use this label to include/exclude the test when filtering runs.
[Trait("Host", "Windows")]
[Trait("Section", "9-Settings")]
public class DarkModeRenderingTests
{
    private readonly AppiumSetup _setup;
    public DarkModeRenderingTests(AppiumSetup setup) => _setup = setup;

    /// <summary>
    /// Cold launches the app in dark mode with the notifications switch toggled ON
    /// and asserts the thumb is not rendered as white (Issue #52 regression).
    /// </summary>
    [SkippableFact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void AppSettings_NotificationSwitchThumb_RendersThemeColor_OnColdLaunchDarkMode()
    {
        // Windows-host gate, fired BEFORE any device-state mutation. The genuine
        // requirement is the GDI+ pixel sampling (System.Drawing), which is Windows-only;
        // off Windows the test can do nothing useful, so skip before the dark-mode + adb +
        // cold-launch choreography runs. Checking the host directly (not TestConfig.IsIOS)
        // matters now that UITEST_PLATFORM=android makes the Android target run on macOS,
        // where IsIOS is false but GDI+ is still unavailable.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new SkipException(
                "Windows-only: requires adb (cmd uimode + force-stop) and System.Drawing (GDI+, Windows-only) for pixel sampling");

        // Confirm adb is reachable from the test process before any state mutation.
        // If not, skip — the test cannot drive the dark-mode + cold-launch setup.
        if (!TryRunAdb("get-state", out _))
            throw new SkipException("adb not reachable from test process — skipping Issue #52 regression");

        _setup.Driver.ResetAppUIState(_setup);
        var driver = _setup.Driver;

        // Track whether we toggled the switch and what dark-mode state was on entry
        // so finally{} can restore device state regardless of outcome.
        var toggledByTest = false;
        var darkModeApplied = false;

        try
        {
            // 1) Force system into dark mode.
            TryRunAdb("shell cmd uimode night yes", out _);
            darkModeApplied = true;
            Thread.Sleep(TestConfig.DelayAfterNavigation);

            // 2) Navigate to App Settings and ensure the switch is toggled ON.
            driver.EnsureOnTab("Settings", _setup);
            driver.WaitAndTap("Settings_Row_AppSettings");
            driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 10);

            var switchElem = driver.FindByAutomationId("AppSettings_Switch_Notifications");
            var initiallyOn = ReadSwitchOn(switchElem);
            if (!initiallyOn)
            {
                switchElem.Click();
                toggledByTest = true;
                Thread.Sleep(TestConfig.DelayAfterTap);

                // Notification permission prompt may appear on first toggle-on. The app
                // uses autoGrantPermissions=true in Appium options, but defensively tap
                // Allow if the system prompt slipped through.
                DismissPermissionPromptIfPresent(driver);
            }

            // 3) Cold launch: force-stop, then re-activate.
            TryRunAdb($"shell am force-stop {TestConfig.AndroidPackage}", out _);
            Thread.Sleep(TestConfig.DelayAfterDismiss);

            driver.ActivateApp(TestConfig.AndroidPackage);
            Thread.Sleep(TestConfig.DelayAppRelaunch);

            // 4) Navigate Home → Settings → App Settings on the freshly relaunched app.
            driver.DismissOnboardingIfPresent(_setup);
            driver.EnsureOnTab("Settings", _setup);
            driver.WaitAndTap("Settings_Row_AppSettings");
            var thumbedSwitch = driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 10);

            // Capture the rect at first paint *before* doing anything else that could
            // cause a re-render. The Issue #52 bug is specifically a first-paint defect.
            var loc = thumbedSwitch.Location;
            var size = thumbedSwitch.Size;

            // 5) Screenshot + sample.
            var screenshotBytes = driver.GetScreenshot().AsByteArray;
            var screenshotPath = Path.Combine(Path.GetTempPath(),
                $"thumb-bug-regression-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            File.WriteAllBytes(screenshotPath, screenshotBytes);

            // For IsToggled=true, the thumb sits in the right half of the switch bounds.
            // Sample at ~75% across, vertically centered. The manual verification at
            // 09_appsettings_FIRST_RENDER_dark_toggled_ON.png used (956, 476) within
            // bounds [864,441][986,512] — center of right half.
            var sampleX = loc.X + (int)(size.Width * 0.75);
            var sampleY = loc.Y + (size.Height / 2);

            Color avg;
            try
            {
                avg = AverageColorAt(screenshotBytes, sampleX, sampleY, patchSize: 20);
            }
            catch (PlatformNotSupportedException ex)
            {
                throw new SkipException(
                    $"System.Drawing (GDI+) not supported on this host: {ex.Message}");
            }

            // 6) Assert NOT white-ish. The manual run sampled #6B7D5A (107, 125, 90) —
            // well below the 230/230/230 floor. Asserting exact-color match would be
            // brittle across emulator rendering variance; asserting "not white" is the
            // direct guard for the regression.
            var isWhiteIsh = avg.R > 230 && avg.G > 230 && avg.B > 230;
            Assert.False(isWhiteIsh,
                $"Switch thumb rendered white-ish (R={avg.R}, G={avg.G}, B={avg.B}) on first cold-launch " +
                $"in dark mode — Issue #52 regression. Sample at ({sampleX},{sampleY}) within switch bounds " +
                $"[{loc.X},{loc.Y}][{loc.X + size.Width},{loc.Y + size.Height}]. " +
                $"Screenshot: {screenshotPath}");
        }
        finally
        {
            // Restore the Switch to its original state if we toggled it. Best-effort:
            // the navigation context may have changed if the test failed mid-flow.
            if (toggledByTest)
            {
                try
                {
                    if (!driver.IsDisplayed("AppSettings_Switch_Notifications", timeoutSeconds: 2))
                    {
                        driver.ResetAppUIState(_setup);
                        driver.EnsureOnTab("Settings", _setup);
                        driver.WaitAndTap("Settings_Row_AppSettings");
                        driver.WaitForElement("AppSettings_Switch_Notifications", timeoutSeconds: 5);
                    }
                    driver.Tap("AppSettings_Switch_Notifications");
                    Thread.Sleep(TestConfig.DelayAfterTap);
                }
                catch { /* best-effort restore */ }
            }

            // Restore light mode regardless of how the test exited.
            if (darkModeApplied)
                TryRunAdb("shell cmd uimode night no", out _);
        }
    }

    /// <summary>
    /// Read a Switch's toggled state. On Android, UiAutomator2 exposes Switch state
    /// via the <c>checked</c> attribute on the element.
    /// </summary>
    private static bool ReadSwitchOn(AppiumElement switchElem)
    {
        try
        {
            var attr = switchElem.GetDomAttribute("checked");
            return string.Equals(attr, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (WebDriverException)
        {
            return false;
        }
    }

    /// <summary>
    /// If a notification-permission prompt is visible (the runtime POST_NOTIFICATIONS
    /// dialog on Android 13+), accept it. No-op otherwise.
    /// </summary>
    private static void DismissPermissionPromptIfPresent(AppiumDriver driver)
    {
        try
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.ShortTimeout;
            var allow = driver.FindElements(By.XPath(
                "//*[@text='Allow' or @text='ALLOW' or @resource-id='com.android.permissioncontroller:id/permission_allow_button']"));
            if (allow.Count > 0)
            {
                allow[0].Click();
                Thread.Sleep(TestConfig.DelayAfterDismiss);
            }
        }
        catch (WebDriverException) { /* prompt not shown */ }
        finally
        {
            driver.Manage().Timeouts().ImplicitWait = TestConfig.DefaultTimeout;
        }
    }

    /// <summary>
    /// Average RGB of a square patch centered at (x,y) in the screenshot bytes.
    /// Uses GDI+ via <see cref="Bitmap"/>; Windows-only by design — the GDI+ pixel
    /// sampling is unavailable off Windows, so the test SkipExceptions there (see the
    /// Host=Windows trait). The Android driver itself can attach from macOS too.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static Color AverageColorAt(byte[] pngBytes, int x, int y, int patchSize)
    {
        using var ms = new MemoryStream(pngBytes);
        using var bmp = new Bitmap(ms);

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
                r += p.R;
                g += p.G;
                b += p.B;
                n++;
            }
        }

        if (n == 0)
            throw new InvalidOperationException(
                $"Sample patch ({x},{y}) ±{half} is outside the screenshot bounds ({bmp.Width}x{bmp.Height}).");

        return Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n));
    }

    /// <summary>
    /// Runs an adb command without throwing. Returns true if adb launched and exited 0;
    /// false if adb is missing or exited non-zero. <paramref name="stdout"/> is the captured
    /// stdout (empty on failure).
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
