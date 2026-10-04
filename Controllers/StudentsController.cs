using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebsiteCreditHoursSystem.Models;

namespace WebsiteCreditHoursSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StudentsController(AppDbContext context)
        {
            _context = context;
        }

        // ===============================
        // GET: Filter + Search Students
        // ===============================
        [HttpGet]
        public async Task<IActionResult> GetStudents(
            string? year,
            string? semester,
            string? search)
        {
            var query = _context.Students.AsQueryable();

            // Filter by Year
            if (!string.IsNullOrEmpty(year))
            {
                query = query.Where(s => s.Year == year);
            }

            // Filter by Semester
            if (!string.IsNullOrEmpty(semester))
            {
                query = query.Where(s => s.Semester == semester);
            }

            // Search by Name
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.FullName.Contains(search));
            }

            var students = await query
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName,
                    s.Email,
                    s.Year,
                    s.Semester
                })
                .ToListAsync();

            return Ok(students);
        }
    }
}