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
        }

        public async Task UpdateSchema()
        {
            await _db.CreateTableAsync<PrayerCategory>(); // Ensure table is created
            await _db.CreateTableAsync<Prayer>(); // Ensure table is created
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
                // Seed initial data
                await InsertAsync(new PrayerCategory { Name = "General", SortOrder = 0 });
                await InsertAsync(new PrayerCategory { Name = "Where I Live", SortOrder = 1 });
                await InsertAsync(new PrayerCategory { Name = "Where I Work", SortOrder = 2 });
                await InsertAsync(new PrayerCategory { Name = "Where I Play", SortOrder = 3 });
            }

        }
    }
}
