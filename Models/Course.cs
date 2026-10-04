using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
   
        [Table("Courses")]
        public class Course
        {
            [Key]
            public string CourseID { get; set; }

            public string? CourseNameEn { get; set; }
            public string? CourseNameAr { get; set; }
            public int? CreditHours { get; set; }
            public int? Semester { get; set; }
            public int? Level { get; set; }
        public int? AcademicYear { get; set; }
        public string? CourseType { get; set; }
            public string? PrerequisiteId { get; set; }
            public decimal? MaxGrade { get; set; } = 100.00m;
        [Required]
        [StringLength(20)]
        public string CourseCategory { get; set; } = "ENG";
        // القيم المتاحة: "UR", "ENG", "ISE", "VIS", "NLP", "CNS", "ROB"

        // ضيفي السطرين دول تحتها عشان الـ Entity Framework يربطهم ببعض
        [ForeignKey("PrerequisiteId")]
            public virtual Course? Prerequisite { get; set; }


        // عرض فقط
        //public int CourseCode { get; set; }

            public ICollection<CourseOffering> CourseOfferings { get; set; }
                = new List<CourseOffering>();
        }
    }




