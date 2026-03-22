using System;
using SQLite;
using PrayerApp.Services;

namespace PrayerApp.Models
{
    [Table("PrayerTag")]
    public class PrayerTag
    {
        private static IDBService? _dbService;
        private string _name = string.Empty;

        [PrimaryKey, AutoIncrement]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Name"), MaxLength(100), Unique]
        public string Name
        {
            get => _name;
            set => _name = value ?? "Unnamed Tag";
        }

        [Column("Color"), MaxLength(9)]
        public string? Color { get; set; } // Hex color code (e.g., "#FF5733")

        /// <summary>True for system-managed tags (e.g., "Recently Notified") — protected from user rename/delete.</summary>
        [Column("IsSystem")]
        public bool IsSystem { get; set; }

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
                throw new InvalidOperationException("DBService not set. Call PrayerTag.SetDBService at app startup.");
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
                throw new InvalidOperationException("DBService not set. Call PrayerTag.SetDBService at app startup.");

            await _dbService.DeleteAsync(this);
        }

        public static async Task<PrayerTag> LoadAsync(int _id)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerTag.SetDBService at app startup.");

            return await _dbService.GetByIdAsync<PrayerTag>(_id);
        }

        public static async Task<List<PrayerTag>> LoadAllAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerTag.SetDBService at app startup.");

            return await _dbService.GetAllAsync<PrayerTag>();
        }

        #endregion
    }
}
