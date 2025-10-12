using System;
using System.Collections.Generic;
using SQLite;
using System.Linq;
using PrayerApp.Services;

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

        [Column("SortOrder")]
        public int? SortOrder {
            get => _sortOrder;

            set => _sortOrder = value ?? 0;
        }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        #region Constructor
        public static void SetDBService(IDBService dbService)
        {
            _dbService = dbService;
        }
        #endregion
        #region Actions

        public async void Save()
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

        #endregion
    }
}
