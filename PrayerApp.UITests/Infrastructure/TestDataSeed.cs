using System.Diagnostics;
using PrayerApp.Models;
using PrayerApp.Services;

namespace PrayerApp.UITests.Infrastructure;

/// <summary>
/// Builds a deterministic SQLite seed DB on the host and pushes it to the Android
/// emulator's app data dir before each suite run. Guarantees tests start with a
/// known baseline: user Collections, Cards, Prayers, answered/favorite/tagged
/// examples — so data-dependent tests don't fail just because the emulator is
/// fresh or a destructive test wiped state on a prior run.
///
/// Uses the app's real DBService so the seed schema is always in sync with the
/// app. No-op on iOS for now (follow-on pass once Android is green).
/// </summary>
internal static class TestDataSeed
{
    public static async Task SeedAndroidAsync()
    {
        if (!TestConfig.IsAndroid) return;

        // Always seed to a fresh temp file — guarantees a clean baseline on each run.
        // If the SeedDataAsync idempotency check (cardCount > 0) ever fired against a
        // pre-populated file, our Family/Work/Friends seed would duplicate; starting
        // from empty avoids that entirely.
        string tempPath = Path.Combine(Path.GetTempPath(), $"prayer_seed_{Guid.NewGuid():N}.db");

        try
        {
            await BuildSeedDbAsync(tempPath);
            await PushSeedToDeviceAsync(tempPath);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static async Task BuildSeedDbAsync(string path)
    {
        var db = new DBService(path);

        // Wire model static so model.SaveAsync() works during seed.
        // These statics are process-local — they don't affect the app on the device,
        // which runs in a separate process with its own statics.
        PrayerCard.SetDBService(db);
        PrayerTag.SetDBService(db);
        PrayerCardTag.SetDBService(db);
        Prayer.SetDBService(db);
        PrayerInteraction.SetDBService(db);
        CardBox.SetDBService(db);

        // UpdateSchema runs automatically in the constructor, but SeedDataAsync
        // awaits it internally. Calling SeedDataAsync creates the app's first-run
        // baseline ("General" card, three default tags, sample prayers).
        await db.SeedDataAsync();

        // Layer on UITest-specific baseline data.
        await SeedUITestContentAsync();

        await db.CloseAsync();
    }

    private static async Task SeedUITestContentAsync()
    {
        // Three user-created collections (Family / Work / Friends) with cards + prayers,
        // plus a "UITest Collection" / "UITest Card" / "UI Test Prayer" set used by
        // existing tests that probe for those specific names.

        var now = DateTime.Now;

        var familyBox = new CardBox { Name = "Family", IsSystem = false, SortOrder = 0, CreatedAt = now, UpdatedAt = now };
        await familyBox.SaveAsync();

        var workBox = new CardBox { Name = "Work", IsSystem = false, SortOrder = 0, CreatedAt = now, UpdatedAt = now };
        await workBox.SaveAsync();

        var friendsBox = new CardBox { Name = "Friends", IsSystem = false, SortOrder = 0, CreatedAt = now, UpdatedAt = now };
        await friendsBox.SaveAsync();

        var uitestBox = new CardBox { Name = "UITest Collection", IsSystem = false, SortOrder = 0, CreatedAt = now, UpdatedAt = now };
        await uitestBox.SaveAsync();

        await SeedCardWithPrayersAsync(familyBox.Id, "Parents", new[]
        {
            ("Dad's health", "Recovery from surgery.", false),
            ("Mom's travel", "Safe trip home.", false),
        });
        await SeedCardWithPrayersAsync(familyBox.Id, "Kids", new[]
        {
            ("School year", "Strength for the new term.", false),
            ("Friendships", "Kind friends at school.", false),
            ("Sports season", "Safety and fun.", false),
        });

        await SeedCardWithPrayersAsync(workBox.Id, "Team", new[]
        {
            ("Big launch", "Smooth release.", false),
            ("Hiring", "Right candidates.", false),
        }, isFavorite: true);

        await SeedCardWithPrayersAsync(friendsBox.Id, "Small Group", new[]
        {
            ("Weekly meeting", "Meaningful discussion.", false),
            ("Chris's job search", "Open doors.", true), // Answered
        });
        await SeedCardWithPrayersAsync(friendsBox.Id, "College", new[]
        {
            ("Class of '22 reunion", "Good turnout.", false),
        });

        // UITest Card lives at top level (BoxId = 0, "Loose Cards") — NOT inside
        // UITest Collection. User-box accordion sections render collapsed by default
        // on the Prayer Cards page, which would hide a card inside them from the UI
        // tree and break any test that scrolls for "UITest Card" by visible text.
        // Loose Cards renders flat and is always visible.
        await SeedCardWithPrayersAsync(boxId: 0, "UITest Card", new[]
        {
            ("UI Test Prayer", "Canonical test prayer used by existing UI tests.", false),
        });

        // Dedicated throwaway targets for destructive tests. Each delete test
        // targets its own "Delete Me" entry instead of the shared UITest
        // baseline — so UITest Collection / UITest Card remain stable for
        // downstream non-destructive tests regardless of run order.
        var deleteColA = new CardBox { Name = "Delete Me Collection A", IsSystem = false, SortOrder = 0, CreatedAt = now, UpdatedAt = now };
        await deleteColA.SaveAsync();
        await SeedCardWithPrayersAsync(deleteColA.Id, "Delete Me Card A", new[]
        {
            ("Throwaway prayer A", "Deleted by Boxes_DeleteCollection_DeleteAllCards.", false),
        });

        var deleteColB = new CardBox { Name = "Delete Me Collection B", IsSystem = false, SortOrder = 0, CreatedAt = now, UpdatedAt = now };
        await deleteColB.SaveAsync();
        await SeedCardWithPrayersAsync(deleteColB.Id, "Delete Me Card B", new[]
        {
            ("Throwaway prayer B", "Deleted by Boxes_DeleteCollection_UnassignCards.", false),
        });

        // Standalone throwaway card for Cards_DeleteCard_RemovesFromList.
        // Lives at top level (BoxId = 0, "Loose Cards") so it's always visible —
        // user boxes render as collapsed accordion sections on first load and
        // would hide the card from the UI tree.
        await SeedCardWithPrayersAsync(boxId: 0, "Delete Me Card", new[]
        {
            ("Throwaway prayer", "Deleted by Cards_DeleteCard_RemovesFromList.", false),
        });
    }

    private static async Task SeedCardWithPrayersAsync(int boxId, string cardTitle,
        (string Title, string Details, bool Answered)[] prayers, bool isFavorite = false)
    {
        var now = DateTime.Now;
        var card = new PrayerCard
        {
            Title = cardTitle,
            BoxId = boxId,
            IsFavorite = isFavorite,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await card.SaveAsync();

        foreach (var (title, details, answered) in prayers)
        {
            var prayer = new Prayer
            {
                PrayerCardId = card.Id,
                Title = title,
                Details = details,
                IsAnswered = answered,
                AnsweredAt = answered ? now : null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await prayer.SaveAsync();
        }
    }

    private static async Task PushSeedToDeviceAsync(string localPath)
    {
        // Per `feedback_android_sqlite_seeding.md`:
        //  1. force-stop the app (else SQLite locks block replacement)
        //  2. adb push to /data/local/tmp (app data dir is restricted to app UID)
        //  3. `run-as` cp into the app's files/prayer_app.db
        //  4. remove any stale WAL/SHM sidecar files so SQLite doesn't merge them
        //     into the freshly seeded DB on next app launch

        var pkg = TestConfig.AndroidPackage;
        var stagePath = TestConfig.AndroidTmpSeedPath;
        var appDb = TestConfig.AndroidAppDbRelativePath;

        await RunAdbAsync($"shell am force-stop {pkg}");

        // Clean any pre-existing tmp seed
        await RunAdbAsync($"shell rm -f {stagePath}", allowFailure: true);

        await RunAdbAsync($"push \"{localPath}\" {stagePath}");

        await RunAdbAsync($"shell run-as {pkg} cp {stagePath} {appDb}");

        // Remove stale WAL/SHM sidecars — if they linger from a prior run, SQLite
        // will replay them on top of our seed and the baseline is no longer clean.
        await RunAdbAsync($"shell run-as {pkg} rm -f {appDb}-wal {appDb}-shm",
            allowFailure: true);

        await RunAdbAsync($"shell rm -f {stagePath}", allowFailure: true);
    }

    /// <summary>Runs an adb command. Throws if adb exits non-zero unless allowFailure is true.</summary>
    private static async Task RunAdbAsync(string arguments, bool allowFailure = false)
    {
        var psi = new ProcessStartInfo("adb", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start adb. Is Android SDK platform-tools on PATH?");

        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0 && !allowFailure)
        {
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException(
                $"adb {arguments} failed (exit {proc.ExitCode}).\nstdout: {stdout}\nstderr: {stderr}");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
