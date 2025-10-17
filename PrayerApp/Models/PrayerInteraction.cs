using System;
using System.Collections.Generic;
using SQLite;
using PrayerApp.Services;

namespace PrayerApp.Models
{
    [Table("PrayerInteraction")]
    public class PrayerInteraction
    {
        private static IDBService? _dbService;

        [PrimaryKey, AutoIncrement]
        [Column("Id")]
        public int Id { get; set; }

        [Column("PrayerId"), Indexed]
        public int PrayerId { get; set; }

        [Column("InteractionType"), MaxLength(50)]
        public string InteractionType { get; set; } = "Prayed";

        [Column("InteractionAt")]
        public DateTime InteractionAt { get; set; } = DateTime.Now;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

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
                throw new InvalidOperationException("DBService not set. Call PrayerInteraction.SetDBService at app startup.");
            if (Id == 0)
            {
                await _dbService.InsertAsync(this);
            }
            else
            {
                UpdatedAt = DateTime.Now;
                await _dbService.UpdateAsync(this);
            }
        }

        public async Task DeleteAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerInteraction.SetDBService at app startup.");

            await _dbService.DeleteAsync(this);
        }

        public static async Task<PrayerInteraction> LoadAsync(int _id)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerInteraction.SetDBService at app startup.");

            return await _dbService.GetByIdAsync<PrayerInteraction>(_id);
        }

        public static async Task<List<PrayerInteraction>> LoadAllAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerInteraction.SetDBService at app startup.");

            return await _dbService.GetAllAsync<PrayerInteraction>();
        }

        #endregion
    }
}
