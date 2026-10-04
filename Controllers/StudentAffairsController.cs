using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Text;
using WebsiteCreditHoursSystem.Models;

namespace WebsiteCreditHoursSystem.Controllers
{
    [ApiController]
    [Route("api/student-affairs")]
    [Authorize] // يضمن إن اللي يدخل هو شئون الطلبة بس عن طريق التوكن
    public class StudentAffairsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StudentAffairsController(AppDbContext context)
        {
            _context = context;
        }

        // ميثود مساعدة لجلب الـ UserId من التوكن (JWT)
        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        [HttpGet("view-students-filtered")]
        public async Task<IActionResult> GetStudents(int? academicYear, string? level, int? semester)
        {
            var query = _context.Students.AsQueryable();

            // 1. الفلترة الاختيارية
            if (!string.IsNullOrEmpty(level))
            {
                query = query.Where(s => s.Year == level);
            }

            if (semester.HasValue)
            {
                query = query.Where(s => s.Semester == semester.ToString());
            }

            try
            {
                // 2. جلب البيانات مع التأمين الكامل ضد الـ Null في الداتابيز
                var results = await query.Select(s => new
                {
                    StudentID = s.StudentID,
                    // تأمين الحقول النصية باستخدام (?? "") لمنع خطأ Data is Null
                    FullName = s.FullName ?? s.NameEn ?? "",
                    Email = s.Email ?? "",
                    UniversityEmail = s.UniversityEmail ?? "لا يوجد بريد جامعي",
                    Year = s.Year ?? "",
                    Semester = s.Semester ?? "",
                    Status = s.Status ?? "Active",

                    // استخدام الشرط الصريح لتجنب مشاكل ترجمة الـ (??) في السيرفر
                    GPA = s.GPA.HasValue ? s.GPA.Value : 0.0m,
                    CompletedHours = s.CompletedCreditHours.HasValue ? s.CompletedCreditHours.Value : 0
                }).ToListAsync();

                if (!results.Any())
                {
                    return NotFound("لا يوجد طلاب مطابقين لهذه البيانات.");
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ في السيرفر: {ex.Message} -> {ex.InnerException?.Message}");
            }
        }

        [HttpGet("student-full-details/{studentId}")]
        public async Task<IActionResult> GetStudentFullDetails(int studentId)
        {
            // 1. جلب بيانات الطالب الأساسية
            var studentData = await _context.Students
                .Include(s => s.CourseRegistrations)
                    .ThenInclude(r => r.CourseOffering)
                        .ThenInclude(co => co.Course)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (studentData == null)
            {
                return NotFound(new { message = "الطالب غير موجود" });
            }

            // 2. تشكيل البيانات المطلوبة
            var result = new
            {
                student = new
                {
                    studentID = studentData.StudentID,
                    FullName = studentData.FullName,
                    email = studentData.Email,
                    UniversityEmail = studentData.UniversityEmail,
                    year = studentData.Year,
                    semester = studentData.Semester,
                    Status = studentData.Status,
                    MaxAllowedHours = studentData.MaxAllowedHours,
                    gpa = studentData.GPA ?? 0.0m,
                    completedHours = studentData.CompletedCreditHours ?? 0
                },
                courses = studentData.CourseRegistrations.Select(r => new
                {
                    courseID = r.CourseOffering.CourseID,
                    courseName = r.CourseOffering.Course.CourseNameEn,
                    creditsHours = r.CourseOffering.Course.CreditHours,
                    status = r.Status
                }).ToList()
            };

            return Ok(result);
        }

        [HttpGet("get-students-in-offering/{offeringId}")] // 👈 التعديل هنا: البحث بـ offeringId
        public async Task<IActionResult> GetStudentsInOffering(int offeringId)
        {
            try
            {
                // 1️⃣ التأكد من وجود العرض (Offering) ده في السيستم
                var offering = await _context.CourseOfferings
                    .Include(o => o.Course)
                    .Include(o => o.Instructor)
                    .FirstOrDefaultAsync(o => o.OfferingID == offeringId);

                if (offering == null)
                    return NotFound("عذراً، مجموعة المادة هذه غير موجودة.");

                // 2️⃣ جلب لستة الطلاب المسجلين في الـ Offering ده بالظبط
                var studentsList = await _context.CourseRegistrations
                    .Where(r => r.OfferingID == offeringId)
                    .Select(r => new
                    {
                        StudentID = r.Student.StudentID,
                        StudentName = r.Student.NameEn, // أو الاسم العربي حسب الداتا بيز عندكِ
                        Email = r.Student.Email,
                        RegistrationDate = r.RegistrationDate,
                        Status = r.Status
                    })
                    .ToListAsync();

                // 3️⃣ لو اللستة فاضية، بنرجع معلومات المادة مع رسالة واضحة للفرونت إند
                if (!studentsList.Any())
                    return Ok(new
                    {
                        message = $"لا يوجد طلاب مسجلين بعد في مادة ({offering.Course?.CourseNameAr}) مع دكتور ({offering.Instructor?.NameEn}).",
                        students = new List<object>()
                    });

                return Ok(new
                {
                    courseName = offering.Course?.CourseNameAr,
                    instructorName = offering.Instructor?.NameEn,
                    semester = offering.Semester,
                    year = offering.Year,
                    students = studentsList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء جلب كشف الطلاب: {ex.Message}");
            }
        }
        [HttpGet("view-courses")]
        public async Task<IActionResult> GetCourses(int? level, int? semester)
        {
            // 1. نبدأ بالاستعلام من جدول الكورسات مباشرة
            var query = _context.Courses.AsQueryable();

            // 2. الفلترة بناءً على الليفل والترم
            if (level.HasValue) query = query.Where(c => c.Level == level.Value);
            if (semester.HasValue) query = query.Where(c => c.Semester == semester.Value);

            // 3. جلب البيانات مع معالجة الـ Null
            var results = await query.Select(c => new
            {
                c.CourseID,
                c.CourseNameAr,
                CourseName = c.CourseNameEn,
                level = c.Level ?? 0,
                semester = c.Semester ?? 0,
                c.CourseCategory,
                c.CourseType,
                creditHours = c.CreditHours ?? 0
            }).ToListAsync();

            if (!results.Any())
            {
                return NotFound("لا توجد كورسات مسجلة لهذا المستوى أو الترم.");
            }

            return Ok(results);
        }

        [HttpGet("course-enrollments/{courseId}")]
        public async Task<IActionResult> GetCourseEnrollments(string courseId)
        {
            // 1. نبحث عن الكورس أولاً
            var course = await _context.Courses
                .Where(c => c.CourseID == courseId)
                .Select(c => new
                {
                    c.CourseID,
                    CourseName = c.CourseNameEn,
                    c.CourseNameAr,
                    c.CreditHours,
                    c.CourseType,
                    c.CourseCategory,
                    level = c.Level ?? 0,
                    semester = c.Semester ?? 0,
                    // 2. نجلب الطلاب المسجلين
                    students = _context.CourseRegistrations
                        .Where(r => r.CourseOffering.CourseID == courseId)
                        .Select(r => new
                        {
                            studentID = r.StudentID,
                            studentName = r.Student.FullName,
                            status = r.Status
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (course == null)
                return NotFound($"الكورس رقم {courseId} غير موجود في قاعدة البيانات.");

            return Ok(course);
        }
        [HttpGet("all-course-offerings")]
        public async Task<IActionResult> GetAllOfferings([FromQuery] int? level = null, [FromQuery] int? semester = null) // 👈 البارامترز بقت اختيارية وتقبل null
        {
            try
            {
                // 1️⃣ بنبدأ بالاستعلام الأساسي من غير ما ينفذ في الداتا بيز لسه (IQueryable)
                var query = _context.CourseOfferings.AsQueryable();

                // 2️⃣ الشرط السحري للفلترة الاختيارية: لو الأدمن بعت الـ level، بنفلتر بيه
                if (level.HasValue && level.Value > 0)
                {
                    query = query.Where(o => o.Year == level.Value);
                }

                // 3️⃣ لو الأدمن بعت الـ semester، بنفلتر بيه كمان
                if (semester.HasValue && semester.Value > 0)
                {
                    query = query.Where(o => o.Semester == semester.Value);
                }

                // 4️⃣ بعد ما طبقنا الفلاتر (لو موجودة)، بنعمل الـ Select والـ Include براحتنا
                var offerings = await query
                    .Include(o => o.Course)
                    .Include(o => o.Instructor)
                    .Select(o => new
                    {
                        ID = o.OfferingID,
                        CourseID = o.CourseID,
                        CourseName = o.Course != null ? o.Course.CourseNameAr : "مادة غير معروفة",
                        InstructorName = o.Instructor != null ? o.Instructor.NameEn : "لم يحدد دكتور",
                        InstructorID = o.InstructorID,
                        Day = o.Day,

                        // 🌟 التعديل السحري هنا: فصل الوقت وإضافة الثواني بالظبط زي الصورة الثانية 🌟
                        StartTime = o.StartTime.HasValue ? o.StartTime.Value.ToString(@"hh\:mm\:ss") : null,
                        EndTime = o.EndTime.HasValue ? o.EndTime.Value.ToString(@"hh\:mm\:ss") : null,


                        Room = o.Room,
                        Capacity = o.Capacity,
                        Level = o.Year,
                        Semester = o.Semester,

                        SessionType = _context.Schedule
                            .Where(s => s.CourseID == o.CourseID
                                     && s.InstructorID == o.InstructorID
                                     && s.Day == o.Day
                                     && s.StartTime == o.StartTime
                                     && s.StudentID == null)
                            .Select(s => s.SessionType)
                            .FirstOrDefault() ?? "Lecture"
                    })
                    .ToListAsync();

                return offerings.Any() ? Ok(offerings) : NotFound("لم يتم العثور على عروض مواد تطابق الفلاتر المحددة.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطأ: {ex.Message}");
            }
        }
        [HttpGet("course-offering/{offeringId}")]
        public async Task<IActionResult> GetOfferingById(int offeringId)
        {
            try
            {
                // البحث عن العرض المطابق للـ ID مع عمل الـ Include للجداول المربوطة
                var offering = await _context.CourseOfferings
                    .Include(o => o.Course)
                    .Include(o => o.Instructor)
                    .Where(o => o.OfferingID == offeringId)
                    .Select(o => new
                    {
                        ID = o.OfferingID,
                        CourseID = o.CourseID,
                        CourseName = o.Course != null ? o.Course.CourseNameAr : "مادة غير معروفة",
                        InstructorName = o.Instructor != null ? o.Instructor.NameEn : "لم يحدد دكتور",
                        InstructorID = o.InstructorID,
                        Day = o.Day,

                        // فصل الوقت وإضافة الثواني بالظبط
                        StartTime = o.StartTime.HasValue ? o.StartTime.Value.ToString(@"hh\:mm\:ss") : null,
                        EndTime = o.EndTime.HasValue ? o.EndTime.Value.ToString(@"hh\:mm\:ss") : null,

                        Room = o.Room,
                        Capacity = o.Capacity,
                        Level = o.Year,
                        Semester = o.Semester,

                        // نفس اللوجيك الذكي بتاعك لجلب الـ SessionType
                        SessionType = _context.Schedule
                            .Where(s => s.CourseID == o.CourseID
                                     && s.InstructorID == o.InstructorID
                                     && s.Day == o.Day
                                     && s.StartTime == o.StartTime
                                     && s.StudentID == null)
                            .Select(s => s.SessionType)
                            .FirstOrDefault() ?? "Lecture"
                    })
                    .FirstOrDefaultAsync(); // جلب عنصر واحد فقط أو null لو مش موجود

                if (offering == null)
                {
                    return NotFound($"عفواً، لا يوجد عرض مادة مسجل في النظام بالرقم المعرف ({offeringId}).");
                }

                return Ok(offering);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء جلب تفاصيل عرض المادة: {ex.Message}");
            }
        }

        [HttpGet("course-offerings-by-course/{courseId}")]
        public async Task<IActionResult> GetOfferingsByCourseId(string courseId) // 👈 تم اعتماد string أو int حسب نوع الـ CourseID عندك
        {
            try
            {
                // 1️⃣ جلب جميع العروض المفتوحة لهذه المادة فقط من جدول الـ CourseOfferings
                var offerings = await _context.CourseOfferings
                    .Include(o => o.Course)
                    .Include(o => o.Instructor)
                    .Where(o => o.CourseID == courseId) // الفلترة الأساسية بكود المادة
                    .Select(o => new
                    {
                        ID = o.OfferingID,
                        CourseID = o.CourseID,
                        CourseName = o.Course != null ? o.Course.CourseNameAr : "مادة غير معروفة",
                        InstructorName = o.Instructor != null ? o.Instructor.NameEn : "لم يحدد دكتور",
                        InstructorID = o.InstructorID,
                        Day = o.Day,

                        // 🌟 تنسيق الوقت الاحترافي بالثواني المعتمد في مشروعك
                        StartTime = o.StartTime.HasValue ? o.StartTime.Value.ToString(@"hh\:mm\:ss") : null,
                        EndTime = o.EndTime.HasValue ? o.EndTime.Value.ToString(@"hh\:mm\:ss") : null,

                        Room = o.Room,
                        Capacity = o.Capacity,
                        Level = o.Year,
                        Semester = o.Semester,

                        // 🌟 جلب الـ SessionType من جدول الـ Schedule بنفس اللوجيك المعتمد
                        SessionType = _context.Schedule
                            .Where(s => s.CourseID == o.CourseID
                                     && s.InstructorID == o.InstructorID
                                     && s.Day == o.Day
                                     && s.StartTime == o.StartTime
                                     && s.StudentID == null)
                            .Select(s => s.SessionType)
                            .FirstOrDefault() ?? "Lecture"
                    })
                    .ToListAsync();

                // 2️⃣ التحقق لو المادة ملهاش أي عروض مفتوحة حالياً في الترم
                if (offerings == null || !offerings.Any())
                {
                    return NotFound($"لم يتم العثور على أي عروض (Offerings) مسجلة للمادة ذات الكود ({courseId}).");
                }

                return Ok(offerings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء جلب عروض المادة: {ex.Message}");
            }
        }

        [HttpGet("view-prerequisites/{courseId}")]
        public async Task<IActionResult> GetPrereqsByCourseId(string courseId, [FromQuery] string lang = "en")
        {
            if (string.IsNullOrEmpty(courseId))
            {
                return BadRequest(lang == "ar" ? "برجاء إدخال كود المادة" : "Course ID is required");
            }

            var cleanCourseId = courseId.Trim().ToLower();

            var prereqs = await _context.CoursePrerequisites
                .Include(p => p.Course)
                .Include(p => p.PrerequisiteCourse) // 👈 عملنا Include للمادة المتطلبة
                .Where(p => p.CourseID.Trim().ToLower() == cleanCourseId)
                .Select(p => new {
                    p.PrereqID,
                    p.CourseID,
                    CourseName = lang == "ar"
                        ? (p.Course != null ? p.Course.CourseNameAr : p.CourseID)
                        : (p.Course != null ? p.Course.CourseNameEn : p.CourseID),

                    p.PrerequisiteCourseID,
                    // 🌟 جلب اسم المادة المتطلبة بناءً على اللغة
                    PrerequisiteCourseName = lang == "ar"
                        ? (p.PrerequisiteCourse != null ? p.PrerequisiteCourse.CourseNameAr : p.PrerequisiteCourseID)
                        : (p.PrerequisiteCourse != null ? p.PrerequisiteCourse.CourseNameEn : p.PrerequisiteCourseID)
                })
                .ToListAsync();

            if (prereqs == null || !prereqs.Any())
            {
                return NotFound(new { success = false, message = lang == "ar" ? "لا توجد متطلبات." : "No prerequisites found." });
            }

            return Ok(new { success = true, data = prereqs });
        }

        [HttpGet("all-levels-weekly-schedule")]
        public async Task<IActionResult> GetAllLevelsWeeklySchedule()
        {
            try
            {
                // 1. ترتيب أيام الأسبوع المعتمد عشان السيستم يمشى عليه
                var daysOrder = new List<string> { "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };

                // 2. جلب كل المحاضرات العامة من جدول الجدول الدراسي
                var allSchedules = await _context.Schedule
                    .Include(s => s.Course)
                    .Include(s => s.Instructor)
                    .Where(s => s.StudentID == null)
                    .ToListAsync();

                // 3. تنظيم البيانات حسب المستوى (Year) ثم الترم (Semester) ثم اليوم (Day)
                var formattedSchedule = allSchedules
                    .GroupBy(s => s.Year ?? 0) // أوالً: التقسيم حسب المستوى الدراسي (1, 2, 3, 4)
                    .Select(levelGroup => new
                    {
                        Level = levelGroup.Key == 0 ? "غير محدد" : $"Level {levelGroup.Key}",
                        Semesters = levelGroup
                            .GroupBy(s => s.Semester ?? "1") // ثانياً: الفلترة والتقسيم حسب الترم
                            .Select(semesterGroup => new
                            {
                                Semester = $"Semester {semesterGroup.Key}",
                                Days = semesterGroup
                                    .GroupBy(s => s.Day) // ثالثاً: تقسيم محاضرات الترم حسب الأيام
                                    .Select(dayGroup => new
                                    {
                                        Day = dayGroup.Key,
                                        Lectures = dayGroup.Select(l => new
                                        {
                                            l.ScheduleID,
                                            l.CourseID,
                                            CourseNameAr = l.Course?.CourseNameAr,
                                            CourseNameEn = l.Course?.CourseNameEn,
                                            InstructorName = l.Instructor?.FullName,
                                            StartTime = l.StartTime?.ToString(@"hh\:mm"),
                                            EndTime = l.EndTime?.ToString(@"hh\:mm"),
                                            l.Room,
                                            l.SessionType
                                        })
                                        .OrderBy(l => l.StartTime) // ترتيب المحاضرات جوة اليوم بالوقت
                                        .ToList()
                                    })
                                    // الترتيب السحري للأيام: بيرتب بناءً على مكان اليوم في مصفوفة daysOrder
                                    .OrderBy(d => daysOrder.IndexOf(char.ToUpper(d.Day[0]) + d.Day.Substring(1).ToLower()))
                                    .ToList()
                            })
                            .OrderBy(s => s.Semester) // ترتيب الترم الأول ثم الثاني
                            .ToList()
                    })
                    .OrderBy(g => g.Level) // ترتيب المستويات تصاعدياً
                    .ToList();

                return Ok(formattedSchedule);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"خطأ في السيرفر أثناء جلب الجداول: {message}");
            }
        }

        [HttpGet("course-grades/{courseId}")]
        public async Task<IActionResult> GetGradesByCourse(string courseId, [FromQuery] int? academicYear, [FromQuery] int? semester)
        {
            var offeringIds = await _context.CourseOfferings
                .Where(co => co.Course.CourseID == courseId)
                .Select(co => co.OfferingID)
                .ToListAsync();

            if (!offeringIds.Any()) return NotFound("لم يتم العثور على أي عروض تدريسية للمادة.");

            var query = _context.Grades
                .Include(g => g.Student)
                .Include(g => g.CourseOffering.Course)
                .Where(g => offeringIds.Contains(g.OfferingID) && g.IsPublished == true) // 🔥 الأمان للشئون
                .AsQueryable();

            if (academicYear.HasValue) query = query.Where(g => g.AcademicYear == academicYear.Value);
            if (semester.HasValue) query = query.Where(g => g.Semester == semester.Value);

            var results = await query.Select(g => new
            {
                g.GradeID,
                g.StudentID,
                StudentFullName = g.Student.FullName,
                CourseID = courseId,
                CourseName = g.CourseOffering.Course.CourseNameEn,
                g.AttendanceGrade,
                g.AssignmentsGrade,
                g.QuizzesGrade,
                g.MidtermExamGrade,
                g.PracticalGrade,
                g.OralGrade,
                g.FinalExamGrade,
                g.NumericGrade,
                g.Percentage,
                g.GradePoints,
                g.LetterGrade,
                g.AcademicYear,
                g.Semester
            }).ToListAsync();

            if (!results.Any()) return NotFound("لا توجد درجات منشورة متاحة للعرض.");
            return Ok(results);
        }
        [HttpGet("student-grades/{studentId}")]
        public async Task<IActionResult> GetStudentGradesByYear(int studentId, [FromQuery] int academicYear, [FromQuery] int? semester)
        {
            var query = _context.Grades
                .Include(g => g.CourseOffering.Course)
                .Where(g => g.StudentID == studentId && g.AcademicYear == academicYear && g.IsPublished == true) // 🔥 الأمان للشئون
                .AsQueryable();

            if (semester.HasValue) query = query.Where(g => g.Semester == semester.Value);

            var grades = await query.Select(g => new
            {
                g.GradeID,
                CourseID = g.CourseOffering.Course.CourseID,
                CourseName = g.CourseOffering.Course.CourseNameEn,
                g.NumericGrade,
                g.Percentage,
                g.LetterGrade,
                g.GradePoints,
                g.Semester,
                g.AcademicYear
            }).ToListAsync();

            if (!grades.Any()) return NotFound("لا توجد درجات منشورة لهذا الطالب في الفترة المحددة.");
            return Ok(grades);
        }
        [HttpGet("export-course-grades-csv")]
        public async Task<IActionResult> ExportCourseGradesCsv([FromQuery] string CourseID, [FromQuery] int academicYear, [FromQuery] int semester)
        {
            var offeringIds = await _context.CourseOfferings
                .Where(co => co.Course.CourseID == CourseID)
                .Select(co => co.OfferingID)
                .ToListAsync();

            if (!offeringIds.Any()) return BadRequest("الكورس غير مسجل بالنظام.");

            var grades = await _context.Grades
                .Include(g => g.Student)
                .Where(g => offeringIds.Contains(g.OfferingID)
                         && g.AcademicYear == academicYear
                         && g.Semester == semester
                         && g.IsPublished == true) // 🔥 حماية حجب الشيت غير المنشور
                .ToListAsync();

            if (!grades.Any()) return BadRequest("لا توجد درجات منشورة لهذا الكورس في الترم المحدد.");

            var csv = new StringBuilder();
            csv.AppendLine("GradeID,StudentID,StudentName,Attendance,Assignments,Quizzes,Midterm,Practical,Oral,Final");

            foreach (var g in grades)
            {
                var studentName = g.Student?.FullName ?? "غير معروف";
                csv.AppendLine($"{g.GradeID},{g.StudentID},{studentName},{g.AttendanceGrade ?? 0},{g.AssignmentsGrade ?? 0},{g.QuizzesGrade ?? 0},{g.MidtermExamGrade ?? 0},{g.PracticalGrade ?? 0},{g.OralGrade ?? 0},{g.FinalExamGrade ?? 0}");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"PublishedGrades_{CourseID}.csv");
        }
        [HttpGet("export-level-sheet-csv")]
        public async Task<IActionResult> ExportLevelSheetCsv(string level, int semester, int academicYear)
        {
            var allowedLevels = GetPossibleLevelNames(level);

            var students = await _context.Students
                .Where(s => allowedLevels.Contains(s.Year))
                // 🔥 الحماية والفلترة من المنبع داخل الـ Include: جلب الدرجات الـ Published فقط
                .Include(s => s.Grades.Where(g => g.Semester == semester && g.AcademicYear == academicYear && g.IsPublished == true))
                    .ThenInclude(g => g.CourseOffering.Course)
                .ToListAsync();

            if (!students.Any()) return BadRequest("لا يوجد طلاب أو نتائج منشورة في هذا المستوى.");

            var csv = new StringBuilder();
            var allCoursesInTerm = students.SelectMany(s => s.Grades)
                .Where(g => g.CourseOffering?.Course != null)
                .Select(g => g.CourseOffering.Course.CourseNameEn)
                .Distinct().ToList();

            var headerParts = new List<string> { "StudentID", "StudentName" };
            foreach (var courseName in allCoursesInTerm) headerParts.Add(courseName);
            headerParts.Add("LettersSection");
            foreach (var courseName in allCoursesInTerm) headerParts.Add($"{courseName}Letter");
            headerParts.AddRange(new[] { "TermPercentage", "GPA", "CGPA", "GPAU", "CGPAU", "OverallStatus" });
            csv.AppendLine(string.Join(",", headerParts));

            foreach (var s in students)
            {
                var studentName = s.FullName ?? "غير معروف";
                var row = new List<string> { s.StudentID.ToString(), studentName };

                foreach (var courseName in allCoursesInTerm)
                {
                    var grade = s.Grades.FirstOrDefault(g => g.CourseOffering?.Course?.CourseNameEn == courseName);
                    row.Add(grade?.NumericGrade?.ToString() ?? "0");
                }

                row.Add("START");

                foreach (var courseName in allCoursesInTerm)
                {
                    var grade = s.Grades.FirstOrDefault(g => g.CourseOffering?.Course?.CourseNameEn == courseName);
                    row.Add(grade?.LetterGrade ?? "N/A");
                }

                decimal totalMaxGrades = s.Grades.Sum(g => g.CourseOffering?.Course?.MaxGrade ?? 100m);
                decimal totalNumericGrades = s.Grades.Sum(g => g.NumericGrade ?? 0m);
                decimal termPercentage = totalMaxGrades > 0 ? Math.Round((totalNumericGrades / totalMaxGrades) * 100, 2) : 0m;

                row.Add(termPercentage.ToString());
                row.Add(s.GPA?.ToString() ?? "0");
                row.Add(s.CGPA?.ToString() ?? "0");
                row.Add(s.GPAU?.ToString() ?? "0");
                row.Add(s.CGPAU?.ToString() ?? "0");
                row.Add(s.OverallLetter ?? "N/A");

                csv.AppendLine(string.Join(",", row));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"ComprehensiveControlSheet_{level}_Published.csv");
        }
        [HttpGet("semester-results-by-level")]
        public async Task<IActionResult> GetSemesterResultsByLevel([FromQuery] int academicYear, [FromQuery] int semester, [FromQuery] string level)
        {
            try
            {
                // 1️⃣ جلب كافة المسميات الممكنة والمطابقة للمستوى المطلوب (لو اللائحة تدعم كذا مسمى)
                var allowedLevels = GetPossibleLevelNames(level);

                // 2️⃣ جلب كل درجات المواد المنشورة فقط والخاصة بالترم والمستوى المحدد
                var currentGrades = await _context.Grades
                    .Include(g => g.Student)
                    .Include(g => g.CourseOffering).ThenInclude(o => o.Course)
                    .Where(g => g.AcademicYear == academicYear
                             && g.Semester == semester
                             && g.Student != null
                             && g.IsPublished == true // 🔐 شرط الأمان الحاسم للشئون
                             && allowedLevels.Contains(g.Student.Year))
                    .ToListAsync();

                if (!currentGrades.Any())
                {
                    return NotFound($"لا توجد درجات منشورة أو نتائج متاحة للشئون لهذا الترم والمستوى ({level}) حالياً.");
                }

                // 3️⃣ تجميع الـ IDs للطلاب المتواجدين في النتيجة
                var studentIds = currentGrades.Select(g => g.StudentID).Distinct().ToList();

                // 4️⃣ جلب الأرشيف التاريخي (الأترام السابقة) لهؤلاء الطلاب لحساب الـ Cumulative بدقة
                var pastResults = await _context.StudentSemesterResults
                    .Where(r => studentIds.Contains(r.StudentID))
                    .ToListAsync();

                var resultsList = new List<object>();

                // 5️⃣ اللوب الأساسية لبناء ملف درجات ومعدلات كل طالب بالملي
                foreach (var studentId in studentIds)
                {
                    var studentGrades = currentGrades.Where(g => g.StudentID == studentId).ToList();
                    var studentInfo = studentGrades.First().Student;

                    // --- أ) حسابات ومجاميع الترم الحالي ---
                    decimal totalPointsNormal = 0; int totalHoursNormal = 0;
                    decimal totalPointsUniv = 0; int totalHoursUniv = 0;
                    decimal totalNumericGradesSum = 0; decimal totalMaxGradesSum = 0;

                    foreach (var grade in studentGrades)
                    {
                        int hours = grade.CourseOffering?.Course?.CreditHours ?? 0;
                        decimal points = grade.GradePoints ?? 0m;
                        string category = grade.CourseOffering?.Course?.CourseCategory ?? "ENG";
                        decimal numericGrade = grade.NumericGrade ?? 0m;
                        decimal maxGrade = grade.CourseOffering?.Course?.MaxGrade ?? 100m;

                        // الحسبة العامة للترم
                        totalPointsNormal += (points * hours);
                        totalHoursNormal += hours;

                        // إجمالي الدرجات لحساب النسبة المئوية الكلية للترم
                        totalNumericGradesSum += numericGrade;
                        totalMaxGradesSum += maxGrade;

                        // حسابات متطلبات الجامعة (استبعاد UR لو وُجدت)
                        if (category != "UR")
                        {
                            totalPointsUniv += (points * hours);
                            totalHoursUniv += hours;
                        }
                    }

                    // المعدلات والنسب للترم الحالي
                    decimal currentGPA = totalHoursNormal > 0 ? Math.Round(totalPointsNormal / totalHoursNormal, 2) : 0m;
                    decimal currentGPAU = totalHoursUniv > 0 ? Math.Round(totalPointsUniv / totalHoursUniv, 2) : 0m;
                    decimal currentSemesterPercentage = totalMaxGradesSum > 0 ? Math.Round((totalNumericGradesSum / totalMaxGradesSum) * 100, 2) : 0m;

                    // --- ب) دمج الأرشيف مع الحالي لحساب المعدل التراكمي الشامل (CGPA) ---
                    var studentPast = pastResults.Where(r => r.StudentID == studentId).ToList();

                    decimal pastPointsNormal = studentPast.Sum(r => r.SemesterGPA * r.SemesterHours);
                    int pastHoursNormal = studentPast.Sum(r => r.SemesterHours);

                    decimal pastPointsUniv = studentPast.Sum(r => r.SemesterGPAU * r.SemesterHours);
                    int pastHoursUniv = studentPast.Sum(r => r.SemesterHours);

                    // الإجماليات التراكمية الشاملة (الماضي + الحاضر)
                    int cumulativeHoursNormal = pastHoursNormal + totalHoursNormal;
                    decimal cumulativePointsNormal = pastPointsNormal + totalPointsNormal;

                    int cumulativeHoursUniv = pastHoursUniv + totalHoursUniv;
                    decimal cumulativePointsUniv = pastPointsUniv + totalPointsUniv;

                    decimal currentCGPA = cumulativeHoursNormal > 0 ? Math.Round(cumulativePointsNormal / cumulativeHoursNormal, 2) : 0m;
                    decimal currentCGPAU = cumulativeHoursUniv > 0 ? Math.Round(cumulativePointsUniv / cumulativeHoursUniv, 2) : 0m;

                    // تحديد التقدير العام التراكمي للطالب بناءً على لائحة الكلية
                    string overallLetter = GetOverallLetter(currentCGPA);

                    // 6️⃣ تركيب الـ Object النهائي الشامل لكل البيانات المطلوبة للشئون
                    resultsList.Add(new
                    {
                        studentID = studentId,
                        studentName = studentInfo?.FullName ?? "غير معروف",
                        academicYear = academicYear,
                        semester = semester,
                        level = level,
                        semesterHours = totalHoursNormal,

                        // 📊 المعدلات والتقديرات الكلية (الفصلية والتراكمية)
                        gpa = currentGPA,
                        cgpa = currentCGPA,
                        gpau = currentGPAU,
                        cgpau = currentCGPAU,
                        totalGradePoints = cumulativePointsNormal,
                        totalPercentage = currentSemesterPercentage,
                        overallLetter = overallLetter,

                        // 📝 تفاصيل درجات كل مادة بالتفصيل الممل (أعمال سنة، فاينال، إلخ) لتعرض في الشاشة
                        coursesReviewed = studentGrades.Select(g => new
                        {
                            courseCode = g.CourseOffering?.Course?.CourseID,
                            courseName = g.CourseOffering?.Course?.CourseNameAr,
                            courseNameEn = g.CourseOffering?.Course?.CourseNameEn,
                            category = g.CourseOffering?.Course?.CourseCategory,
                            hours = g.CourseOffering?.Course?.CreditHours,

                            // الدرجات التفصيلية المضافة حديثاً
                            attendanceGrade = g.AttendanceGrade,
                            assignmentsGrade = g.AssignmentsGrade,
                            quizzesGrade = g.QuizzesGrade,
                            midtermExamGrade = g.MidtermExamGrade,
                            practicalGrade = g.PracticalGrade,
                            oralGrade = g.OralGrade,
                            finalExamGrade = g.FinalExamGrade,

                            // المجاميع والتقدير الحرفي للمادة
                            numericGrade = g.NumericGrade,
                            percentage = g.Percentage,
                            letterGrade = g.LetterGrade,
                            gradePoints = g.GradePoints
                        }).ToList()
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = $"تم جلب نتائج درجات الطلاب للمستوى ({level}) بنجاح.",
                    data = resultsList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء معالجة بيانات الدرجات: {ex.Message}");
            }
        }
        [HttpGet("student-grades-history/{studentId}")]
        public async Task<IActionResult> GetStudentGradesHistory(int studentId)
        {
            // 1️⃣ جلب بيانات الطالب الأساسية
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound($"لا يوجد طالب مسجل برقم {studentId}");

            // 2️⃣ جلب تاريخ الدرجات المنشورة فقط لهذا الطالب (عشان الشئون والطالب يشوفوا نفس النتيجة الحقيقية)
            var allGrades = await _context.Grades
                .Include(g => g.CourseOffering.Course)
                .Where(g => g.StudentID == studentId && g.IsPublished == true) // 🔥 إضافة شرط الـ Publish هنا
                .ToListAsync();

            // 3️⃣ جلب ملخص الأترام للطالب من جدول الـ StudentSemesterResults اللي في الصورة الثانية
            var semesterSummaries = await _context.StudentSemesterResults
                .Where(ssr => ssr.StudentID == studentId)
                .ToListAsync();

            // 4️⃣ الـ Magic اللوجيك: تقسيم وتجميع الدرجات بناءً على السنة والترم بشكل منظم
            var academicHistory = allGrades
                .GroupBy(g => new { g.AcademicYear, g.Semester })
                .OrderByDescending(group => group.Key.AcademicYear)
                .ThenByDescending(group => group.Key.Semester)
                .Select(group =>
                {
                    // البحث عن ملخص الترم المطابق من الجدول التاني لربط الـ GPA والساعات
                    var summary = semesterSummaries
            .FirstOrDefault(s => s.AcademicYear?.ToString() == group.Key.AcademicYear.ToString()
                              && s.Semester?.ToString() == group.Key.Semester.ToString());

                    return new
                    {
                        AcademicYear = group.Key.AcademicYear,
                        Semester = group.Key.Semester,
                        // دمج بيانات الصورة التانية (Semester Results)
                        SemesterGPA = summary?.SemesterGPA ?? 0.0m,
                        SemesterHours = summary?.SemesterHours ?? 0,
                        CumulativeGPAAtThisPoint = summary?.CumulativeGPA ?? 0.0m,
                        CumulativeHoursAtThisPoint = summary?.CumulativeHours ?? 0,

                        // لستة المواد الخاصة بهذا الترم فقط بتفاصيلها
                        Courses = group.Select(g => new
                        {
                            g.GradeID,
                            CourseID = g.CourseOffering?.Course?.CourseID,
                            CourseNameAr = g.CourseOffering?.Course?.CourseNameAr,
                            CourseNameEn = g.CourseOffering?.Course?.CourseNameEn,
                            g.NumericGrade,
                            g.Percentage,
                            g.LetterGrade,
                            g.GradePoints,
                            g.IsPublished
                        }).ToList()
                    };
                }).ToList();

            // 5️⃣ تركيب الـ Object النهائي النظيف
            var result = new
            {
                student.StudentID,
                student.FullName,
                student.NameEn,
                CurrentGPA = student.GPA ?? 0.0m,
                TotalCompletedHours = student.CompletedCreditHours ?? 0,
                student.Status,
                CurrentYear = student.Year,
                AcademicHistory = academicHistory
            };

            return Ok(result);
        }



        [HttpGet("view-instructors-stats")]
        public async Task<IActionResult> GetInstructorsStats()
        {
            var instructors = await _context.Instructors
                .Select(i => new
                {
                    i.InstructorID,
                    FullName = i.FullName,
                    i.Email,
                    TotalCourses = _context.CourseOfferings.Count(co => co.InstructorID == i.InstructorID),
                    CoursesList = _context.CourseOfferings
                        .Where(co => co.InstructorID == i.InstructorID)
                        .Select(co => new
                        {
                            co.CourseID,
                            CourseName = co.Course.CourseNameEn,
                            CourseNameEn = co.Course.CourseNameAr,
                            StudentsCount = _context.Grades.Count(g => g.OfferingID == co.OfferingID)
                        }).ToList()
                }).ToListAsync();

            return Ok(instructors);
        }
        [HttpGet("instructor-details/{id}")]
        public async Task<IActionResult> GetInstructorDetails(int id)
        {
            // 1. جلب بيانات المحاضر مع الكورسات اللي بيدرسها
            var instructor = await _context.Instructors
                .Where(i => i.InstructorID == id)
                .Select(i => new
                {
                    i.InstructorID,
                    FullName = i.FullName,

                    i.Email,
                    // 2. قائمة الكورسات المرتبطة به من جدول الـ Offerings
                    TotalCourses = _context.CourseOfferings.Count(co => co.InstructorID == i.InstructorID),
                    CoursesList = _context.CourseOfferings
                        .Where(co => co.InstructorID == i.InstructorID)
                        .Select(co => new
                        {
                            co.CourseID,
                            CourseName = co.Course.CourseNameEn,
                            CourseNameEn = co.Course.CourseNameAr,

                            // عدد الطلاب المسجلين في كل مادة عنده
                            StudentsCount = _context.CourseRegistrations.Count(r => r.OfferingID == co.OfferingID)
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            // 3. التحقق من وجود المحاضر
            if (instructor == null)
            {
                return NotFound($"المحاضر صاحب الرقم {id} غير موجود.");
            }

            return Ok(instructor);
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

        [HttpGet("student-full-history/{id}")]
        public async Task<IActionResult> GetStudentFullHistory(int id)
        {
            var studentData = await _context.Students
                .Where(s => s.StudentID == id)
                .Select(s => new
                {
                    Info = new
                    {
                        s.FullName,
                        s.UniversityEmail,
                        s.Phone,
                        s.MajorAr,
                        s.Year,
                        s.Semester,
                        s.GPA,
                        s.CompletedCreditHours
                    },
                    CurrentSchedule = _context.Schedule
                        .Where(sch => sch.StudentID == id)
                        .Select(sch => new
                        {
                            sch.Day,
                            sch.StartTime,
                            sch.Room,
                            CourseName = sch.CourseID
                        }).ToList(),
                    AcademicHistory = _context.Grades
                        .Where(g => g.StudentID == id)
                        .OrderByDescending(g => g.CourseOffering.Year)
                        .ThenByDescending(g => g.CourseOffering.Semester)
                        .Select(g => new
                        {
                            Year = g.CourseOffering.Year,
                            Semester = g.CourseOffering.Semester,
                            CourseName = g.CourseOffering.Course.CourseNameAr,
                            Grade = g.LetterGrade,
                            Points = g.NumericGrade
                        }).ToList()
                }).FirstOrDefaultAsync();

            if (studentData == null) return NotFound("الطالب غير موجود");

            return Ok(studentData);
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

        private string GetOverallLetter(decimal c_gpa)
        {
            if (c_gpa >= 3.75m) return "A";
            if (c_gpa >= 3.00m) return "B";
            if (c_gpa >= 2.50m) return "C";
            if (c_gpa >= 2.00m) return "D";
            return "F";
        }
        private List<string> GetPossibleLevelNames(string level)
        {
            // تحويل النص لحروف صغيرة، إزالة المسافات، وتوحيد الياء والهمزات لتجنب أخطاء الكتابة العربية
            var normalized = level?.Trim().ToLower()
                .Replace("أ", "ا")
                .Replace("ى", "ي");

            switch (normalized)
            {
                case "1":
                case "first":
                case "الاول":
                    return new List<string> { "1", "First", "Level 1", "الاول" };

                case "2":
                case "second":
                case "الثاني":
                    return new List<string> { "2", "Second", "Level 2", "الثاني" };

                case "3":
                case "third":
                case "الثالث":
                    return new List<string> { "3", "Third", "Level 3", "الثالث" };

                case "4":
                case "fourth":
                case "الرابع":
                    return new List<string> { "4", "Fourth", "Level 4", "الرابع" };

                case "5":
                case "fifth":
                case "الخامس":
                    return new List<string> { "5", "Fifth", "Level 5", "الخامس" };

                default:
                    return new List<string> { level }; // لو مبعوت أي نمط تاني يرجعه زي ما هو آمن
            }
        }
    }
}
