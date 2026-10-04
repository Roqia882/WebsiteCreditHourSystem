using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebsiteCreditHoursSystem.Models;

namespace WebsiteCreditHoursSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PasswordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PasswordController(AppDbContext context) { _context = context; }

       [Authorize]
        [HttpPost("change")]
         public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
         {
             var userIdStr = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

             if (string.IsNullOrEmpty(userIdStr))
                 return Unauthorized("لم يتم العثور على هوية المستخدم");

             int userId = int.Parse(userIdStr);
             var user = await _context.SystemUsers.FindAsync(userId);

             if (user == null || user.Password != request.CurrentPassword)
                 return BadRequest("كلمة المرور الحالية غير صحيحة");

             user.Password = request.NewPassword;
             await _context.SaveChangesAsync();

             return Ok(new { success = true, message = "تم تغيير كلمة المرور بنجاح" });
         }

        
        [Authorize]
        [HttpGet("test-auth")]
        public IActionResult TestAuthentication()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

            return Ok(new
            {
                message = "✅ التوكن سليم والسيرفر اتعرف عليك!",
                isAuthenticated = User.Identity?.IsAuthenticated,
                userName = User.Identity?.Name,
                extractedClaims = claims
            });
        }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
}