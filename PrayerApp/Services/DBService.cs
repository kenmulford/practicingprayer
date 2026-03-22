using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

using PrayerApp.Models;


namespace PrayerApp.Services
{
    class DBService : IDBService
    {
        private SQLiteAsyncConnection? _db;
        private readonly Task _initTask;

        public DBService(string dbPath)
        {
            // Startup recovery check — runs synchronously before any connection is opened.
            // Handles any state left behind by a force-close mid-restore.
            RunStartupRecovery(dbPath);

            _db = new SQLiteAsyncConnection(dbPath);

            // Start table creation + schema migration asynchronously.
            // All public DB methods await _initTask before proceeding,
            // so callers never hit an uninitialized database.
            _initTask = UpdateSchema();
        }

        /// <summary>
        /// Awaits the initial table creation and schema migration.
        /// Completes instantly after the first call since Task caches its result.
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            await _initTask;
        }

        private static void RunStartupRecovery(string dbPath)
        {
            var dir = Path.GetDirectoryName(dbPath)!;
            var restorePath = Path.Combine(dir, "prayer_app_restore.db");
            var backupTmpPath = Path.Combine(dir, "prayer_app_backup.tmp");

            bool restoreExists = File.Exists(restorePath);
            bool dbExists = File.Exists(dbPath);
            bool backupTmpExists = File.Exists(backupTmpPath);

            if (restoreExists && backupTmpExists)
            {
                // Both stale — impossible by construction but handle defensively
                File.Delete(restorePath);
                File.Delete(backupTmpPath);
            }
            else if (restoreExists && dbExists)
            {
                // Write interrupted — original intact; discard partial restore
                File.Delete(restorePath);
            }
            else if (restoreExists && !dbExists)
            {
                // Swap interrupted mid-flight — complete the rename
                File.Move(restorePath, dbPath);
            }
            else if (backupTmpExists && dbExists)
            {
                // Swap completed, stale backup remains
                File.Delete(backupTmpPath);
            }
            else if (backupTmpExists && !dbExists)
            {
                // Catastrophic swap failure — roll back to original
                File.Move(backupTmpPath, dbPath);
            }
            // else: normal startup — nothing to do
        }

        public async Task UpdateSchema()
        {
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            await _db.CreateTableAsync<PrayerCard>();
            await _db.CreateTableAsync<Prayer>();
            await _db.CreateTableAsync<PrayerTag>();
            await _db.CreateTableAsync<PrayerCardTag>();
            await _db.CreateTableAsync<PrayerInteraction>();
            await _db.CreateTableAsync<UserColor>();

            await EnsurePrayerCardColumnsAsync();

            try
            {
                await _db.ExecuteAsync("DROP TABLE IF EXISTS PrayerRequestTag");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateSchema] PrayerRequestTag migration: {ex.Message}");
            }

            // One-time cleanup: remove orphaned PrayerInteraction and PrayerCardTag rows
            // left behind by prior deletions that didn't cascade.
            try
            {
                await _db.ExecuteAsync(
                    "DELETE FROM PrayerInteraction WHERE PrayerId NOT IN (SELECT Id FROM PrayerRequest)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateSchema] Orphan PrayerInteraction cleanup: {ex.Message}");
            }

            try
            {
                // PrayerRequestId == 0 rows are legacy card-level rows handled by BUG-21 migration
                await _db.ExecuteAsync(
                    "DELETE FROM PrayerCardTag WHERE PrayerRequestId > 0 AND PrayerRequestId NOT IN (SELECT Id FROM PrayerRequest)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateSchema] Orphan PrayerCardTag cleanup: {ex.Message}");
            }

            // Add IsDefault column to UserColor and backfill existing seed colors
            try
            {
                await _db.ExecuteAsync("ALTER TABLE UserColor ADD COLUMN IsDefault INTEGER DEFAULT 0");
            }
            catch { /* Column already exists */ }

            try
            {
                // Mark the 8 original seed colors as defaults (by hex value)
                var seedHexValues = new[] {
                    "#B84040", "#B35A20", "#7A4020", "#1E7870",
                    "#2E5A9A", "#663C8C", "#8C3860", "#505050"
                };
                foreach (var hex in seedHexValues)
                {
                    await _db.ExecuteAsync(
                        "UPDATE UserColor SET IsDefault = 1 WHERE UPPER(HexValue) = ?",
                        hex.ToUpperInvariant());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateSchema] UserColor IsDefault backfill: {ex.Message}");
            }
        }

        public async Task CloseAsync()
        {
            await EnsureInitializedAsync();
            if (_db == null) return;
            // ExecuteScalarAsync handles the result set PRAGMA wal_checkpoint returns
            // (busy, log, checkpointed columns). ExecuteAsync calls ExecuteNonQuery
            // which throws SQLiteException("not an error") on SQLITE_ROW being returned.
            await _db.ExecuteScalarAsync<int>("PRAGMA wal_checkpoint(TRUNCATE)");
            await _db.CloseAsync();
            _db = null;
        }

        public async Task ReinitializeAsync(string path)
        {
            _db = new SQLiteAsyncConnection(path);
            await UpdateSchema();
        }

        public async Task<List<T>> GetAllAsync<T>() where T : new()
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.Table<T>().ToListAsync();
        }

        public async Task<T> GetByIdAsync<T>(int id) where T : new()
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.FindAsync<T>(id);
        }

        public async Task<int> InsertAsync<T>(T item)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.InsertAsync(item);
        }

        public async Task<int> UpdateAsync<T>(T item)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.UpdateAsync(item);
        }

        public async Task<int> DeleteAsync<T>(T item)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.DeleteAsync(item);
        }

        public async Task<int> DropTableAsync<T>() where T : new()
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.DropTableAsync<T>();
        }

        public async Task<List<PrayerCardTag>> GetByRequestIdAsync(int prayerRequestId)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.Table<PrayerCardTag>()
                .Where(pct => pct.PrayerRequestId == prayerRequestId)
                .ToListAsync();
        }

        public async Task<List<PrayerCardTag>> GetByTagIdAsync(int prayerTagId)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.Table<PrayerCardTag>()
                .Where(pct => pct.PrayerTagId == prayerTagId)
                .ToListAsync();
        }

        public async Task<int> DeleteByTagIdAsync(int prayerTagId)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.ExecuteAsync(
                "DELETE FROM PrayerCardTag WHERE PrayerTagId = ?",
                prayerTagId
            );
        }

        public async Task<int> DeleteJunctionRowsByRequestIdAsync(int prayerRequestId)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.ExecuteAsync(
                "DELETE FROM PrayerCardTag WHERE PrayerRequestId = ?",
                prayerRequestId
            );
        }

        public async Task<List<LatestInteractionResult>> GetLatestInteractionByPrayerAsync()
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.QueryAsync<LatestInteractionResult>(
                "SELECT PrayerId, MAX(InteractionAt) AS LatestInteractionAt FROM PrayerInteraction GROUP BY PrayerId");
        }

        public async Task<DateTime?> GetMaxInteractionDateAsync()
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            var result = await _db.ExecuteScalarAsync<string>(
                "SELECT MAX(InteractionAt) FROM PrayerInteraction");
            return result is not null ? DateTime.Parse(result) : null;
        }

        public async Task<int> DeleteInteractionsByPrayerIdAsync(int prayerId)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.ExecuteAsync(
                "DELETE FROM PrayerInteraction WHERE PrayerId = ?", prayerId);
        }

        public async Task SeedDataAsync()
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
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

        public async Task<List<Prayer>> GetPrayersByCardIdAsync(int prayerCardId)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.Table<Prayer>()
                .Where(prayer => prayer.PrayerCardId == prayerCardId)
                .ToListAsync();
        }

        public async Task<List<PrayerInteraction>> GetInteractionsByPrayerIdAsync(int prayerId)
        {
            await EnsureInitializedAsync();
            if (_db == null) throw new InvalidOperationException("Database is not available.");
            return await _db.Table<PrayerInteraction>()
                .Where(i => i.PrayerId == prayerId)
                .ToListAsync();
        }

        private async Task EnsurePrayerCardColumnsAsync()
        {
            // _db is guaranteed non-null here — callers hold the null guard
            try
            {
                await _db!.ExecuteAsync("ALTER TABLE PrayerCard ADD COLUMN IsFavorite INTEGER DEFAULT 0");
            }
            catch
            {
            }

            try
            {
                await _db!.ExecuteAsync("ALTER TABLE PrayerRequest ADD COLUMN AnsweredAt TEXT");
            }
            catch
            {
            }

            // BUG-21: Add PrayerRequestId column to PrayerCardTag — tags now live at the request level
            try
            {
                await _db!.ExecuteAsync("ALTER TABLE PrayerCardTag ADD COLUMN PrayerRequestId INTEGER DEFAULT 0");
            }
            catch
            {
            }

            // BUG-21 data migration: promote legacy card-level tag rows to request-level rows.
            // For each PrayerCardTag row where PrayerRequestId is still 0 (old schema),
            // find all prayer requests on that card and create a new row per request.
            try
            {
                await MigrateCardTagsToRequestTagsAsync();
            }
            catch (Exception ex)
            {
                // Non-fatal: app continues, but existing tag assignments may not be migrated.
                // The exception message here will surface in device logs for diagnosis.
                System.Diagnostics.Debug.WriteLine($"[BUG-21 Migration] MigrateCardTagsToRequestTagsAsync failed — {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BUG-21 Migration] Stack: {ex.StackTrace}");
            }
        }

        private async Task MigrateCardTagsToRequestTagsAsync()
        {
            if (_db == null) return;

            // Guard: verify PrayerRequestId column is present before querying it.
            // If the ALTER TABLE above failed silently, the column may not exist yet,
            // which would cause a SQLiteException here and crash the app on startup.
            var columnExists = false;
            try
            {
                var rows = await _db.QueryAsync<SQLiteColumnInfo>("PRAGMA table_info(PrayerCardTag)");
                columnExists = rows.Any(r => r.name == "PrayerRequestId");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BUG-21 Migration] PRAGMA table_info failed: {ex.Message}");
                return;
            }

            if (!columnExists)
            {
                System.Diagnostics.Debug.WriteLine("[BUG-21 Migration] PrayerRequestId column not found — skipping migration.");
                return;
            }

            // Find all legacy rows (PrayerRequestId == 0)
            var legacyRows = await _db.Table<PrayerCardTag>()
                .Where(pct => pct.PrayerRequestId == 0)
                .ToListAsync();

            if (legacyRows.Count == 0) return;

            System.Diagnostics.Debug.WriteLine($"[BUG-21 Migration] Migrating {legacyRows.Count} legacy card-level tag row(s).");

            foreach (var legacy in legacyRows)
            {
                // Get all prayer requests on the card this tag was assigned to
                var prayers = await _db.Table<Prayer>()
                    .Where(p => p.PrayerCardId == legacy.PrayerCardId)
                    .ToListAsync();

                foreach (var prayer in prayers)
                {
                    // Avoid duplicates
                    var existing = await _db.Table<PrayerCardTag>()
                        .Where(pct => pct.PrayerRequestId == prayer.Id && pct.PrayerTagId == legacy.PrayerTagId)
                        .FirstOrDefaultAsync();

                    if (existing == null)
                    {
                        await _db.InsertAsync(new PrayerCardTag
                        {
                            PrayerCardId = legacy.PrayerCardId,
                            PrayerTagId = legacy.PrayerTagId,
                            PrayerRequestId = prayer.Id,
                            CreatedAt = legacy.CreatedAt
                        });
                    }
                }

                // Remove the legacy card-level row now that it's been migrated
                await _db.DeleteAsync(legacy);
            }

            System.Diagnostics.Debug.WriteLine("[BUG-21 Migration] Migration complete.");
        }

        /// <summary>Maps a single row from PRAGMA table_info(...) for column existence checks.</summary>
        private class SQLiteColumnInfo
        {
            public string name { get; set; } = string.Empty;
        }
    }
}
