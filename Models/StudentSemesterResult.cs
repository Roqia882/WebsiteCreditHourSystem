using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
    [Table("StudentSemesterResults")]
    public class StudentSemesterResult
    {
        [Key]
        public int ResultID { get; set; }

        [Required]
        public int StudentID { get; set; }

        [ForeignKey("StudentID")]
        public Student Student { get; set; }

        [Required]
        [StringLength(50)]
        public string AcademicYear { get; set; }

        [Required]
        [StringLength(50)]
        public string Semester { get; set; }

        public int SemesterHours { get; set; }
        public int CumulativeHours { get; set; }

        [Column(TypeName = "decimal(4, 2)")]
        public decimal SemesterGPA { get; set; }

        [Column(TypeName = "decimal(4, 2)")]
        public decimal CumulativeGPA { get; set; }

        [Column(TypeName = "decimal(4, 2)")]
        public decimal SemesterGPAU { get; set; }

        [Column(TypeName = "decimal(4, 2)")]
        public decimal CumulativeGPAU { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}