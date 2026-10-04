using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using System.Net;
using WebsiteCreditHoursSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace WebsiteCreditHoursSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentRegisterController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration; // لقراءة ملف appsettings.json

        // المشيد (Constructor) لاستقبال الـ Context والـ Configuration
        public StudentRegisterController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public IActionResult RegisterStudent([FromBody] StudentRegisterRequest request)
        {
            // 1. التحقق من صحة البيانات المرسلة
            if (request == null || string.IsNullOrEmpty(request.FullName) || string.IsNullOrEmpty(request.Email))
            {
                return BadRequest("الاسم الكامل والبريد الإلكتروني الشخصي مطلوبان.");
            }

            try
            {
                // 2. توليد رقم تعريف الطالب (ID)
                int studentId = 1;
                if (_context.Students.Any())
                {
                    studentId = _context.Students.Max(s => s.StudentID) + 1;
                }

                // 3. توليد البيانات الجامعية التلقائية
                string universityEmail = $"{studentId}@university.edu";
                string password = GenerateRandomPassword(8);

                // 4. إنشاء كائن الطالب وحفظه
                var student = new Student
                {
                    FullName = request.FullName,
                    NameEn = request.FullName, 
                    NameAr = request.FullName, 
                    Year = request.Year,
                    StudentID = studentId,
                    UniversityEmail = universityEmail,
                    Email = request.Email,
                    Password = password
                };

                _context.Students.Add(student);
                _context.SaveChanges();

                // 5. إرسال الإيميل (في بلوك try مستقل حتى لا يفشل التسجيل إذا فشل الإرسال)
                try
                {
                    string emailBody = $@"
                        <div dir='rtl' style='font-family: Arial, sans-serif;'>
                            <h3>مرحباً {request.FullName}</h3>
                            <p>تم تسجيلك بنجاح في نظام الساعات المعتمدة.</p>
                            <p><b>بيانات الدخول الخاصة بك:</b></p>
                            <ul>
                                <li><b>رقم الطالب:</b> {studentId}</li>
                                <li><b>الإيميل الجامعي:</b> {universityEmail}</li>
                                <li><b>كلمة المرور:</b> {password}</li>
                            </ul>
                            <p>يمكنك تحميل التطبيق والدخول من هنا: <a href='https://your-app-link.com'>رابط التطبيق</a></p>
                        </div>";

                    SendEmail(request.Email, "بيانات تسجيل الطالب الجديدة", emailBody);
                }
                catch (Exception ex)
                {
                    // إذا فشل الإيميل، نرسل رد نجاح مع تنبيه بخصوص الإيميل
                    return Ok(new
                    {
                        Message = "تم التسجيل بنجاح، ولكن فشل إرسال الإيميل. السبب: " + ex.Message,
                        StudentID = studentId,
                        UniversityEmail = universityEmail
                    });
                }

                return Ok(new
                {
                    Message = "تم تسجيل الطالب بنجاح وإرسال البيانات لبريده الشخصي.",
                    StudentID = studentId,
                    UniversityEmail = universityEmail
                });
            }
            catch (Exception ex)
            {
                // جلب الرسالة التفصيلية للخطأ في قاعدة البيانات
                var realError = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, $"خطأ في قاعدة البيانات: {realError}");
            }
        }

        private string GenerateRandomPassword(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#$!";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private void SendEmail(string toEmail, string subject, string body)
        {
            // قراءة الإعدادات من ملف appsettings.json
            var fromEmail = _configuration["EmailSettings:Email"];
            var appPassword = _configuration["EmailSettings:Password"];
            var host = _configuration["EmailSettings:Host"];
            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var displayName = _configuration["EmailSettings:DisplayName"];

            var smtp = new SmtpClient(host)
            {
                Port = port,
                Credentials = new NetworkCredential(fromEmail, appPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, displayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(toEmail);

            smtp.Send(mailMessage);
        }
    }

    public class StudentRegisterRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Year { get; set; }
    }
}