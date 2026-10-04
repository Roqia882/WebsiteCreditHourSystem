using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebsiteCreditHoursSystem.DTOs;
using WebsiteCreditHoursSystem.Models;
using WebsiteCreditHoursSystem.Services;



namespace WebsiteCreditHoursSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PasswordResetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;

        public PasswordResetController(AppDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("request-code")]
        public async Task<IActionResult> RequestCode([FromBody] RequestResetCodeDto request)
        {
            var user = await _context.SystemUsers
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || string.IsNullOrEmpty(user.Email))
                return NotFound("User not found or email is empty");
            var code = new Random().Next(100000, 999999).ToString();

            var resetCode = new PasswordResetCode
            {
                UserId = user.UserID,
                Code = code,
                ExpireAt = DateTime.Now.AddMinutes(10),
                IsUsed = false
            };

            _context.PasswordResetCodes.Add(resetCode);
            await _context.SaveChangesAsync();

            string subject = "Your Password Reset Code";
            string message = $"Hello {user.FullName},\n\nYour password reset code is: {code}\nIt will expire in 10 minutes.";

            await _emailService.SendEmailAsync(user.Email, subject, message);

            return Ok(new { message = "Reset code sent to your university email" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            var user = await _context.SystemUsers
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
                return NotFound(new { message = "Email not found" });

            var resetCode = await _context.PasswordResetCodes
                .Where(c => c.UserId == user.UserID && !c.IsUsed && c.ExpireAt > DateTime.Now)
                .OrderByDescending(c => c.ExpireAt)
                .FirstOrDefaultAsync();

            if (resetCode == null || resetCode.Code != request.Code)
                return BadRequest(new { message = "Invalid or expired code" });

            user.Password = request.NewPassword;
            resetCode.IsUsed = true;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password updated successfully" });
        }
    }
}
