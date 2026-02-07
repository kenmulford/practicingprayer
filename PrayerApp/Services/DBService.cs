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
        private readonly SQLiteAsyncConnection _db;

        public DBService(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
            _db.CreateTableAsync<PrayerCard>().Wait(); // Create Table
            _db.CreateTableAsync<Prayer>().Wait(); // Create Prayer/Request Table
            _db.CreateTableAsync<PrayerTag>().Wait(); // Create Table
            _db.CreateTableAsync<PrayerRequestTag>().Wait(); // Create Table
        }

        public async Task UpdateSchema()
        {
            await _db.CreateTableAsync<PrayerCard>(); // Ensure table is created
            await _db.CreateTableAsync<Prayer>(); // Ensure prayer/request table is created
            await _db.CreateTableAsync<PrayerTag>(); // Ensure table is created
            await _db.CreateTableAsync<PrayerRequestTag>(); // Ensure table is created
        }

        public async Task<List<T>> GetAllAsync<T>() where T : new()
        {
            return await _db.Table<T>().ToListAsync();
        }

        public async Task<T> GetByIdAsync<T>(int id) where T : new()
        {
            return await _db.FindAsync<T>(id);
        }

        public async Task<int> InsertAsync<T>(T item)
        {
            return await _db.InsertAsync(item);
        }

        public async Task<int> UpdateAsync<T>(T item)
        {
            return await _db.UpdateAsync(item);
        }

        public async Task<int> DeleteAsync<T>(T item)
        {
            return await _db.DeleteAsync(item);
        }

        public async Task<int> DropTableAsync<T>() where T : new()
        {
            return await _db.DropTableAsync<T>();
        }

        public async Task<List<PrayerRequestTag>> GetByRequestIdAsync(int prayerRequestId)
        {
            return await _db.Table<PrayerRequestTag>()
                .Where(prt => prt.PrayerRequestId == prayerRequestId)
                .ToListAsync();
        }

        public async Task<List<PrayerRequestTag>> GetByTagIdAsync(int prayerTagId)
        {
            return await _db.Table<PrayerRequestTag>()
                .Where(prt => prt.PrayerTagId == prayerTagId)
                .ToListAsync();
        }

        public async Task<int> DeleteByRequestIdAsync(int prayerRequestId)
        {
            return await _db.ExecuteAsync(
                "DELETE FROM PrayerRequestTag WHERE PrayerRequestId = ?",
                prayerRequestId
            );
        }

        public async Task<int> DeleteByTagIdAsync(int prayerTagId)
        {
            return await _db.ExecuteAsync(
                "DELETE FROM PrayerRequestTag WHERE PrayerTagId = ?",
                prayerTagId
            );
        }

        public async Task SeedDataAsync()
        {
            if (PrayerApp.Services.Settings.FirstRun)
            {
                await DropSyncDataAsync();

                // Seed single prayer card
                var generalCard = new PrayerCard
                {
                    Title = "General",
                    Details = "Prayer requests",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await InsertAsync(generalCard);

                //TODO: Seed initial tags here when tag taxonomy is decided
                await InsertAsync(new PrayerTag { Name = "Urgent", Color = "#FF0000" });
                await InsertAsync(new PrayerTag { Name = "Family", Color = "#0000FF" });
                await InsertAsync(new PrayerTag { Name = "Work", Color = "#00FF00" });

                // Seed original prayer request items - all attached to General card
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
        }

        private async Task DropSyncDataAsync()
        {
            // Drop new schema tables first
            await DropTableAsync<PrayerRequestTag>();
            await _db.CreateTableAsync<PrayerRequestTag>();

            await DropTableAsync<PrayerTag>();
            await _db.CreateTableAsync<PrayerTag>();

            await DropTableAsync<Prayer>();
            await _db.CreateTableAsync<Prayer>();

            await DropTableAsync<PrayerCard>();
            await _db.CreateTableAsync<PrayerCard>();

            // Also drop legacy tables to avoid duplicate data when migrating
            try
            {
                await DropTableAsync<PrayerInteraction>();
            }
            catch {  }

            try
            {
                await DropTableAsync<PrayerCategory>();
            }
            catch {  }

            try
            {
                // Also drop old PrayerCardTag if it exists
                await _db.ExecuteAsync("DROP TABLE IF EXISTS PrayerCardTag");
            }
            catch {  }
        }
    }
}
