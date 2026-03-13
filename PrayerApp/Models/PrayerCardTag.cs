using System;
using SQLite;
using PrayerApp.Services;

namespace PrayerApp.Models
{
    [Table("PrayerCardTag")]
    public class PrayerCardTag
    {
        private static IDBService? _dbService;

        [PrimaryKey, AutoIncrement]
        [Column("Id")]
        public int Id { get; set; }

        [Column("PrayerCardId"), Indexed]
        public int PrayerCardId { get; set; }

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
                throw new InvalidOperationException("DBService not set. Call PrayerCardTag.SetDBService at app startup.");
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
                throw new InvalidOperationException("DBService not set. Call PrayerCardTag.SetDBService at app startup.");

            await _dbService.DeleteAsync(this);
        }

        public static async Task<PrayerCardTag> LoadAsync(int _id)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerCardTag.SetDBService at app startup.");

            return await _dbService.GetByIdAsync<PrayerCardTag>(_id);
        }

        public static async Task<List<PrayerCardTag>> LoadAllAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerCardTag.SetDBService at app startup.");

            return await _dbService.GetAllAsync<PrayerCardTag>();
        }

        public static async Task<List<PrayerCardTag>> LoadByCardIdAsync(int prayerCardId)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerCardTag.SetDBService at app startup.");

            return await _dbService.GetByCardIdAsync(prayerCardId);
        }

        public static async Task<List<PrayerCardTag>> LoadByTagIdAsync(int prayerTagId)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerCardTag.SetDBService at app startup.");

            return await _dbService.GetByTagIdAsync(prayerTagId);
        }

        #endregion
    }
}
