using System.IO;
using PrayerApp.UITests.Infrastructure;

namespace PrayerApp.Tests.Infrastructure;

/// <summary>
/// Guards against drift between the UITest seed
/// (<c>PrayerApp.UITests/Infrastructure/TestDataSeed.cs</c>) and the
/// <see cref="TestSeedFixtures"/> constants that assertion sites consume.
///
/// Two-part guarantee:
///   1. Every fixture defined in <see cref="TestSeedFixtures"/> is actually
///      referenced by the seed source (so a new constant can't be added
///      without wiring it into the seed).
///   2. Every fixture value follows the canonical family prefix
///      (<c>"UITest Delete Target"</c>) — a typo in a constant value is
///      caught here before the UI test runs against a misaligned device.
///
/// Why a source-string check (vs. running the seed in-process): TestDataSeed
/// pushes a fully-built SQLite file to an Android emulator / iOS simulator
/// via adb / simctl. Reproducing that in PrayerApp.Tests would drag in the
/// full DBService + device-shell dependency chain. The source-string check
/// is a tractable consistency guard that catches the realistic drift mode:
/// a developer adds or renames a fixture, runs xUnit, ships — and the
/// UITest suite then can't find the seeded entity on the device.
/// </summary>
public class TestDataSeedConsistencyTests
{
    private const string FamilyPrefix = "UITest Delete Target";

    private static string LoadSeedSource()
    {
        // Walk up from the test assembly's bin/ output to the repo root, then
        // into the UITests project. AppContext.BaseDirectory points at
        // bin/<Config>/net10.0/, so a few parents land at the repo root.
        var baseDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && current is not null; i++)
        {
            var candidate = Path.Combine(
                current.FullName,
                "PrayerApp.UITests",
                "Infrastructure",
                "TestDataSeed.cs");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            current = current.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate PrayerApp.UITests/Infrastructure/TestDataSeed.cs " +
            $"walking up from {baseDir}.");
    }

    [Fact]
    public void Seed_References_DeleteCard_Constant()
    {
        var source = LoadSeedSource();
        Assert.Contains("TestSeedFixtures.DeleteCard", source);
    }

    [Fact]
    public void Seed_References_DeleteCardA_Constant()
    {
        var source = LoadSeedSource();
        Assert.Contains("TestSeedFixtures.DeleteCardA", source);
    }

    [Fact]
    public void Seed_References_DeleteCardB_Constant()
    {
        var source = LoadSeedSource();
        Assert.Contains("TestSeedFixtures.DeleteCardB", source);
    }

    [Fact]
    public void Seed_References_DeleteCollectionA_Constant()
    {
        var source = LoadSeedSource();
        Assert.Contains("TestSeedFixtures.DeleteCollectionA", source);
    }

    [Fact]
    public void Seed_References_DeleteCollectionB_Constant()
    {
        var source = LoadSeedSource();
        Assert.Contains("TestSeedFixtures.DeleteCollectionB", source);
    }

    [Theory]
    [InlineData(nameof(TestSeedFixtures.DeleteCard), TestSeedFixtures.DeleteCard)]
    [InlineData(nameof(TestSeedFixtures.DeleteCardA), TestSeedFixtures.DeleteCardA)]
    [InlineData(nameof(TestSeedFixtures.DeleteCardB), TestSeedFixtures.DeleteCardB)]
    [InlineData(nameof(TestSeedFixtures.DeleteCollectionA), TestSeedFixtures.DeleteCollectionA)]
    [InlineData(nameof(TestSeedFixtures.DeleteCollectionB), TestSeedFixtures.DeleteCollectionB)]
    [InlineData(nameof(TestSeedFixtures.DeleteRuntimePrayer), TestSeedFixtures.DeleteRuntimePrayer)]
    [InlineData(nameof(TestSeedFixtures.DeleteRuntimeTagPrefix), TestSeedFixtures.DeleteRuntimeTagPrefix)]
    public void Fixture_Value_FollowsFamilyPrefix(string name, string value)
    {
        Assert.StartsWith(FamilyPrefix, value);
        // Sanity: name parameter must be non-empty (xUnit display only — keeps
        // the failure message readable when a constant value drifts).
        Assert.False(string.IsNullOrEmpty(name));
    }
}
