using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Threading.Tasks;

namespace WebsiteCreditHoursSystem.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            if (string.IsNullOrEmpty(toEmail))
                throw new ArgumentNullException(nameof(toEmail), "Recipient email cannot be null");

            // قراءة القسم الخاص بالإعدادات
            var emailSettings = _config.GetSection("EmailSettings");

            var email = new MimeMessage();

            // تعديل الأسماء هنا لتطابق الـ JSON بالضبط
            email.From.Add(new MailboxAddress(emailSettings["DisplayName"], emailSettings["Email"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = message }; // خليتها Html عشان التنسيق يكون أحلى

            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            // استخدام "Host" و "Port" كما في الـ JSON
            await smtp.ConnectAsync(emailSettings["Host"], int.Parse(emailSettings["Port"]),
                MailKit.Security.SecureSocketOptions.StartTls);

            // استخدام "Email" و "Password" كما في الـ JSON
            await smtp.AuthenticateAsync(emailSettings["Email"], emailSettings["Password"]);

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}