using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
        [Table("CourseRegistration")]
        public class CourseRegistration
        {
            [Key]
            public int RegistrationID { get; set; }

            public int StudentID { get; set; }
            public int OfferingID { get; set; }
            public DateTime RegistrationDate { get; set; }
            public string Status { get; set; }

            [ForeignKey(nameof(StudentID))]
            public Student Student { get; set; }

            [ForeignKey(nameof(OfferingID))]
            public CourseOffering CourseOffering { get; set; }
        }
    }



