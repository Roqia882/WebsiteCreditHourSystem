using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
      [Table("Enrollment")]
        public class Enrollment
        {
            [Key]
            public int EnrollmentID { get; set; }
            public int StudentID { get; set; }
            public string CourseID { get; set; }
            public string? Grade { get; set; }
        }
    }



