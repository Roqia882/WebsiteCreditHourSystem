using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // استبدليها باسم الـ Namespace بتاع الـ Data عندك
using WebsiteCreditHoursSystem.Models; // استبدليها باسم الـ Namespace بتاع الموديلز

namespace WebsiteCreditHoursSystem.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet("dashboard-analytics")]
        public async Task<IActionResult> GetDashboardAnalytics(int academicYear)
        {
            // 1. أرقام الكروت العلوية (Cards)
            var totalStudents = await _context.Students.CountAsync();
            var totalCourses = await _context.Courses.CountAsync();

            // حساب متوسط الدرجات لجميع المواد المنشورة في السنة المحددة
            var avgGrade = await _context.Grades
                .Where(g => g.AcademicYear == academicYear && g.NumericGrade != null && g.IsPublished)
                .AverageAsync(g => (decimal?)g.NumericGrade) ?? 0;

            // حساب نسبة الإنجاز (المواد اللي اتنشرت بالنسبة للمواد اللي اترصدت)
            var totalGradesCount = await _context.Grades.CountAsync(g => g.AcademicYear == academicYear);
            var publishedCount = await _context.Grades.CountAsync(g => g.AcademicYear == academicYear && g.IsPublished);
            decimal completionRate = totalGradesCount > 0 ? (decimal)publishedCount / totalGradesCount * 100 : 0;

            // 2. توزيع الطلاب حسب المستوى (للرسم البياني الأفقي)
            var studentDistribution = await _context.Students
                .GroupBy(s => s.Year)
                .Select(g => new {
                    DepartmentOrLevel = g.Key,
                    Count = g.Count()
                }).ToListAsync();

            // 3. توزيع التقديرات (للرسم البياني الرأسي A, B, C...)
            var gradeDistribution = await _context.Grades
                .Where(g => g.AcademicYear == academicYear && g.IsPublished)
                .GroupBy(g => g.LetterGrade)
                .Select(g => new {
                    Label = g.Key ?? "N/A",
                    Value = g.Count()
                }).OrderBy(g => g.Label).ToListAsync();

            // 4. أكثر 10 مواد فيها طلاب مسجلين
            var topCourses = await _context.CourseOfferings
                .Include(o => o.Course)
                .Select(o => new {
                    CourseCode = o.Course.CourseID,
                    CourseName = o.Course.CourseNameAr,
                    Enrolled = _context.Grades.Count(g => g.OfferingID == o.OfferingID && g.AcademicYear == academicYear),
                    Percentage = 0 // دي الـ Frontend يحسبها بناءً على السعة القصوى لو حابة
                })
                .OrderByDescending(c => c.Enrolled)
                .Take(10).ToListAsync();

            // 5. عبء عمل الدكاترة (Professor Workload)
            var profWorkload = await _context.Instructors
                .Select(p => new {
                    Name = p.NameAr,
                    CoursesCount = _context.CourseOfferings.Count(o => o.InstructorID == p.InstructorID),
                    StudentsCount = _context.Grades.Count(g => g.CourseOffering.InstructorID == p.InstructorID && g.AcademicYear == academicYear)
                })
                .OrderByDescending(p => p.StudentsCount)
                .Take(10).ToListAsync();

            return Ok(new
            {
                Cards = new { totalStudents, totalCourses, avgGrade = Math.Round(avgGrade, 2), completionRate = Math.Round(completionRate, 1) },
                StudentDistribution = studentDistribution,
                GradeDistribution = gradeDistribution,
                TopCourses = topCourses,
                ProfessorWorkload = profWorkload
            });
        }
        [HttpGet("recent-students")]
        public async Task<IActionResult> GetRecentStudents()
        {
            // 1. جلب آخر 5 طلاب تم إضافتهم للداتابيز
            var recentStudents = await _context.Students
                .OrderByDescending(s => s.StudentID) // الترتيب من الأحدث للأقدم بناءً على الـ ID
                .Take(5) // أخذ أول 5 نتائج فقط
                .Select(s => new
                {
                    s.StudentID,
                    FullName = s.NameEn, // عرض الاسم بصيغة FullName كما طلبت
                    s.Email,
                    s.Year,
                    s.Semester,
                    RegistrationDate = "" // لو عندك عمود تاريخ تقدر تضيفه هنا، لو مفيش ممكن تشيل السطر ده
                })
                .ToListAsync();

            // 2. التحقق من وجود بيانات
            if (!recentStudents.Any())
            {
                return NotFound("لا يوجد طلاب مسجلين حالياً.");
            }

            return Ok(recentStudents);
        }
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = new
            {
                totalStudents = await _context.Students.CountAsync(),
                totalInstructors = await _context.Instructors.CountAsync(),
                totalCourses = await _context.Courses.CountAsync(),
                totalOfferings = await _context.CourseOfferings.CountAsync()
            };
            return Ok(stats);
        }

    }
}
