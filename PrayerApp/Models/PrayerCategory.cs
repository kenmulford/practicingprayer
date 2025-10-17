using System;
using System.Collections.Generic;
using SQLite;
using System.Linq;
using PrayerApp.Services;
using System.Runtime.CompilerServices;

namespace PrayerApp.Models
{
    [Table("PrayerCategory")]
    public class PrayerCategory()
    {
        private static IDBService? _dbService;
        private int _sortOrder;
        private string _name;

        [PrimaryKey, AutoIncrement]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Name"), MaxLength(100)]
        public string Name { get => _name; set => _name = value ?? "Unnamed Category"; }

        [Column("IsFavorite")]
        public bool IsFavorite { get; set; } = false;

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
                throw new InvalidOperationException("DBService not set. Call PrayerCategory.SetDBService at app startup.");
            if(Id == 0)
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
                throw new InvalidOperationException("DBService not set. Call PrayerCategory.SetDBService at app startup.");
            
            await _dbService.DeleteAsync(this);
        }

        public static async Task<PrayerCategory> LoadAsync(int _id)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerCategory.SetDBService at app startup.");
            
            return await _dbService.GetByIdAsync<PrayerCategory>(_id);
        }

        public static async Task<List<PrayerCategory>> LoadAllAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call PrayerCategory.SetDBService at app startup.");

            return await _dbService.GetAllAsync<PrayerCategory>();
        }

        #endregion
    }
}
