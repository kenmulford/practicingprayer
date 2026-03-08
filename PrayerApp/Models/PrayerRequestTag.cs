using System;
using SQLite;
using PrayerApp.Services;

namespace PrayerApp.Models
{
    [Table("PrayerRequestTag")]
    public class PrayerRequestTag
    {
        private static IDBService? _dbService;

        [PrimaryKey, AutoIncrement]
        [Column("Id")]
        public int Id { get; set; }

        [Column("PrayerRequestId"), Indexed]
        public int PrayerRequestId { get; set; }

        [Column("PrayerTagId"), Indexed]
        public int PrayerTagId { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        #region Static Methods
        public static void SetDBService(IDBService dbService)
        {
            _dbService = dbService;
        }
        #endregion

        #region Actions

        public async Task SaveAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerRequestTag.SetDBService at app startup.");
            if (Id == 0)
            {
                await _dbService.InsertAsync(this);
            }
            else
            {
                await _dbService.UpdateAsync(this);
            }
        }

        public async Task DeleteAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerRequestTag.SetDBService at app startup.");

            await _dbService.DeleteAsync(this);
        }

        public static async Task<PrayerRequestTag> LoadAsync(int _id)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerRequestTag.SetDBService at app startup.");

            return await _dbService.GetByIdAsync<PrayerRequestTag>(_id);
        }

        public static async Task<List<PrayerRequestTag>> LoadAllAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerRequestTag.SetDBService at app startup.");

            return await _dbService.GetAllAsync<PrayerRequestTag>();
        }

        public static async Task<List<PrayerRequestTag>> LoadByRequestIdAsync(int prayerRequestId)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerRequestTag.SetDBService at app startup.");

            return await _dbService.GetByRequestIdAsync(prayerRequestId);
        }

        public static async Task<List<PrayerRequestTag>> LoadByTagIdAsync(int prayerTagId)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerRequestTag.SetDBService at app startup.");

            return await _dbService.GetByTagIdAsync(prayerTagId);
        }

        #endregion
    }
}
