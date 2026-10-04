namespace WebsiteCreditHoursSystem.Models
{
    public class UpdateOfferingDto
    {
         public string CourseID { get; set; }
            public int InstructorID { get; set; }
            public int? Semester { get; set; }
            public int? Year { get; set; }
        public int? Capacity { get; set; }
        public string? Day { get; set; }
        public string? SessionType { get; set; }
        public TimeSpan? StartTime { get; set; }
            public TimeSpan? EndTime { get; set; }
            public string? Room { get; set; } // بما إنك قلتي القاعات مش أزمة، ممكن تسيبيه أو تمسحيه
        }
    }

