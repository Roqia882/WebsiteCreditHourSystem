using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
    
        [Table("Instructors")]
        public class Instructor
        {
            [Key]
            public int InstructorID { get; set; }
            public string NameEn { get; set; }
            public string? NameAr { get; set; }
            public string Email { get; set; }
            public string? FullName { get; set; }
        public int? UserId { get; set; } // ده الحقل اللي هيتخزن فيه الـ ID

        [ForeignKey("UserId")]
        public virtual SystemUser? User { get; set; } // دي الـ Navigation Property اللي بتربطه بالجدول التاني
    }
    }


