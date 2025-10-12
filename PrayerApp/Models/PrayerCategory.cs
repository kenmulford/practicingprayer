using System;
using System.Collections.Generic;
using SQLite;

namespace PrayerApp.Models
{
    [Table("PrayerCategory")]
    public class PrayerCategory
    {
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
    }
}
