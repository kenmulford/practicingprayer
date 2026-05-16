using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using PrayerApp.Converters;

namespace PrayerApp.Tests.Converters;

// Locks the fallback path to mirror Colors.xaml token values exactly.
// Application.Current is null inside xUnit, so the public Convert() path always
// hits the fallback — covers light-mode answered/active. The static helpers
// expose both themes for full coverage.
public class BoolToMutedColorConverterTests
{
    // ── GetResourceKey ──────────────────────────

    [Fact]
    public void GetResourceKey_AnsweredLight_ReturnsGray400()
        => Assert.Equal("Gray400", BoolToMutedColorConverter.GetResourceKey(isAnswered: true, isDark: false));

    [Fact]
    public void GetResourceKey_AnsweredDark_ReturnsGray500()
        => Assert.Equal("Gray500", BoolToMutedColorConverter.GetResourceKey(isAnswered: true, isDark: true));

    [Fact]
    public void GetResourceKey_ActiveLight_ReturnsOffBlack()
        => Assert.Equal("OffBlack", BoolToMutedColorConverter.GetResourceKey(isAnswered: false, isDark: false));

    [Fact]
    public void GetResourceKey_ActiveDark_ReturnsWhite()
        => Assert.Equal("White", BoolToMutedColorConverter.GetResourceKey(isAnswered: false, isDark: true));

    // ── GetFallbackColor (must mirror Colors.xaml exactly) ──────────────────────────

    [Fact]
    public void GetFallbackColor_AnsweredLight_MirrorsGray400()
        => Assert.Equal(Color.FromArgb("#717171"), BoolToMutedColorConverter.GetFallbackColor(isAnswered: true, isDark: false));

    [Fact]
    public void GetFallbackColor_AnsweredDark_MirrorsGray500()
        => Assert.Equal(Color.FromArgb("#6E6E6E"), BoolToMutedColorConverter.GetFallbackColor(isAnswered: true, isDark: true));

    [Fact]
    public void GetFallbackColor_ActiveLight_MirrorsOffBlack()
        => Assert.Equal(Color.FromArgb("#1f1f1f"), BoolToMutedColorConverter.GetFallbackColor(isAnswered: false, isDark: false));

    [Fact]
    public void GetFallbackColor_ActiveDark_ReturnsWhite()
        => Assert.Equal(Colors.White, BoolToMutedColorConverter.GetFallbackColor(isAnswered: false, isDark: true));

    // ── Convert (public IValueConverter API — hits fallback because Application.Current is null in xUnit) ──────────────────────────

    [Fact]
    public void Convert_AnsweredLight_ReturnsFallbackMirroringGray400()
    {
        var sut = new BoolToMutedColorConverter();
        var result = sut.Convert(true, typeof(Color), null, CultureInfo.InvariantCulture);
        Assert.Equal(Color.FromArgb("#717171"), result);
    }

    [Fact]
    public void Convert_ActiveLight_ReturnsFallbackMirroringOffBlack()
    {
        var sut = new BoolToMutedColorConverter();
        var result = sut.Convert(false, typeof(Color), null, CultureInfo.InvariantCulture);
        Assert.Equal(Color.FromArgb("#1f1f1f"), result);
    }
}
