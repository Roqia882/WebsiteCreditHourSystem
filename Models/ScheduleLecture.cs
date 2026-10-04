using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
      [Table("Schedule")]
        public class ScheduleLecture
        {
            [Key]
            public int ScheduleID { get; set; }
            public int? StudentID { get; set; }
            public string CourseID { get; set; }
            public string? Semester { get; set; }
            public int? Year { get; set; }
            public string? Day { get; set; }
            public TimeSpan? StartTime { get; set; }
            public TimeSpan? EndTime { get; set; }
            public string? Room { get; set; }
            public string? SessionType { get; set; }

            public int? InstructorID { get; set; }


            [ForeignKey("CourseID")]
            public Course Course { get; set; }
            [ForeignKey("InstructorID")]
            public Instructor Instructor { get; set; }
        }
    }







