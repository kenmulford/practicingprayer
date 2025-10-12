using System;
using System.Collections.Generic;
using SQLite;

namespace PrayerApp.Models
{
    [Table("PrayerInteraction")]
    public class PrayerInteraction
    {
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
    }
}
