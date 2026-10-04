using System.ComponentModel.DataAnnotations;

namespace WebsiteCreditHoursSystem.Models
{
    public class SystemControl
    {
        [Key]
        public int ID { get; set; }

        public bool IsRegistrationOpen { get; set; } = false;

        [Required]
        [StringLength(50)]
        public string CurrentSemester { get; set; } // "1" أو "2" أو "Summer"

        // 🌟 الحقل الجديد اللي ضفناه عشان نتحكم في السنة الأكاديمية للجامعة كلها
        [Required]
        [StringLength(50)]
        public string CurrentAcademicYear { get; set; } // "2025/2026"
    }
}