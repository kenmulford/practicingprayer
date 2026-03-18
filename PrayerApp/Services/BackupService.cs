using System.IO.Compression;
using CommunityToolkit.Maui.Alerts;

namespace PrayerApp.Services;

public class BackupService : IBackupService
{
    private readonly IDBService _dbService;
    private readonly string _dbPath;

    public BackupService(IDBService dbService)
    {
        _dbService = dbService;
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "prayer_app.db");
    }

    public async Task<bool> ExportAsync()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var fileName = $"practicing_prayer_{today}.pcrd";
        var tempZipPath = Path.Combine(FileSystem.CacheDirectory, fileName);

        try
        {
            // Clear any stale backup files from cache before creating a new one.
            // We do NOT delete after sharing because the receiving app (Google Drive,
            // Files, etc.) reads the file asynchronously after the share sheet closes.
            // The OS will evict the cache directory on its own schedule.
            foreach (var old in Directory.GetFiles(FileSystem.CacheDirectory, "*.pcrd"))
                File.Delete(old);

            // Close DB with WAL checkpoint to ensure the .db file is complete
            await _dbService.CloseAsync();

            // Read the DB bytes while the connection is closed
            byte[] dbBytes = await File.ReadAllBytesAsync(_dbPath);

            // Reopen the DB immediately — connection is unavailable for milliseconds only
            await _dbService.ReinitializeAsync(_dbPath);

            // Build the .pcrd ZIP in the cache directory
            await using (var zipStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write))
            {
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
                var entry = archive.CreateEntry("prayer_app.db", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(dbBytes);
            }

            // Share via OS share sheet — lets user save to Google Drive, Files, email, etc.
            // More reliable than IFileSaver on Android (avoids onActivityResult crash on API 36).
            // Share.RequestAsync returns immediately after dispatching the intent —
            // there is no completion callback, so no success toast here.
            // The share sheet itself is the UX confirmation.
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Save Practicing Prayer Backup",
                File = new ShareFile(tempZipPath, "application/zip")
            });

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackupService.ExportAsync] {ex}");
            await Toast.Make("Backup failed").Show();
            return false;
        }
    }

    public async Task<bool> ImportAsync()
    {
        // Pick file BEFORE showing any modal (iOS constraint: UIDocumentPicker conflicts with modal)
        FileResult? picked = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Select a Practicing Prayer backup (.pcrd)"
        });
        if (picked is null) return false;

        // Validate: must be a ZIP containing prayer_app.db
        byte[] dbBytes;
        try
        {
            await using var pickedStream = await picked.OpenReadAsync();
            using var archive = new ZipArchive(pickedStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry("prayer_app.db");
            if (entry is null)
            {
                await Shell.Current.DisplayAlertAsync("Invalid Backup",
                    "This file doesn't appear to be a valid Practicing Prayer backup.", "OK");
                return false;
            }
            await using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms);
            dbBytes = ms.ToArray();
        }
        catch
        {
            await Shell.Current.DisplayAlertAsync("Invalid Backup",
                "This file doesn't appear to be a valid Practicing Prayer backup.", "OK");
            return false;
        }

        // Push blocking modal — only after successful validation
        var progressPage = new Views.Backup.RestoreProgressPage();
        await Shell.Current.Navigation.PushModalAsync(progressPage);

        try
        {
            var dir = Path.GetDirectoryName(_dbPath)!;
            var restorePath = Path.Combine(dir, "prayer_app_restore.db");
            var backupTmpPath = Path.Combine(dir, "prayer_app_backup.tmp");

            // Phase 1 — Write incoming DB (original untouched)
            await File.WriteAllBytesAsync(restorePath, dbBytes);

            // Phase 2 — Swap
            await _dbService.CloseAsync();
            File.Move(_dbPath, backupTmpPath, overwrite: true);
            File.Move(restorePath, _dbPath, overwrite: true);

            // Phase 3 — Reinitialize, cleanup, navigate
            await _dbService.ReinitializeAsync(_dbPath);
            File.Delete(backupTmpPath);

            await Shell.Current.GoToAsync("//MainPage");
            await Shell.Current.Navigation.PopModalAsync();
            await Toast.Make("Restore complete").Show();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackupService.ImportAsync] {ex}");
            await Shell.Current.Navigation.PopModalAsync();
            await Shell.Current.DisplayAlertAsync("Restore Failed",
                "Restore failed. Please restart the app.", "OK");
            return false;
        }
    }
}
