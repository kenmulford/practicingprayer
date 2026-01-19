using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using PrayerApp.Services;

namespace PrayerApp.Models
{
    [Table("Prayer")]
    public class Prayer
    {
        private static IDBService? _dbService;
        private string _title = "Prayer Request";

        [PrimaryKey, AutoIncrement]
        [Column("Id")]
        public int Id { get; set; }

        [Column("PrayerCategoryId"), Indexed]
        public int PrayerCategoryId { get; set; }

        [Column("Title"), MaxLength(100)]
        public string Title {
            get => _title;
            set => _title = value ?? "Prayer Request";
        }

        [Column("Details"), MaxLength(1000)]
        public string? Details { get; set;}

        [Column("CanNotify")]
        public bool CanNotify { get; set; } = false;

        [Column("PrayerFrequency")]
        public PrayerFrequency PrayerFrequency { get; set; } = PrayerFrequency.Weekly;

        [Column("IsAnswered")]
        public bool IsAnswered { get; set; } = false;
        
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
                throw new InvalidOperationException("DBService not set. Call Prayer.SetDBService at app startup.");
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
                throw new InvalidOperationException("DBService not set. Call Prayer.SetDBService at app startup.");

            await _dbService.DeleteAsync(this);
        }

        public static async Task<Prayer> LoadAsync(int _id)
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call Prayer.SetDBService at app startup.");

            return await _dbService.GetByIdAsync<Prayer>(_id);
        }

        public static async Task<List<Prayer>> LoadAllAsync()
        {
            if (_dbService == null)
                throw new InvalidOperationException("DBService not set. Call Prayer.SetDBService at app startup.");

            return await _dbService.GetAllAsync<Prayer>();
        }

        #endregion
    }
}
