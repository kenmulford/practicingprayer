using System.IO.Compression;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;

namespace PrayerApp.Services;

public class BackupService : IBackupService
{
    private readonly IDBService _dbService;
    private readonly IFileSaver _fileSaver;
    private readonly string _dbPath;

    public BackupService(IDBService dbService, IFileSaver fileSaver)
    {
        _dbService = dbService;
        _fileSaver = fileSaver;
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "prayer_app.db");
    }

    public async Task<bool> ExportAsync()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var fileName = $"prayer_cards_{today}.pcrd";
        var tempZipPath = Path.Combine(FileSystem.CacheDirectory, fileName);

        try
        {
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

            // Invoke OS save dialog
            await using var fileStream = File.OpenRead(tempZipPath);
            var result = await _fileSaver.SaveAsync(fileName, fileStream, CancellationToken.None);

            if (result.IsSuccessful)
            {
                await Toast.Make("Backup saved").Show();
                return true;
            }
            else
            {
                await Toast.Make("Backup cancelled").Show();
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackupService.ExportAsync] {ex}");
            await Toast.Make("Backup failed").Show();
            return false;
        }
        finally
        {
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);
        }
    }

    public async Task<bool> ImportAsync()
    {
        // Pick file BEFORE showing any modal (iOS constraint: UIDocumentPicker conflicts with modal)
        FileResult? picked = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = "Select a Prayer Cards backup (.pcrd)"
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
                    "This file doesn't appear to be a valid Prayer Cards backup.", "OK");
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
                "This file doesn't appear to be a valid Prayer Cards backup.", "OK");
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
