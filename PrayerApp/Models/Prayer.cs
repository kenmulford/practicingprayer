using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace PrayerApp.Models
{
    [Table("Prayer")]
    public class Prayer
    {
        private string _title;

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

        [Column("NotificationFrequency"), MaxLength(50)]
        public string NotificationFrequency { get; set; } = "Weekly";

        [Column("IsAnswered")]
        public bool IsAnswered { get; set; } = false;
    }
}
