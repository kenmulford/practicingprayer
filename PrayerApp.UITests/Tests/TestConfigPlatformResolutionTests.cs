using PrayerApp.UITests.Infrastructure;
using Xunit;

namespace PrayerApp.UITests.Tests;

/// <summary>
/// Issue #139 — unit coverage for the UITEST_PLATFORM target-platform override.
///
/// These exercise the pure <see cref="TestConfig.ResolvePlatform"/> seam, which the
/// process-once <c>static readonly</c> <c>IsAndroid</c>/<c>IsIOS</c> fields delegate to.
/// They run with NO emulator/simulator/Appium — deliberately not in the "Appium"
/// collection — so they verify the resolution table on any host without booting a driver.
/// </summary>
[Trait("Category", "Unit")]
public class TestConfigPlatformResolutionTests
{
    [Theory]
    // Explicit override forces the target platform regardless of host OS.
    [InlineData("android", false, false, true, false)] // override=android → Android
    [InlineData("ios", false, false, false, true)]     // override=ios → iOS
    // Case/whitespace insensitivity: Trim + ToLowerInvariant before matching.
    [InlineData("  ANDROID  ", false, false, true, false)]
    // Unset (null) override falls back to the host OS — byte-for-byte the old behaviour.
    [InlineData(null, true, false, true, false)]  // unset on Windows → Android (unchanged)
    [InlineData(null, false, true, false, true)]  // unset on macOS → iOS (unchanged)
    // Declared-but-blank UITEST_PLATFORM (e.g. `UITEST_PLATFORM=` or a blank CI var) is
    // treated as unset, so it falls back to the host OS — not (false,false) which would
    // make GetOptions() throw despite the "unset → host default" contract.
    [InlineData("", true, false, true, false)]    // empty on Windows → Android (host fallback)
    [InlineData("   ", false, true, false, true)] // whitespace on macOS → iOS (host fallback)
    // Unrecognised override with no host match → (false,false): GetOptions() then throws
    // PlatformNotSupportedException (fail-loud). Documents the preserved fall-through.
    [InlineData("foo", false, false, false, false)]
    public void ResolvePlatform_ResolvesTargetPlatform(
        string? overrideValue, bool hostIsWindows, bool hostIsOSX,
        bool expectedAndroid, bool expectedIOS)
    {
        var (isAndroid, isIOS) = TestConfig.ResolvePlatform(overrideValue, hostIsWindows, hostIsOSX);

        Assert.Equal(expectedAndroid, isAndroid);
        Assert.Equal(expectedIOS, isIOS);
    }
}
