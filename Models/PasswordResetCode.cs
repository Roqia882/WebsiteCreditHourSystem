namespace WebsiteCreditHoursSystem.Models
{
    public class PasswordResetCode
    {
        
            public int Id { get; set; }
            public int UserId { get; set; }
            public string Code { get; set; }
            public DateTime ExpireAt { get; set; }
            public bool IsUsed { get; set; }

            // Relation
            public SystemUser SystemUser { get; set; }
        }
    }



