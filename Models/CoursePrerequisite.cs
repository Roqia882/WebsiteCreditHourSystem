using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
    public class CoursePrerequisite
    {
            [Key]
            public int PrereqID { get; set; }
            public string CourseID { get; set; } = string.Empty;
            public string PrerequisiteCourseID { get; set; } = string.Empty;
        // 🌟 السطرين دول اللي هيفكوا الإيرور ويجيبوا بيانات المادة
        [ForeignKey("CourseID")]
        public virtual Course? Course { get; set; }
        [ForeignKey("PrerequisiteCourseID")]
        public virtual Course? PrerequisiteCourse { get; set; }
    }
    }


