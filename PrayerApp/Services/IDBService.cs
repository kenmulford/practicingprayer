using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrayerApp.Services
{
    public interface IDBService
    {
        // SQLite requires parameterless constructors in the models
        Task<List<T>> GetAllAsync<T>() where T : new();
        Task<T> GetByIdAsync<T>(int Id) where T : new();
        Task<int> InsertAsync<T>(T item);
        Task<int> UpdateAsync<T>(T item);
        Task<int> DeleteAsync<T>(T item);
        Task<int> DropTableAsync<T>() where T : new();

        // PrayerRequestTag specific queries
        Task<List<PrayerApp.Models.PrayerRequestTag>> GetByRequestIdAsync(int prayerRequestId);
        Task<List<PrayerApp.Models.PrayerRequestTag>> GetByTagIdAsync(int prayerTagId);
        Task<int> DeleteByRequestIdAsync(int prayerRequestId);
        Task<int> DeleteByTagIdAsync(int prayerTagId);

        Task<List<PrayerApp.Models.Prayer>> GetPrayersByCardIdAsync(int prayerCardId);

        Task SeedDataAsync();

        Task UpdateSchema();
    }
}
