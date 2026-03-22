using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrayerApp.Services
{
    /// <summary>Maps a GROUP BY result row for latest interaction per prayer.</summary>
    public class LatestInteractionResult
    {
        public int PrayerId { get; set; }
        public DateTime LatestInteractionAt { get; set; }
    }

    public interface IDBService
    {
        // SQLite requires parameterless constructors in the models
        Task<List<T>> GetAllAsync<T>() where T : new();
        Task<T> GetByIdAsync<T>(int Id) where T : new();
        Task<int> InsertAsync<T>(T item);
        Task<int> UpdateAsync<T>(T item);
        Task<int> DeleteAsync<T>(T item);
        Task<int> DropTableAsync<T>() where T : new();

        // PrayerCardTag specific queries
        Task<List<PrayerApp.Models.PrayerCardTag>> GetByRequestIdAsync(int prayerRequestId);
        Task<List<PrayerApp.Models.PrayerCardTag>> GetByTagIdAsync(int prayerTagId);
        Task<int> DeleteByTagIdAsync(int prayerTagId);
        Task<int> DeleteJunctionRowsByRequestIdAsync(int prayerRequestId);

        Task<List<PrayerApp.Models.Prayer>> GetPrayersByCardIdAsync(int prayerCardId);
        Task<List<PrayerApp.Models.PrayerInteraction>> GetInteractionsByPrayerIdAsync(int prayerId);

        // PrayerInteraction aggregate queries (SQL-level, avoids loading all rows)
        Task<List<LatestInteractionResult>> GetLatestInteractionByPrayerAsync();
        Task<DateTime?> GetMaxInteractionDateAsync();
        Task<int> DeleteInteractionsByPrayerIdAsync(int prayerId);

        Task SeedDataAsync();

        Task UpdateSchema();

        Task CloseAsync();
        Task ReinitializeAsync(string path);
    }
}
