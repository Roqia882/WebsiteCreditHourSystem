using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
    [Table("Students")]
    public class Student
    {
        public Student()
        {
            // دي قيم يدوية الكود هيحطها أول ما تعملي "new Student"
            this.Semester = "1";
            this.GPA = 0.0m;
            this.CompletedCreditHours = 0;
            this.Status = "Active";
        }


        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int StudentID { get; set; }

        // أزلنا [Required] وأضفنا علامة الاستفهام لتسمح بـ null
        public string? NameEn { get; set; }
        public string? NameAr { get; set; }

        [Required] // اتركي الإيميل والباسورد مطلوبين لأنهما أساسيان للدخول
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public string? Year { get; set; }
        public string? Semester { get; set; }
        public string? Phone { get; set; }
        public string? UniversityEmail { get; set; }
        public string? FullName { get; set; }
        public string? MajorEn { get; set; }
        public string? MajorAr { get; set; }
        public string Status { get; set; } = "Active"; // القيمة الافتراضية "نشط"

    

        [Column(TypeName = "decimal(4, 2)")]
        public decimal? GPA { get; set; } = 0.0m; // 🌟 حولناه لـ decimal وضفنا حرف الـ m للقيمة الافتراضية

        [Column(TypeName = "decimal(7, 2)")]
        public decimal? TotalGradePoints { get; set; } // إجمالي نقاط الدرجات

        [Column(TypeName = "decimal(5, 2)")]
        public decimal? TotalPercentage { get; set; } // النسبة المئوية الكلية
        public string? OverallLetter { get; set; }

        // 🌟 ودول الثلاثة الجداد اللي ضفناهم عشان نغطي اللائحة كاملة
        [Column(TypeName = "decimal(4, 2)")]
        public decimal? CGPA { get; set; } = 0.0m;

        [Column(TypeName = "decimal(4, 2)")]
        public decimal? GPAU { get; set; } = 0.0m;

        [Column(TypeName = "decimal(4, 2)")]
        public decimal? CGPAU { get; set; } = 0.0m;


       
        public int? CompletedCreditHours { get; set; } = 0;
        public string? ProfileImagePath { get; set; }
        // حقل وهمي (Not Mapped) بيتبعت للفرونت إند بس
        public string FullProfileImageUrl => string.IsNullOrEmpty(ProfileImagePath)
            ? null
            : "https://your-domain.com" + ProfileImagePath;

        public ICollection<Grade> Grades { get; set; }
        public int MaxAllowedHours { get; set; } = 14; // القيمة الافتراضية هي 18 ساعة
        public virtual ICollection<CourseRegistration> CourseRegistrations { get; set; } = new List<CourseRegistration>();
    }
}