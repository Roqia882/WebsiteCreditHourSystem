using System.ComponentModel.DataAnnotations;

namespace WebsiteCreditHoursSystem.Models
{
    public class SystemUser
    {
        [Key]
        public int UserID { get; set; }

            public string FullName { get; set; }

            public string Email { get; set; }

            public string Password { get; set; }

            public string Role { get; set; }   // Admin / Affairs / Instructor

            public bool IsActive { get; set; }

            public DateTime CreatedAt { get; set; }
        
        public ICollection<PasswordResetCode> ResetCodes { get; set; } = new List<PasswordResetCode>();
    }
}
    



