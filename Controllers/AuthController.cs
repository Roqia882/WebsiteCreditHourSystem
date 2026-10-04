using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebsiteCreditHoursSystem.Models;
using WebsiteCreditHoursSystem.Services;

namespace WebsiteCreditHoursSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwtService;

        public AuthController(AppDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.SystemUsers
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.Password == request.Password);

            if (user == null)
                return Unauthorized(new { success = false, message = "بيانات الدخول غير صحيحة" });

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                success = true,
                token = token,
                Role = user.Role,
                Name = user.FullName,
                UserId = user.UserID
            });
        }
        [Authorize]
        [HttpGet("profile-info")]
        public async Task<IActionResult> GetProfileInfo()
        {
            // 1. سحب الـ ID باستخدام المسمى الدقيق اللي ظهر في الـ Debugger
            var userIdString = User.FindFirstValue("UserId")
                               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized("السيرفر لسه مش قادر يوصل للـ UserId جوه التوكن.");
            }

            // 2. التحويل لرقم (int) لأن جدولك بيستخدم int
            if (!int.TryParse(userIdString, out int userId))
            {
                return BadRequest("المعرف الموجود في التوكن ليس رقماً صالحاً.");
            }
            // 3. البحث في جدول النظام وسحب الـ Role من الداتابيز
            var userInDb = await _context.SystemUsers
                .Where(u => u.UserID == userId)
                .Select(u => new {
                    u.FullName,
                    u.Email,
                    u.Role // سحبنا الـ Role من العمود اللي في الجدول
                })
                .FirstOrDefaultAsync();

            if (userInDb == null)
            {
                return NotFound("المستخدم غير موجود في قاعدة البيانات.");
            }

            return Ok(new
            {
                fullName = userInDb.FullName,
                email = userInDb.Email,
                role = userInDb.Role // هنا القيمة جاية من الجدول علطول
            });
            
        }

        public class LoginRequest { public string Email { get; set; } public string Password { get; set; } }
    }
}
    