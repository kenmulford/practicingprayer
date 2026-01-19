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
            _db.CreateTableAsync<PrayerCategory>().Wait(); // Create Table
            _db.CreateTableAsync<Prayer>().Wait(); // Create Table
            _db.CreateTableAsync<PrayerInteraction>().Wait(); // Create Table
        }

        public async Task UpdateSchema()
        {
            await _db.CreateTableAsync<PrayerCategory>(); // Ensure table is created
            await _db.CreateTableAsync<Prayer>(); // Ensure table is created
            await _db.CreateTableAsync<PrayerInteraction>(); // Ensure table is created
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

        public async Task SeedDataAsync()
        {

            if (Settings.FirstRun)
            {
                
                await DropSyncDataAsync();

                // Seed initial data
                await InsertAsync(new PrayerCategory { Name = "General", IsFavorite = false });
                await InsertAsync(new PrayerCategory { Name = "Where I Live", IsFavorite = true });
                await InsertAsync(new PrayerCategory { Name = "Where I Work", IsFavorite = false });
                await InsertAsync(new PrayerCategory { Name = "Where I Play", IsFavorite = false });

                await InsertAsync(new Prayer {
                    PrayerCategoryId = 1, 
                    Title = "Sample Prayer Entry 1",
                    Details = "Sample details.", 
                    CreatedAt = DateTime.UtcNow, 
                    UpdatedAt = DateTime.UtcNow } 
                );

                await InsertAsync(new Prayer
                {
                    PrayerCategoryId = 2,
                    Title = "Sample Prayer Entry 2",
                    Details = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis in sem sit amet sapien tincidunt pretium. Mauris tristique libero tellus, laoreet blandit metus congue non. Ut at sagittis lacus. ",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
                );

                await InsertAsync(new Prayer
                {
                    PrayerCategoryId = 3,
                    Title = "Sample Prayer Entry 3",
                    Details = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis in sem sit amet sapien tincidunt pretium. Mauris tristique libero tellus, laoreet blandit metus congue non. Ut at sagittis lacus. Nullam in felis quam. Phasellus nisi augue, hendrerit non vulputate fermentum, maximus a risus.",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
                );

                await InsertAsync(new Prayer
                {
                    PrayerCategoryId = 4,
                    Title = "Sample Prayer Entry 4",
                    Details = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis in sem sit amet sapien tincidunt pretium. Mauris tristique libero tellus, laoreet blandit metus congue non. Ut at sagittis lacus. Nullam in felis quam. Phasellus nisi augue, hendrerit non vulputate fermentum, maximus a risus. Phasellus aliquam fringilla libero et feugiat. Nam eget varius mi. Curabitur sit amet rutrum sem. Morbi ut ipsum ex. Nulla est ante, hendrerit vitae mollis quis, fringilla id ligula. Vestibulum id nisi sed nunc finibus egestas. Phasellus eleifend ante at enim ornare auctor a ac dolor. Nullam nec nisi vulputate, ultrices nisi quis, bibendum ligula. Proin fermentum mauris nec ipsum ultrices gravida. Sed faucibus scelerisque massa at porttitor.",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
                );
            }
        }

        private async Task DropSyncDataAsync()
        {
            await DropTableAsync<PrayerCategory>();
            await _db.CreateTableAsync<PrayerCategory>();

            await DropTableAsync<Prayer>();
            await _db.CreateTableAsync<Prayer>();
        }
    }
}
