using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text;
using WebsiteCreditHoursSystem.DTOs;
using WebsiteCreditHoursSystem.Models;
using static Azure.Core.HttpHeader;

namespace WebsiteCreditHoursSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // ممكن ترجعيه بعد ما مشكلة التوكن تتحل
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // ===============================
        // 1️⃣ عرض كل المستخدمين
        // ===============================
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.SystemUsers
                .Select(u => new
                {
                    u.UserID,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.IsActive
                })
                .ToListAsync();

            return Ok(users);
        }

        // ===============================
        // 2️⃣ إضافة مستخدم جديد
        // ===============================
        [HttpPost("users")]
        public async Task<IActionResult> AddUser([FromBody] CreateUserDto dto)
        {
            var user = new SystemUser
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Password = dto.Password,
                Role = dto.Role,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.Now
            };

            _context.SystemUsers.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User created successfully" });
        }

        // ===============================
        // 3️⃣ تعديل مستخدم
        // ===============================
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _context.SystemUsers.FindAsync(id);

            if (user == null)
                return NotFound("User not found");

            user.FullName = dto.FullName;
            user.Email = dto.Email;
            user.Password = dto.Password;
            user.Role = dto.Role;
            user.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new { message = "User updated successfully" });
        }

        // ===============================
        // 4️⃣ حذف مستخدم
        // ===============================
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.SystemUsers.FindAsync(id);

            if (user == null)
                return NotFound("User not found");

            _context.SystemUsers.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User deleted successfully" });
        }

        // ===============================
        // 5️⃣ عرض الطلاب بفلترة (تم مسح براميتر academicYear لعدم استخدامه)
        // ===============================

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
            var studentData = await _context.Students
                .Include(s => s.CourseRegistrations)
                    .ThenInclude(r => r.CourseOffering)
                        .ThenInclude(co => co.Course)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (studentData == null)
                return NotFound(new { message = "الطالب غير موجود" });

            var result = new
            {
                student = new
                {
                    studentData.StudentID,
                    studentData.NameEn,
                    studentData.Email,
                    studentData.Year,
                    studentData.Status,
                    studentData.Semester,
                    studentData.MaxAllowedHours,
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
        [HttpDelete("delete-student-safely/{id}")]
        // [Authorize(Roles = "Admin")] // تفعّل لاحقاً للأدمن فقط
        public async Task<IActionResult> DeleteStudentSafely(int id)
        {
            // 1. البحث عن الطالب في قاعدة البيانات
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound(new { success = false, message = $"الطالب صاحب الرقم {id} غير موجود في النظام." });
            }

            // 2. التحقق من وجود أي سجل أكاديمي (درجات) مرتبط بالطالب
            var hasGrades = await _context.Grades.AnyAsync(g => g.StudentID == id);
            if (hasGrades)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "عذراً، لا يمكن حذف هذا الطالب نهائياً يا نجلاء لأن له سجل درجات أكاديمي في الكنترول. ممكن تغيري   حالته إلى (غير نشط) من شاشة تعديل الحالة."
                });
            }

            // 3. التحقق من وجود تسجيلات مواد أو جداول (حتى لو لم يرصد لها درجات بعد)
            var hasRegistrations = await _context.CourseRegistrations.AnyAsync(r => r.StudentID == id);
            if (hasRegistrations)
            {
                return BadRequest(new
                {
                    success = false,
                    message = " نجلاء خدي بالك لا يمكن حذف الطالب لوجود مواد مسجلة له هذا الترم. لازم الاولي نلغي تاريخو وتسجيلاتو يا نجلاء وبعدين نحذفو ودا مش متاح ف كبري دماغك مع تحياتي روقا ."
                });
            }

            try
            {
                // 4. إذا تخطى الشروط السابقة (أي أنه طالب جديد تماماً بدون أي سجل)، يتم حذفه بأمان
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = $"تم حذف الطالب {student.NameEn} نهائياً من النظام بنجاح." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"حدث خطأ غير متوقع أثناء الحذف: {ex.Message}" });
            }
        }
        // ===============================
        // 7️⃣ إضافة محاضر (مع حماية الـ Transaction)
        [HttpPost("add-instructor")]
        public async Task<IActionResult> AddInstructor([FromBody] InstructorDTO model)
        {
            if (await _context.SystemUsers.AnyAsync(u => u.Email == model.Email))
            {
                return BadRequest("هذا الإيميل مسجل مسبقاً.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. إنشاء اليوزر أولاً (هنا بنحط الباسورد)
                var newUser = new SystemUser
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password, // الحقل ده موجود في SystemUser فقط
                    Role = "Instructor",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.SystemUsers.Add(newUser);
                await _context.SaveChangesAsync(); // بنحفظ هنا عشان ناخد الـ UserID

                // 2. إنشاء المحاضر (هنا مفيش باسورد)
                var newInstructor = new Instructor
                {
                    NameEn = model.FullName,
                   NameAr = model.FullName,
                    FullName = model.FullName,
                    Email = model.Email,
                    UserId = newUser.UserID // بنربطه باليوزر اللي لسه مكرتينه
                };

                _context.Instructors.Add(newInstructor);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(new { message = "تمت إضافة المحاضر بنجاح" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest("حدث خطأ أثناء الحفظ: " + ex.Message);
            }
        }
        public class InstructorDTO
        {
           // public string NameEn { get; set; }
           // public string NameAr { get; set; }
            public string FullName { get; set; }

            public string Email { get; set; }
            public string Password { get; set; } // الحقل ده هو اللي هيشيل الأيرور
        }


        public class InstructorUBDATE
        {
           //  public string NameEn { get; set; }
            // public string NameAr { get; set; }
            public string FullName { get; set; }

            public string Email { get; set; }
           // public string Password { get; set; } // الحقل ده هو اللي هيشيل الأيرور
        }

        // ===============================
        // 8️⃣ إحصائيات المحاضرين
        // ===============================
        [HttpGet("view-instructors-stats")]
        public async Task<IActionResult> GetInstructorsStats()
        {
            var instructors = await _context.Instructors
                .Select(i => new
                {
                    i.InstructorID,
                    i.FullName,
                    i.Email,
                    TotalCourses = _context.CourseOfferings.Count(co => co.InstructorID == i.InstructorID),
                    CoursesList = _context.CourseOfferings
                        .Where(co => co.InstructorID == i.InstructorID)
                        .Select(co => new
                        {
                            co.CourseID,
                            CourseName = co.Course.CourseNameEn,
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
        // ===============================
        // 9️⃣ تعديل وحذف المحاضر
        // ===============================
        //[FromBody] InstructorUBDATE updatedData
        [HttpPut("update-instructor/{instructorId}")]
        public async Task<IActionResult> UpdateInstructor(int instructorId, [FromBody] InstructorUBDATE updatedData)
        {
            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null) return NotFound("المحاضر غير موجود");

            instructor.NameAr = updatedData.FullName;
            instructor.Email = updatedData.Email;
            instructor.FullName = updatedData.FullName;
            instructor.NameEn = updatedData.FullName;

            if (instructor.UserId.HasValue)
            {
                var linkedUser = await _context.SystemUsers.FindAsync(instructor.UserId);
                if (linkedUser != null)
                {
                    linkedUser.FullName = updatedData.FullName;
                    linkedUser.Email = updatedData.Email;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "تم التحديث بنجاح" });
        }

        [HttpDelete("delete-instructor/{instructorId}")]
        public async Task<IActionResult> DeleteInstructor(int instructorId)
        {
            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null) return NotFound("المحاضر غير موجود");

            var hasCourses = await _context.CourseOfferings.AnyAsync(co => co.InstructorID == instructorId);
            if (hasCourses) return BadRequest("لا يمكن حذف المحاضر لارتباطه بجدول محاضرات.");

            if (instructor.UserId.HasValue)
            {
                var user = await _context.SystemUsers.FindAsync(instructor.UserId);
                if (user != null) _context.SystemUsers.Remove(user);
            }

            _context.Instructors.Remove(instructor);
            await _context.SaveChangesAsync();
            return Ok(new { message = "تم حذف المحاضر وحسابه بنجاح" });
        }


        // 1. عرض كل المواد بناءً على السنة الدراسية والليفل والسمستر
        // ===============================
        // 1️⃣ عرض الكورسات (نفس ترتيبك)
        // ===============================
        [HttpGet("view-courses")]
        public async Task<IActionResult> GetCourses(int? level, int? semester)
        {
            var query = _context.Courses.AsQueryable();

            if (level.HasValue)
            {
                query = query.Where(c => c.Level == level.Value);
            }

            if (semester.HasValue)
            {
                query = query.Where(c => c.Semester == semester.Value);
            }

            var results = await query.Select(c => new {
                c.CourseID,
                c.CourseNameAr,
                c.CourseNameEn,
                c.CourseType,
                c.CourseCategory,
                level = c.Level ?? 0,
                semester = c.Semester ?? 0,
                
                creditHours = c.CreditHours ?? 0
            }).ToListAsync();

            if (!results.Any())
            {
                return NotFound("لا توجد كورسات مسجلة لهذا المستوى أو الترم.");
            }

            return Ok(results);
        }

        // ===============================
        // 2️⃣ الطلاب المسجلين (تم حذف string courseId الزائد)
        // ===============================
        [HttpGet("course-enrollments/{courseId}")]
        public async Task<IActionResult> GetCourseEnrollments(string courseId)
        {
            var course = await _context.Courses
                .Where(c => c.CourseID == courseId)
                .Select(c => new {
                    c.CourseID,
                    c.CourseNameEn,
                    c.CourseNameAr,
                    level = c.Level ?? 0,
                    semester = c.Semester ?? 0,
                    c.CourseType,
                    c.CourseCategory,
                    c.CreditHours,
                    students = _context.CourseRegistrations
                        .Where(r => r.CourseOffering.CourseID == courseId)
                        .Select(r => new {
                            studentID = r.StudentID,
                            studentName = r.Student.NameEn,
                            status = r.Status
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (course == null)
                return NotFound($"الكورس رقم {courseId} غير موجود.");

            return Ok(course);
        }

        // ===============================
        // 3️⃣ إضافة مادة (تم تنظيف البراميترز)
        // ===============================
        [HttpPost("add-course")]
        public async Task<IActionResult> AddCourse(string courseId, string nameAr, string nameEn, int hours, int semester, int level, string courseType, string CourseCategory)
        {
            var newCourse = new Course
            {
                CourseID = courseId,
                CourseNameAr = nameAr,
                CourseNameEn = nameEn,
                CreditHours = hours,
                Semester = semester,
                Level = level,
                CourseCategory= CourseCategory,
                CourseType = courseType
            };

            try
            {
                _context.Courses.Add(newCourse);
                await _context.SaveChangesAsync();
                return Ok("تم إضافة المادة بنجاح.");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"خطأ في الداتا بيز: {innerException}");
            }
        }

        // ===============================
        // 4️⃣ حذف مادة
        // ===============================
        [HttpDelete("delete-course/{id}")]
        public async Task<IActionResult> DeleteCourse(string id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound("المادة غير موجودة.");

            try
            {
                var dependentCourses = _context.Courses.Where(c => c.PrerequisiteId == id);
                foreach (var dep in dependentCourses)
                {
                    dep.PrerequisiteId = null;
                }

                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();
                return Ok($"تم حذف مادة {course.CourseNameAr} بنجاح.");
            }
            catch (Exception)
            {
                return StatusCode(500, "لا يمكن حذف المادة لارتباطها بسجلات أخرى.");
            }
        }

        // ===============================
        // 5️⃣ تحديث مادة
        // ===============================
        [HttpPut("update-course/{id}")]
        public async Task<IActionResult> UpdateCourse(string id, string nameAr, string nameEn, int hours, int semester, int level, string courseType , string CourseCategory)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound("المادة غير موجودة.");

            course.CourseNameAr = nameAr;
            course.CourseNameEn = nameEn;
            course.CreditHours = hours;
            course.Semester = semester;
            course.Level = level;
            course.CourseType = courseType;
            course.CourseCategory = CourseCategory;

            await _context.SaveChangesAsync();
            return Ok("تم تحديث بيانات المادة بنجاح.");
        }

        // ===============================
        [HttpPost("setup-course-offering-and-schedule")]
        public async Task<IActionResult> SetupCourseOfferingAndSchedule([FromBody] UpdateOfferingDto dto)
        {
            var courseInfo = await _context.Courses.FirstOrDefaultAsync(c => c.CourseID == dto.CourseID);
            if (courseInfo == null) return NotFound("المادة غير موجودة.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string currentSessionType = dto.SessionType ?? "Lecture";

                // 1️⃣ التعامل مع جدول الـ CourseOfferings (الفصل بناءً على المادة والدكتور ونوع السيشن)
                // 🌟 التعديل السحري: ضفنا تشيك على الـ SessionType عشان يعمل سطر جديد لو النوع مختلف 🌟
                var existingOffering = await _context.CourseOfferings
                    .FirstOrDefaultAsync(o => o.CourseID == dto.CourseID
                                           && o.InstructorID == dto.InstructorID
                                           && _context.Schedule
                                                .Where(s => s.CourseID == o.CourseID
                                                         && s.InstructorID == o.InstructorID
                                                         && s.Day == o.Day
                                                         && s.StartTime == o.StartTime
                                                         && s.StudentID == null)
                                                .Select(s => s.SessionType)
                                                .FirstOrDefault() == currentSessionType);

                if (existingOffering != null)
                {
                    // لو السطر ده بالنوع ده موجود قبل كده، بنحدث المواعيد والسعة بتاعته فوراً
                    existingOffering.Day = dto.Day;
                    existingOffering.StartTime = dto.StartTime;
                    existingOffering.EndTime = dto.EndTime;
                    existingOffering.Room = dto.Room;
                    existingOffering.Capacity = dto.Capacity;

                    _context.CourseOfferings.Update(existingOffering);
                }
                else
                {
                    // لو أول مرة ينزل النوع ده (مثلاً لاب أو سكشن) للدكتور والمادة دي، بيعمل سطر جديد تماماً بـ ID جديد
                    var newOffering = new CourseOffering
                    {
                        CourseID = dto.CourseID,
                        InstructorID = dto.InstructorID,
                        Semester = dto.Semester ?? courseInfo.Semester ?? 1,
                        Year = dto.Year ?? courseInfo.Level ?? 1,
                        Day = dto.Day,
                        StartTime = dto.StartTime,
                        EndTime = dto.EndTime,
                        Room = dto.Room,
                        Capacity = dto.Capacity
                    };
                    _context.CourseOfferings.Add(newOffering);
                }

                // 2️⃣ التسميع في جدول الـ Schedule العام (زي ما هو ومضمون)
                if (!string.IsNullOrEmpty(dto.Day) && dto.StartTime.HasValue)
                {
                    var oldSchedules = await _context.Schedule
                        .Where(s => s.CourseID == dto.CourseID
                                 && s.InstructorID == dto.InstructorID
                                 && s.Day == dto.Day
                                 && s.StartTime == dto.StartTime
                                 && s.SessionType == currentSessionType
                                 && s.StudentID == null)
                        .ToListAsync();

                    if (oldSchedules.Any()) _context.Schedule.RemoveRange(oldSchedules);

                    var scheduleLecture = new ScheduleLecture
                    {
                        CourseID = dto.CourseID,
                        InstructorID = dto.InstructorID,
                        Semester = dto.Semester?.ToString() ?? courseInfo.Semester?.ToString() ?? "1",
                        Year = dto.Year ?? courseInfo.Level ?? 1,
                        Day = dto.Day,
                        StartTime = dto.StartTime,
                        EndTime = dto.EndTime,
                        Room = dto.Room,
                        SessionType = currentSessionType,
                        StudentID = null
                    };
                    _context.Schedule.Add(scheduleLecture);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true, message = "تم الحفظ بنجاح." });
            }
            catch (Exception ex) { await transaction.RollbackAsync(); return StatusCode(500, ex.Message); }
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
                                            // 🌟 التعديل السحري هنا: فصل الوقت وإضافة الثواني بالظبط زي الصورة الثانية 🌟
                                            StartTime = l.StartTime?.ToString(@"hh\:mm\:ss"), // ينتج: "02:00:00"
                                            EndTime = l.EndTime?.ToString(@"hh\:mm\:ss"),     // ينتج: "03:00:00"
                                           
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
        // 7️⃣ عرض جدول العروض بالكامل
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
        [HttpPost("admin-manual-registration")]
        public async Task<IActionResult> AdminManualRegistration(int studentId, int offeringId)
        {
            try
            {
                // 1. التأكد من وجود الطالب في النظام أولاً
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);
                if (student == null) return NotFound("الطالب غير موجود في السيستم.");

                // 2. البحث المباشر بالـ offeringId بدون قيود
                var offering = await _context.CourseOfferings
                    .Include(o => o.Course)
                    .FirstOrDefaultAsync(o => o.OfferingID == offeringId);

                if (offering == null)
                    return NotFound("عرض المادة هذا غير موجود في النظام.");

                // 3. نتأكد إن الطالب مش مسجل في نفس الـ Offering ده بالذات قبل كدة
                var exists = await _context.CourseRegistrations
                    .AnyAsync(r => r.StudentID == studentId && r.OfferingID == offeringId);

                if (exists) return BadRequest("الطالب مسجل بالفعل في هذه المجموعة/المادة.");

                // 4. عملية التسجيل الاستثنائي
                var registration = new CourseRegistration
                {
                    StudentID = studentId,
                    OfferingID = offeringId,
                    RegistrationDate = DateTime.Now,
                    Status = "Registered" // حالة استثنائية مدخلة من الأدمن
                };

                _context.CourseRegistrations.Add(registration);

                // 5. تقليل سعة المجموعة المحددة
                offering.Capacity -= 1;
                _context.CourseOfferings.Update(offering);

                // 6. 🌟 المتطابق تماماً مع كود الطالب 🌟
                // إنشاء سطر الدرجات بنفس المواصفات والحقول الافتراضية
                // 🔥 الحل: تحويل القيم النصية أو الرقمية إلى متغيرات جاهزة قبل القوس لمنع خطأ الـ TryParse
                // 6. 🌟 إنشاء سطر الدرجات الافتراضي المؤمن بالكامل وبدون أي أخطاء كومبيلر 🌟

                // نأخذ القيمة الرقمية مباشرة، وإذا كانت فارغة نضع السنة الحالية
                int finalAcademicYear = offering.Course?.AcademicYear ?? DateTime.Now.Year;

                // نأخذ رقم الترم مباشرة، وإذا كان فارغاً نضع 1
                int finalSemester = 1;
                if (offering.Semester.HasValue)
                {
                    finalSemester = offering.Semester.Value;
                }
                else if (int.TryParse(offering.Semester?.ToString(), out int sem))
                {
                    finalSemester = sem; // احتياطاً لو كان الموديل معرفاً بنوع آخر
                }

                var emptyGradeRecord = new WebsiteCreditHoursSystem.Models.Grade
                {
                    StudentID = studentId,
                    OfferingID = offeringId,
                    AcademicYear = finalAcademicYear, // إدخال القيمة المؤمنة مباشرة
                    Semester = finalSemester,         // إدخال القيمة المؤمنة مباشرة
                    IsPublished = false,
                    Status = "Draft",

                    // كل خانات الدرجات التفصيلية تنزل NULL مستنية رصد الدكتور
                    AttendanceGrade = null,
                    AssignmentsGrade = null,
                    QuizzesGrade = null,
                    MidtermExamGrade = null,
                    PracticalGrade = null,
                    OralGrade = null,
                    FinalExamGrade = null,
                    NumericGrade = null,
                    Percentage = null,
                    LetterGrade = null,
                    GradePoints = null
                };

                _context.Grades.Add(emptyGradeRecord);
                // 7. حفظ التغييرات
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = $"تم تسجيل الطالب بنجاح، وتم إنشاء سطر رصد الدرجات (Draft) لمادة {offering.Course?.CourseNameAr}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء التسجيل اليدوي: {ex.Message} -> {ex.InnerException?.Message}");
            }
        }
        /*
        [HttpPost("admin-manual-registration")]
        public async Task<IActionResult> AdminManualRegistration(int studentId, int offeringId)
        {
            try
            {
                // 1. التأكد من وجود الطالب في النظام أولاً
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);
                if (student == null) return NotFound("الطالب غير موجود في السيستم.");

                // 2. 👈 البحث المباشر بالـ offeringId بدون أي قيود على الترم أو السنة (قمة المرونة)
                var offering = await _context.CourseOfferings
                    .Include(o => o.Course)
                    .FirstOrDefaultAsync(o => o.OfferingID == offeringId);

                if (offering == null)
                    return NotFound("عرض المادة هذا غير موجود في النظام.");

                // 3. نتأكد إن الطالب مش مسجل في نفس الـ Offering ده بالذات قبل كدة
                var exists = await _context.CourseRegistrations
                    .AnyAsync(r => r.StudentID == studentId && r.OfferingID == offeringId);

                if (exists) return BadRequest("الطالب مسجل بالفعل في هذه المجموعة/المادة.");

                // 4. عملية التسجيل الاستثنائي
                var registration = new CourseRegistration
                {
                    StudentID = studentId,
                    OfferingID = offeringId,
                    RegistrationDate = DateTime.Now,
                    Status = "Registered" // عشان نكون عارفين إنها حالة استثنائية من شؤون الطلاب
                };

                _context.CourseRegistrations.Add(registration);

                // 5. تقليل سعة المجموعة المحددة
                offering.Capacity -= 1;
                _context.CourseOfferings.Update(offering);

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = $"تم تسجيل الطالب بنجاح في مادة {offering.Course?.CourseNameAr} (ترم {offering.Semester} - سنة {offering.Year})." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء التسجيل اليدوي: {ex.Message}");
            }
        }*/
        // 2. عرض الطلاب المسجلين في مجموعة/عرض معين للمادة
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
        // 3. حذف عرض المادة
        [HttpDelete("delete-offering/{offeringId}")] // 👈 التعديل هنا: الحذف باستخدام الـ offeringId الفريد
        public async Task<IActionResult> DeleteOffering(int offeringId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1️⃣ البحث عن الـ Offering ومعه سجلات الطلاب المسجلين فيه
                var offering = await _context.CourseOfferings
                    .Include(o => o.CourseRegistrations)
                    .FirstOrDefaultAsync(o => o.OfferingID == offeringId);

                if (offering == null)
                    return NotFound("عذراً، هذا العرض غير موجود.");

                // 2️⃣ حماية البيانات: نمنع الحذف لو فيه طالب واحد على الأقل سجل في المجموعة دي
                if (offering.CourseRegistrations != null && offering.CourseRegistrations.Any())
                {
                    return BadRequest("لا يمكن حذف هذا العرض لأن هناك طلاباً قاموا بالتسجيل في هذه المجموعة بالفعل.");
                }

                // 3️⃣ تنظيف جدول الـ Schedule: بنجيب كل المواعيد (محاضرة، لاب، سكشن) المربوطة بالمادة والدكتور ده
                var relatedSchedules = await _context.Schedule
                    .Where(s => s.CourseID == offering.CourseID
                             && s.InstructorID == offering.InstructorID
                             && s.StudentID == null) // عشان نضمن إنها مواعيد الجدول الأساسي للمادة
                    .ToListAsync();

                if (relatedSchedules.Any())
                {
                    _context.Schedule.RemoveRange(relatedSchedules); // حذف كل المواعيد المرتبطة دفعة واحدة
                }

                // 4️⃣ حذف الـ Offering نفسه
                _context.CourseOfferings.Remove(offering);

                // حفظ التغييرات وتأكيد العملية
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "تم حذف عرض المادة والمواعيد المرتبطة به من الجدول بنجاح." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"حدث خطأ أثناء الحذف: {ex.Message}");
            }
        }

        [HttpPut("update-course-offering-and-schedule/{offeringId}")]
        public async Task<IActionResult> UpdateCourseOfferingAndSchedule(int offeringId, [FromBody] UpdateOfferingDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var offering = await _context.CourseOfferings.FirstOrDefaultAsync(o => o.OfferingID == offeringId);
                if (offering == null) return NotFound("عرض المادة غير موجود.");

                // 🌟 الاحتفاظ بالبيانات القديمة (بالذات الوقت واليوم) عشان نلاقيهم في جدول الـ Schedule 🌟
                string oldCourseId = offering.CourseID;
                int? oldInstructorId = offering.InstructorID;
                string oldDay = offering.Day;
                TimeSpan? oldStartTime = offering.StartTime;

                string currentSessionType = dto.SessionType ?? "Lecture";

                // 1️⃣ تحديث جدول الـ CourseOffering بالكامل (لأي نوع سيشن لأن كل وحدة ليها سطرها المنفصل)
                offering.CourseID = dto.CourseID;
                offering.InstructorID = dto.InstructorID;
                offering.Semester = dto.Semester;
                offering.Year = dto.Year;
                offering.Capacity = dto.Capacity;

                // 🌟 شيلنا شرط الـ Lecture هنا لأن الـ Offering الحالي يمثل السيشن دي بالذات 🌟
                offering.Day = dto.Day;
                offering.StartTime = dto.StartTime;
                offering.EndTime = dto.EndTime;
                offering.Room = dto.Room;

                _context.CourseOfferings.Update(offering);

                // 2️⃣ تحديث السجل جوه جدول الـ Schedule
                // 🌟 دقة البحث هنا زادت: بنبحث بالمادة، الدكتور، النوع، واليوم والوقت القدام بالملّي 🌟
                var scheduleLecture = await _context.Schedule
                    .FirstOrDefaultAsync(s => s.CourseID == oldCourseId
                                           && s.InstructorID == oldInstructorId
                                           && s.SessionType == currentSessionType
                                           && s.Day == oldDay
                                           && s.StartTime == oldStartTime
                                           && s.StudentID == null);

                if (scheduleLecture != null)
                {
                    // تحديث السجل بالمواعيد والبيانات الجديدة بالكامل
                    scheduleLecture.CourseID = dto.CourseID;
                    scheduleLecture.InstructorID = dto.InstructorID;
                    scheduleLecture.Day = dto.Day;
                    scheduleLecture.StartTime = dto.StartTime;
                    scheduleLecture.EndTime = dto.EndTime;
                    scheduleLecture.Room = dto.Room;
                    scheduleLecture.SessionType = currentSessionType;
                    scheduleLecture.Year = dto.Year ?? scheduleLecture.Year;
                    scheduleLecture.Semester = dto.Semester?.ToString() ?? scheduleLecture.Semester;

                    _context.Schedule.Update(scheduleLecture);
                }
                // حالة احتياطية: لو مكنش نزل ميعاد في الـ Schedule أصلاً
                else if (!string.IsNullOrEmpty(dto.Day) && dto.StartTime.HasValue)
                {
                    var newSchedule = new ScheduleLecture
                    {
                        CourseID = dto.CourseID,
                        InstructorID = dto.InstructorID,
                        Semester = dto.Semester?.ToString() ?? "1",
                        Year = dto.Year ?? 1,
                        Day = dto.Day,
                        StartTime = dto.StartTime,
                        EndTime = dto.EndTime,
                        Room = dto.Room,
                        SessionType = currentSessionType,
                        StudentID = null
                    };
                    _context.Schedule.Add(newSchedule);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true, message = "تم تحديث البيانات والتنسيق بين الجداول بنجاح." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"حدث خطأ أثناء التعديل: {ex.Message}");
            }
        }
        [HttpDelete("drop-student-registration")] //
        public async Task<IActionResult> DropRegistration([FromQuery] int studentId, [FromQuery] int offeringId) // 👈 التعديل هنا: استبدال courseId بـ offeringId
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1️⃣ البحث الصارم عن سجل تسجيل الطالب في هذا الـ Offering بالذات
                var registration = await _context.CourseRegistrations
                    .Include(r => r.CourseOffering) // بنجيب معاه الـ Offering عشان نعدل السعة
                    .FirstOrDefaultAsync(r => r.StudentID == studentId && r.OfferingID == offeringId);

                if (registration == null)
                    return NotFound("عذراً، هذا الطالب غير مسجل في هذه المجموعة/المادة.");

                // 2️⃣ إرجاع السعة المتاحة (Capacity) للمادة: بنزودها 1 لأن الطالب ساب مكانه
                if (registration.CourseOffering != null)
                {
                    registration.CourseOffering.Capacity += 1;
                    _context.CourseOfferings.Update(registration.CourseOffering);
                }

                // 3️⃣ حذف سجل التسجيل نهائياً من الداتا بيز
                _context.CourseRegistrations.Remove(registration);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "تم حذف تسجيل الطالب وإرجاع السعة المتاحة للمجموعة بنجاح." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"حدث خطأ أثناء حذف تسجيل الطالب: {ex.Message}");
            }
        }

        // 6. تحديث حالة التسجيل
        [HttpPatch("update-registration-status")]
        public async Task<IActionResult> UpdateRegistrationStatus(int studentId, int offeringId, string newStatus) // 👈 تعديل: استبدال courseId بـ offeringId
        {
            try
            {
                // البحث الصارم برقم الطالب والـ offeringId المحدد
                var registration = await _context.CourseRegistrations
                    .FirstOrDefaultAsync(r => r.StudentID == studentId && r.OfferingID == offeringId);

                if (registration == null)
                    return NotFound("سجل التسجيل غير موجود لهذا الطالب في هذه المجموعة الحالية.");

                // تحديث الحالة يدوياً
                registration.Status = newStatus;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = $"تم تحديث حالة التسجيل بنجاح إلى: {newStatus}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء تحديث الحالة: {ex.Message}");
            }
        }
        // 1. إندبوينت عشان الفرونت إند يقدر يقرأ السنة والترم الحاليين وحالة التسجيل ويعرضهم للأدمين
        [HttpGet("system-settings")]
        public async Task<IActionResult> GetSystemSettings()
        {
            // بنجيب السطر الوحيد اللي في الجدول
            var settings = await _context.SystemControls.FirstOrDefaultAsync();

            // لو الجدول فاضي خالص (أول مرة تشغلوا السيستم)، الكود هيعمل سطر افتراضي أوتوماتيك عشان السيستم ميعطلش
            if (settings == null)
            {
                var defaultSettings = new SystemControl
                {
                    CurrentAcademicYear = "2025/2026",
                    CurrentSemester = "1",
                    IsRegistrationOpen = false
                };
                _context.SystemControls.Add(defaultSettings);
                await _context.SaveChangesAsync();
                return Ok(defaultSettings);
            }

            return Ok(settings);
        }

        // 2. إندبوينت عشان الأدمين يقدر يعدل السنة أو الترم أو يفتح ويقفل التسجيل من شاشته
        [HttpPut("update-system-settings")]
        public async Task<IActionResult> UpdateSystemSettings(string academicYear, string semester, bool isRegistrationOpen)
        {
            var settings = await _context.SystemControls.FirstOrDefaultAsync();

            if (settings == null) return NotFound("إعدادات النظام غير موجودة في قاعدة البيانات.");

            // تحديث القيم بناءً على اللي الأدمين هيدخله في الشاشة
            settings.CurrentAcademicYear = academicYear;   // مثلاً: "2025/2026"
            settings.CurrentSemester = semester;           // مثلاً: "2" أو "Summer"
            settings.IsRegistrationOpen = isRegistrationOpen; // true أو false

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"تم تحديث إعدادات النظام بنجاح! السنة الحالية: {academicYear}، الترم الحالي: {semester}، حالة التسجيل: {(isRegistrationOpen ? "مفتوح" : "مغلق")}"
            });
        }
        [HttpPatch("toggle-registration")]
        public async Task<IActionResult> ToggleRegistration([FromQuery] bool isOpen)
        {
            try
            {
                // 1️⃣ البحث عن سجل التحكم الوحيد في السيستم
                var control = await _context.SystemControls.FirstOrDefaultAsync();

                if (control == null)
                {
                    // لو مش موجود، بننشئ سجل جديد ونحط حالة الفتح/القفل
                    control = new SystemControl
                    {
                        IsRegistrationOpen = isOpen
                        // لو عمود الـ CurrentSemester إجباري في الداتابيز، تقدري تحطي قيمة افتراضية أو تسيبيه null لو يقبل
                    };
                    _context.SystemControls.Add(control);
                }
                else
                {
                    // لو موجود، بنحدث حالة الفتح أو الغلق بناءً على اختيار الأدمن
                    control.IsRegistrationOpen = isOpen;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    status = isOpen,
                    message = isOpen
                        ? "تم فتح باب التسجيل للطلاب رسمياً بشكل عام."
                        : "تم غلق باب التسجيل للطلاب رسمياً بشكل عام."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"حدث خطأ أثناء تحديث إعدادات النظام: {ex.Message}");
            }
        }
        [HttpGet("search-students")]
        public async Task<IActionResult> GetStudents(string? name, string? year, string? semester)
        {
            var query = _context.Students.AsQueryable();

            if (!string.IsNullOrEmpty(name))
                query = query.Where(s => s.NameAr.Contains(name) || s.NameEn.Contains(name));

            if (!string.IsNullOrEmpty(year))
                query = query.Where(s => s.Year == year);

            if (!string.IsNullOrEmpty(semester))
                query = query.Where(s => s.Semester == semester);

            var results = await query.Select(s => new
            {
                s.StudentID,
                s.NameAr,
                s.NameEn,
                s.Email,
                s.Year,
                s.Semester,
                GPA = s.GPA != null ? s.GPA : 0.0m,
                CompletedHours = s.CompletedCreditHours ?? 0
            }).ToListAsync();

            return Ok(results);
        }

        // 9. تحديث ساعات الطالب القصوى
        [HttpPatch("update-student-max-hours/{studentId}")]
        public async Task<IActionResult> UpdateMaxHours(int studentId, int hours)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null) return NotFound("الطالب غير موجود");

            student.MaxAllowedHours = hours;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"تم تحديث ساعات الطالب {student.NameAr} إلى {hours} ساعة بنجاح" });
        }

        // 10. السجل الأكاديمي
        [HttpGet("full-academic-history/{studentId}")]
        public async Task<IActionResult> GetFullAcademicHistory(int studentId)
        {
            try
            {
                var student = await _context.Students
                    .Include(s => s.CourseRegistrations).ThenInclude(r => r.CourseOffering).ThenInclude(o => o.Course)
                    .Include(s => s.CourseRegistrations).ThenInclude(r => r.CourseOffering).ThenInclude(o => o.Instructor)
                    .FirstOrDefaultAsync(s => s.StudentID == studentId);

                if (student == null) return NotFound("الطالب غير موجود.");

                var academicHistory = student.CourseRegistrations
                    .GroupBy(r => new { r.CourseOffering.Year, r.CourseOffering.Semester })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Semester)
                    .Select(group => new
                    {
                        Level = group.Key.Year,
                        Semester = group.Key.Semester,
                        Courses = group.Select(r => new
                        {
                            CourseCode = r.CourseOffering.CourseID,
                            CourseNameAr = r.CourseOffering.Course.CourseNameAr,
                            CourseNameEn = r.CourseOffering.Course.CourseNameEn,
                            Instructor = r.CourseOffering.Instructor.NameEn,
                            CreditHours = r.CourseOffering.Course.CreditHours,
                            Grade = r.Status,
                            RegistrationDate = r.RegistrationDate.ToString("yyyy-MM-dd")
                        }).ToList(),
                        TotalSemesterCredits = group.Sum(r => r.CourseOffering.Course.CreditHours)
                    }).ToList();

                var fullProfile = new
                {
                    PersonalData = new
                    {
                        student.StudentID,
                        student.NameEn,
                        student.Email,
                        student.Phone,
                        CurrentLevel = student.Year,
                        CurrentSemester = student.Semester,
                        TotalCreditsEarned = student.CourseRegistrations.Where(r => r.Status == "Passed").Sum(r => r.CourseOffering.Course.CreditHours)
                    },
                    AcademicHistory = academicHistory
                };

                return Ok(fullProfile);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"خطأ في جلب السجل الأكاديمي: {ex.Message}");
            }
        }

        // 11. تحديث بيانات الطالب الأساسية
        [HttpPut("update-student/{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentUpdateDto updatedData)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound("الطالب غير موجود.");

            if (!string.IsNullOrEmpty(updatedData.Name)) student.NameEn = updatedData.Name;
            if (!string.IsNullOrEmpty(updatedData.Email)) student.Email = updatedData.Email;
            if (!string.IsNullOrEmpty(updatedData.Phone)) student.Phone = updatedData.Phone;
            if (!string.IsNullOrEmpty(updatedData.Level)) student.Year = updatedData.Level;
            if (!string.IsNullOrEmpty(updatedData.Semester)) student.Semester = updatedData.Semester;

            await _context.SaveChangesAsync();
            return Ok(new { message = "تم تحديث بيانات الطالب بنجاح", currentData = student });
        }
        // كلاس صغير (DTO) لتنظيم البيانات المبعوثة في الطلب
        public class StudentUpdateDto
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? Level { get; set; }
            public string? Semester { get; set; }
        }


        // 12. تغيير حالة الطالب
        [HttpPatch("change-student-status/{id}")]
        public async Task<IActionResult> ChangeStatus(int id, string newStatus)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound("الطالب غير موجود.");

            student.Status = newStatus;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"تم تغيير حالة الطالب {student.NameEn} إلى {newStatus} بنجاح.", currentStatus = student.Status });
        }


        [HttpGet("view-all-prerequisites")]
        public async Task<IActionResult> GetAllPrereqs([FromQuery] string lang = "en")
        {
            var list = await _context.CoursePrerequisites
                .Include(p => p.Course)
                .Include(p => p.PrerequisiteCourse) // 👈 Include للاثنين معاً
                .Select(p => new {
                    p.PrereqID,
                    p.CourseID,
                    CourseName = lang == "ar"
                        ? (p.Course != null ? p.Course.CourseNameAr : p.CourseID)
                        : (p.Course != null ? p.Course.CourseNameEn : p.CourseID),

                    p.PrerequisiteCourseID,
                    PrerequisiteCourseName = lang == "ar"
                        ? (p.PrerequisiteCourse != null ? p.PrerequisiteCourse.CourseNameAr : p.PrerequisiteCourseID)
                        : (p.PrerequisiteCourse != null ? p.PrerequisiteCourse.CourseNameEn : p.PrerequisiteCourseID)
                })
                .ToListAsync();

            if (list == null || !list.Any())
            {
                return NotFound(lang == "ar" ? "لا توجد متطلبات مسجلة في النظام حالياً." : "No prerequisites registered.");
            }

            return Ok(new { success = true, data = list });
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
        [HttpPost("set-course-prerequisite")]
        public async Task<IActionResult> SetPrerequisite(string courseId, string PrerequisiteCourseID)
        {


            if (string.IsNullOrEmpty(courseId) || string.IsNullOrEmpty(PrerequisiteCourseID))
                return BadRequest("برجاء إدخال كود المادة وكود المتطلب");

            if (courseId == PrerequisiteCourseID)
                return BadRequest("المادة لا يمكن أن تكون متطلباً لنفسها");

            // 2. التأكد إن الربط ده مش موجود قبل كدة
            var exists = await _context.CoursePrerequisites
                .AnyAsync(p => p.CourseID == courseId && p.PrerequisiteCourseID == PrerequisiteCourseID);

            if (exists) return BadRequest("هذا المتطلب مسجل بالفعل لهذه المادة");

            // 3. إنشاء السجل الجديد وتخزينه
            var newPrereq = new CoursePrerequisite
            {
                CourseID = courseId,
                PrerequisiteCourseID = PrerequisiteCourseID
            };

            _context.CoursePrerequisites.Add(newPrereq);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"تم ربط المادة {courseId} بالمتطلب {PrerequisiteCourseID} بنجاح" });
        }

        [HttpDelete("remove-prerequisite/{courseId}/{PrerequisiteCourseID}")]
        public async Task<IActionResult> DeletePrereq(string courseId, string PrerequisiteCourseID)
        {
            // 1. البحث عن السجل المحدد الذي يجمع بين المادة والمتطلب الخاص بها
            // تفترض هنا أن أسماء الأعمدة في الجدول هي CourseID و PrerequisiteCourseID (عدليها حسب الموديل عندك)
            var prereq = await _context.CoursePrerequisites
                .FirstOrDefaultAsync(p => p.CourseID == courseId && p.PrerequisiteCourseID == PrerequisiteCourseID);

            // 2. إذا لم يجد هذا الربط المحدد
            if (prereq == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = $"عفواً، المادة {PrerequisiteCourseID} ليست مسجلة كمتطلب سابق للمادة {courseId}."
                });
            }

            try
            {
                // 3. تنفيذ عملية الحذف للسجل المحدد فقط
                _context.CoursePrerequisites.Remove(prereq);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Success = true,
                    Message = $"تم حذف المادة {PrerequisiteCourseID} من قائمة المتطلبات السابقة للمادة {courseId} بنجاح."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"حدث خطأ أثناء محاولة حذف المتطلب: {ex.Message}"
                });
            }
        }

        [HttpGet("preview-semester-results")]
        public async Task<IActionResult> PreviewSemesterResults(
    [FromQuery] int academicYear,
    [FromQuery] int semester,
    [FromQuery] string level) // 🌟 ضفنا الفلترة بالمستوى هنا
        {
            // 🌟 جلب كافة المسميات الممكنة والمطابقة للمستوى المطلوب
            var allowedLevels = GetPossibleLevelNames(level);

            // 1. نجيب درجات الطلاب وفلترتهم بالقائمة المرنة الجديدة
            var currentGrades = await _context.Grades
                .Include(g => g.Student)
                .Include(g => g.CourseOffering)
                .ThenInclude(o => o.Course)
                .Where(g => g.AcademicYear == academicYear
                         && g.Semester == semester
                         && g.Student != null
                         && allowedLevels.Contains(g.Student.Year)) // 🌟 المقارنة المرنة الجديدة
                .ToListAsync();

            if (!currentGrades.Any())
            {
                return BadRequest($"لا توجد درجات مرصودة لهذا الترم والمستوى ({level}) بعد.");
            }

            // 2. تجميع الـ IDs للطلاب في هذا المستوى
            var studentIds = currentGrades.Select(g => g.StudentID).Distinct().ToList();

            // 3. جلب سجل الأرشيف التاريخي لهؤلاء الطلاب
            var pastResults = await _context.StudentSemesterResults
                .Where(r => studentIds.Contains(r.StudentID))
                .ToListAsync();

            var previewList = new List<object>();

            // 4. الحسابات الذكية لكل طالب في الـ Memory
            foreach (var studentId in studentIds)
            {
                var studentGrades = currentGrades.Where(g => g.StudentID == studentId).ToList();
                var studentInfo = studentGrades.First().Student;

                // --- أ) حسابات الترم الحالي ---
                decimal totalPointsNormal = 0;
                int totalHoursNormal = 0;

                decimal totalPointsUniv = 0;
                int totalHoursUniv = 0;

                decimal totalNumericGradesSum = 0;
                decimal totalMaxGradesSum = 0;

                foreach (var grade in studentGrades)
                {
                    int hours = grade.CourseOffering?.Course?.CreditHours ?? 0;
                    decimal points = grade.GradePoints ?? 0m;
                    string category = grade.CourseOffering?.Course?.CourseCategory ?? "ENG";

                    decimal numericGrade = grade.NumericGrade ?? 0m;
                    decimal maxGrade = grade.CourseOffering?.Course?.MaxGrade ?? 100m;

                    // الحسبة العامة
                    totalPointsNormal += (points * hours);
                    totalHoursNormal += hours;

                    // إجمالي الدرجات لحساب النسبة المئوية الكلية
                    totalNumericGradesSum += numericGrade;
                    totalMaxGradesSum += maxGrade;

                    // استبعاد متطلبات الجامعة UR
                    if (category != "UR")
                    {
                        totalPointsUniv += (points * hours);
                        totalHoursUniv += hours;
                    }
                }

                decimal currentGPA = totalHoursNormal > 0 ? Math.Round(totalPointsNormal / totalHoursNormal, 2) : 0m;
                decimal currentGPAU = totalHoursUniv > 0 ? Math.Round(totalPointsUniv / totalHoursUniv, 2) : 0m;
                decimal currentSemesterPercentage = totalMaxGradesSum > 0 ? Math.Round((totalNumericGradesSum / totalMaxGradesSum) * 100, 2) : 0m;

                // --- ب) دمج الأرشيف لحساب التراكمي الشامل ---
                var studentPast = pastResults.Where(r => r.StudentID == studentId).ToList();

                decimal pastPointsNormal = studentPast.Sum(r => r.SemesterGPA * r.SemesterHours);
                int pastHoursNormal = studentPast.Sum(r => r.SemesterHours);

                decimal pastPointsUniv = studentPast.Sum(r => r.SemesterGPAU * r.SemesterHours);
                int pastHoursUniv = studentPast.Sum(r => r.SemesterHours);

                // الإجماليات التراكمية (القديم + الحالي)
                int cumulativeHoursNormal = pastHoursNormal + totalHoursNormal;
                decimal cumulativePointsNormal = pastPointsNormal + totalPointsNormal;

                int cumulativeHoursUniv = pastHoursUniv + totalHoursUniv;
                decimal cumulativePointsUniv = pastPointsUniv + totalPointsUniv;

                decimal currentCGPA = cumulativeHoursNormal > 0 ? Math.Round(cumulativePointsNormal / cumulativeHoursNormal, 2) : 0m;
                decimal currentCGPAU = cumulativeHoursUniv > 0 ? Math.Round(cumulativePointsUniv / cumulativeHoursUniv, 2) : 0m;

                // دالة تحديد التقدير العام بناءً على الـ CGPA الحالي
                string overallLetter = GetOverallLetterFromGPA(currentCGPA);

                // 5. بناء الـ Response المكتمل بالبيانات المطلوبة بالظبط
                previewList.Add(new
                {
                    studentID = studentId,
                    studentName = studentInfo?.FullName ?? "غير معروف",
                    academicYear = academicYear,
                    semester = semester,
                    level = level,
                    semesterHours = totalHoursNormal,

                    // 🌟 البيانات الكلية المطلوبة للطالب (الصورة الثانية)
                    gpa = currentGPA,
                    cgpa = currentCGPA,
                    gpau = currentGPAU,
                    cgpau = currentCGPAU,
                    totalGradePoints = cumulativePointsNormal, // إجمالي نقاط الدرجات التراكمية
                    totalPercentage = currentSemesterPercentage,  // النسبة المئوية الكلية للترم
                    overallLetter = overallLetter,             // التقدير العام الحالي

                    // 🌟 تفاصيل درجات كل مادة كاملة (الصورة الأولى)
                    coursesReviewed = studentGrades.Select(g => new
                    {
                        courseCode = g.CourseOffering?.Course?.CourseID,
                        courseName = g.CourseOffering?.Course?.CourseNameAr,
                        category = g.CourseOffering?.Course?.CourseCategory,
                        hours = g.CourseOffering?.Course?.CreditHours,

                        numericGrade = g.NumericGrade,   // الدرجة الرقمية (مثال: 29)
                        percentage = g.Percentage,       // النسبة المئوية للمادة (مثال: 29)
                        letterGrade = g.LetterGrade,     // التقدير الحرفي للمادة (مثال: F)
                        gradePoints = g.GradePoints      // نقاط المادة (مثال: 0)
                    })
                });
            }

            return Ok(new
            {
                success = true,
                message = $"تم توليد معاينة النتيجة للمستوى ({level}) بنجاح.",
                data = previewList
            });
        }

        // 🌟 ميثود مساعدة سريعة لتوليد التقدير العام بناءً على الـ CGPA
        private string GetOverallLetterFromGPA(decimal c_gpa)
        {
            if (c_gpa >= 3.75m) return "A";
            if (c_gpa >= 3.00m) return "B";
            if (c_gpa >= 2.50m) return "C";
            if (c_gpa >= 2.00m) return "D";
            return "F";
        }
        [HttpPost("publish-semester-results")]
        public async Task<IActionResult> PublishSemesterResults(
    [FromQuery] int academicYear,
    [FromQuery] int semester,
    [FromQuery] string level)
        {
            // 🌟 جلب كافة المسميات الممكنة والمطابقة للمستوى المطلوب
            var allowedLevels = GetPossibleLevelNames(level);

            // 1. الفلترة الصارمة من البداية جوة قاعدة البيانات بناءً على الـ Allowed Levels
            var currentGrades = await _context.Grades
                .Include(g => g.Student)
                .Include(g => g.CourseOffering)
                .ThenInclude(o => o.Course)
                .Where(g => g.AcademicYear == academicYear
                         && g.Semester == semester
                         && g.Student != null
                         && allowedLevels.Contains(g.Student.Year) // 🌟 المقارنة المرنة الجديدة
                         && g.IsPublished == false)
                .ToListAsync();

            if (!currentGrades.Any())
            {
                return BadRequest($"لا توجد درجات غير منشورة لهذا الترم والمستوى ({level}) أو تم نشرها بالفعل.");
            }

            // تجميع الـ IDs للطلاب
            var studentIds = currentGrades.Select(g => g.StudentID).Distinct().ToList();

            // جلب أرشيف الترمات السابقة
            var pastResults = await _context.StudentSemesterResults
                .Where(r => studentIds.Contains(r.StudentID))
                .ToListAsync();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    foreach (var studentId in studentIds)
                    {
                        var studentGrades = currentGrades.Where(g => g.StudentID == studentId).ToList();
                        var student = studentGrades.First().Student;

                        decimal localTotalPointsNormal = 0;
                        int localTotalHoursNormal = 0;
                        int localPassedHoursThisSemester = 0;

                        decimal localTotalPointsUniv = 0;
                        int localTotalHoursUniv = 0;

                        decimal localTotalNumericGradesSum = 0;
                        decimal localTotalMaxGradesSum = 0;

                        // 2. حسابات الترم الحالي
                        foreach (var grade in studentGrades)
                        {
                            int hours = grade.CourseOffering?.Course?.CreditHours ?? 0;
                            decimal points = grade.GradePoints ?? 0m;
                            string category = grade.CourseOffering?.Course?.CourseCategory ?? "ENG";

                            decimal numericGrade = grade.NumericGrade ?? 0m;
                            decimal maxGrade = grade.CourseOffering?.Course?.MaxGrade ?? 100m;

                            localTotalPointsNormal += (points * hours);
                            localTotalHoursNormal += hours;

                            localTotalNumericGradesSum += numericGrade;
                            localTotalMaxGradesSum += maxGrade;

                            // 🌟 الساعات المجتازة هي فقط المواد التي لم يرسب فيها الطالب (ليست F)
                            if (grade.LetterGrade != "F" && grade.Status != "Fail")
                            {
                                localPassedHoursThisSemester += hours;
                            }

                            if (category != "UR")
                            {
                                localTotalPointsUniv += (points * hours);
                                localTotalHoursUniv += hours;
                            }

                            // تحديث حالة المادة كـ منشورة
                            grade.IsPublished = true;
                            grade.ApprovedDate = DateTime.Now;
                        }

                        // حساب معدلات الفصلي الحالي
                        decimal currentGPA = localTotalHoursNormal > 0 ? Math.Round(localTotalPointsNormal / localTotalHoursNormal, 2) : 0m;
                        decimal currentGPAU = localTotalHoursUniv > 0 ? Math.Round(localTotalPointsUniv / localTotalHoursUniv, 2) : 0m;
                        decimal currentSemesterPercentage = localTotalMaxGradesSum > 0 ? Math.Round((localTotalNumericGradesSum / localTotalMaxGradesSum) * 100, 2) : 0m;

                        // 3. حساب التراكمي
                        var studentPast = pastResults.Where(r => r.StudentID == studentId).ToList();


                        /*  decimal pastPointsNormal = studentPast.Sum(r => r.SemesterHours * r.SemesterGPA);
                          int pastHoursNormal = studentPast.Sum(r => r.SemesterHours);


                          decimal pastPointsUniv = studentPast.Sum(r => r.SemesterHours * r.SemesterGPAU);
                        int pastHoursUniv = studentPast.Sum(r => r.SemesterHours);
                        */
                        // ترتيب الأرشيف لجلب آخر ترم مسجل للطالب بشكل صحيح
                        var lastSemesterResult = studentPast
                            .OrderBy(r => r.AcademicYear)
                            .ThenBy(r => r.Semester)
                            .LastOrDefault();

                        // الحصول على النقاط التراكمية السابقة مباشرة بدون إعادة ضرب لتجنب أي فروق حسابية
                        decimal pastPointsNormal = lastSemesterResult != null ? (lastSemesterResult.CumulativeGPA * lastSemesterResult.CumulativeHours) : 0m;
                        decimal pastPointsUniv = lastSemesterResult != null ? (lastSemesterResult.CumulativeGPAU * lastSemesterResult.CumulativeHours) : 0m;

                        int pastHoursNormal = studentPast.Sum(r => r.SemesterHours);
                        int pastHoursUniv = studentPast.Where(r => r.SemesterGPAU > 0).Sum(r => r.SemesterHours);
                        

                        int cumulativeHoursNormal = pastHoursNormal + localTotalHoursNormal;
                        decimal cumulativePointsNormal = pastPointsNormal + localTotalPointsNormal;

                        int cumulativeHoursUniv = pastHoursUniv + localTotalHoursUniv;
                        decimal cumulativePointsUniv = pastPointsUniv + localTotalPointsUniv;

                        decimal currentCGPA = cumulativeHoursNormal > 0 ? Math.Round(cumulativePointsNormal / cumulativeHoursNormal, 2) : 0m;
                        decimal currentCGPAU = cumulativeHoursUniv > 0 ? Math.Round(cumulativePointsUniv / cumulativeHoursUniv, 2) : 0m;

                        string overallLetter = GetOverallLetterFromGPA(currentCGPA);

                        // 4. فحص جدار الأمان لمنع تكرار الحساب عند إعادة النشر لنفس الترم
                        var existingTermResult = await _context.StudentSemesterResults
                            .FirstOrDefaultAsync(r => r.StudentID == studentId && r.AcademicYear == academicYear.ToString() && r.Semester == semester.ToString());

                        if (existingTermResult != null)
                        {
                            // لو السطر موجود أصلاً (إعادة نشر)، بنحذف تأثير ساعاته المجتازة السابقة من كائن الطالب أولاً عشان نمنع دبلرة الساعات
                            student.CompletedCreditHours = (student.CompletedCreditHours ?? 0) - (existingTermResult.SemesterHours);
                            _context.StudentSemesterResults.Remove(existingTermResult);
                        }
                        // 4. إنشاء سطر الأرشيف
                        var archiveResult = new StudentSemesterResult
                        {
                            StudentID = studentId,
                            AcademicYear = academicYear.ToString(),
                            Semester = semester.ToString(),
                            SemesterHours = localTotalHoursNormal,
                            CumulativeHours = cumulativeHoursNormal,
                            SemesterGPA = currentGPA,
                            CumulativeGPA = currentCGPA,
                            SemesterGPAU = currentGPAU,
                            CumulativeGPAU = currentCGPAU,
                            CreatedOn = DateTime.Now
                        };
                        _context.StudentSemesterResults.Add(archiveResult);

                        // 5. تحديث كائن الطالب
                        student.GPA = currentGPA;
                        student.CGPA = currentCGPA;
                        student.GPAU = currentGPAU;
                        student.CGPAU = currentCGPAU;
                        student.TotalGradePoints = cumulativePointsNormal;
                        student.TotalPercentage = currentSemesterPercentage;
                        student.OverallLetter = overallLetter;

                        // 1. تحديث الساعات المعتمدة المجتازة (القديمة + الجديدة الناجح فيها)
                        student.CompletedCreditHours = (student.CompletedCreditHours ?? 0) + localPassedHoursThisSemester;

                        // 2. 🌟 الـ Level Up مبيتغيرش غير في نهاية الترم التاني (Semester 2) فقط!
                        // الـ Level Up مبيتغيرش غير في نهاية الترم التاني (Semester 2) فقط بناءً على ساعات اللائحة
                        // 2. 🌟 الـ Level Up الذكي المعتمد على نمط الطالب الحالي ولائحة هندسة النظم الذكية
                        if (semester == 2)
                        {
                            // التحقق هل الطالب متسجل حالياً برقم (مثل 1، 2) أم بكلمة (مثل First، الثاني)
                            bool isNumericPattern = int.TryParse(student.Year, out _);

                            // فحص نمط النص لمعرفة هل مسجل بالعربي؛ إذا احتوى على "ال"
                            bool isArabicPattern = student.Year != null && student.Year.Contains("ال");

                            if (student.CompletedCreditHours >= 128)
                                student.Year = isNumericPattern ? "5" : (isArabicPattern ? "الخامس" : "Fifth");

                            else if (student.CompletedCreditHours >= 96)
                                student.Year = isNumericPattern ? "4" : (isArabicPattern ? "الرابع" : "Fourth");

                            else if (student.CompletedCreditHours >= 64)
                                student.Year = isNumericPattern ? "3" : (isArabicPattern ? "الثالث" : "Third");

                            else if (student.CompletedCreditHours >= 32)
                                student.Year = isNumericPattern ? "2" : (isArabicPattern ? "الثاني" : "Second");

                            else
                                student.Year = isNumericPattern ? "1" : (isArabicPattern ? "الاول" : "First");
                        }
                        // لو semester == 1 مش هيلمس الـ student.Year وهيفضل الطالب في مستواه الحالي ثابت!

                        // 3. تحريك الترم للتسجيل القادم بشكل سليم
                        if (semester == 1) student.Semester = "2";
                        else if (semester == 2) student.Semester = "1";

                        _context.Entry(student).State = EntityState.Modified;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        success = true,
                        message = $"شطورة يا روقا احسنتي يا بنتي ! تم اعتماد ونشر النتيجة للمستوى ({level}) فقط بنجاح، وتثبيت كافة المعدلات الأربعة!"
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, $"حدث خطأ أثناء اعتماد النتيجة: {ex.Message}");
                }
            }
        }
        [HttpGet("course-grades/{courseId}")]
        public async Task<IActionResult> GetGradesByCourse(string courseId, [FromQuery] int? academicYear, [FromQuery] int? semester)
        {
            // بنجيب كل الـ OfferingIDs للمادة دي عشان لو متسجلة مع كذا دكتور
            var offeringIds = await _context.CourseOfferings
                .Where(co => co.Course.CourseID == courseId)
                .Select(co => co.OfferingID)
                .ToListAsync();

            if (!offeringIds.Any())
                return NotFound("لم يتم العثور على أي عروض تدريسية لهذا الكورس.");

            var query = _context.Grades
                .Include(g => g.Student)
                .Include(g => g.CourseOffering.Course)
                .Where(g => offeringIds.Contains(g.OfferingID))
                .AsQueryable();

            // فلتر اختياري بالسنة والترم
            if (academicYear.HasValue)
                query = query.Where(g => g.AcademicYear == academicYear.Value);

            if (semester.HasValue)
                query = query.Where(g => g.Semester == semester.Value);

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
                g.Semester,
                g.IsPublished
            }).ToListAsync();

            if (!results.Any()) return NotFound("لا توجد درجات مسجلة لهذه المادة بناءً على الفلاتر المحددة.");

            return Ok(results);
        }
        [HttpGet("student-grades/{studentId}")]
        public async Task<IActionResult> GetStudentGradesByYear(int studentId, [FromQuery] int academicYear, [FromQuery] int? semester)
        {
            var query = _context.Grades
                .Include(g => g.CourseOffering.Course)
                .Where(g => g.StudentID == studentId && g.AcademicYear == academicYear)
                .AsQueryable();

            if (semester.HasValue)
                query = query.Where(g => g.Semester == semester.Value);

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
                g.AcademicYear,
                g.IsPublished
            }).ToListAsync();

            if (!grades.Any()) return NotFound("لا توجد درجات مسجلة لهذا الطالب في السنة الدراسية المحددة.");

            return Ok(grades);
        }
       [HttpGet("student-grades-history/{studentId}")]
public async Task<IActionResult> GetStudentGradesHistory(int studentId)
{
    // 1️⃣ جلب بيانات الطالب الأساسية
    var student = await _context.Students
        .FirstOrDefaultAsync(s => s.StudentID == studentId);

    if (student == null) 
        return NotFound($"لا يوجد طالب مسجل برقم {studentId}");

    // 2️⃣ جلب كل درجات الطالب من جدول الدرجات
    var allGrades = await _context.Grades
        .Include(g => g.CourseOffering.Course)
        .Where(g => g.StudentID == studentId)
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
        .Select(group => {
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

        [HttpGet("export-course-grades-csv")]
        public async Task<IActionResult> ExportCourseGradesCsv([FromQuery] string CourseID, [FromQuery] int academicYear, [FromQuery] int semester)
        {
            // 1️⃣ جلب قائمة بكل الـ OfferingIDs المتاحة للمادة دي (عشان نغطي كل الدكاترة والمجموعات)
            var offeringIds = await _context.CourseOfferings
                .Where(co => co.Course.CourseID == CourseID)
                .Select(co => co.OfferingID)
                .ToListAsync();

            if (!offeringIds.Any())
                return BadRequest("لم يتم العثور على أي عروض تدريسية لهذا الكورس في النظام.");

            // 2️⃣ جلب الدرجات بالتفصيل للطلاب المسجلين في "أي مجموعة أو دكتور" تبع المادة دي
            // 🌟 استخدمنا .Contains() عشان نجمع درجات الكورس بالكامل مهما تعددت مجموعاته
            var grades = await _context.Grades
                .Include(g => g.Student)
                .Where(g => offeringIds.Contains(g.OfferingID)
                         && g.AcademicYear == academicYear
                         && g.Semester == semester)
                .ToListAsync();

            if (!grades.Any())
                return BadRequest("لا توجد درجات أو طلاب مسجلين في هذا الكورس لهذا الترم والسنة.");

            var csv = new StringBuilder();

            // 3️⃣ بناء الهيدر المطابق لأعمدة الـ Excel
            var headerParts = new List<string>
    {
        "GradeID", "StudentID", "StudentName",
        "Attendance", "Assignments", "Quizzes",
        "Midterm", "Practical", "Oral", "Final"
    };
            csv.AppendLine(string.Join(",", headerParts));

            // 4️⃣ ملء البيانات بناءً على موديل الـ Grade الفعلي عندك بالملي
            foreach (var g in grades)
            {
                var studentName = g.Student?.FullName ?? "غير معروف";

                var row = new List<string>
        {
            g.GradeID.ToString(),
            g.StudentID.ToString(),
            studentName,

            g.AttendanceGrade?.ToString() ?? "0",
            g.AssignmentsGrade?.ToString() ?? "0",
            g.QuizzesGrade?.ToString() ?? "0",
            g.MidtermExamGrade?.ToString() ?? "0",
            g.PracticalGrade?.ToString() ?? "0",
            g.OralGrade?.ToString() ?? "0",
            g.FinalExamGrade?.ToString() ?? "0"
        };

                csv.AppendLine(string.Join(",", row));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"CourseGrades_{CourseID}_Sem{semester}_{academicYear}.csv");
        }
        [HttpPost("import-grades-csv")]
        public async Task<IActionResult> ImportGradesCsv(
            IFormFile file,
            [FromQuery] string courseId,     // 🌟 إضافة كود الكورس هنا بشكل صريح
            [FromQuery] string level,
            [FromQuery] int academicYear,
            [FromQuery] int semester)
        {
            if (file == null || file.Length == 0) return BadRequest("الملف غير موجود.");

            // جلب المسميات المقبولة للمستوى المحدّد لمنع خلط البيانات
            var allowedLevels = GetPossibleLevelNames(level);

            using var reader = new StreamReader(file.OpenReadStream());

            var headerLine = await reader.ReadLineAsync(); // قراءة السطر الأول (Headers)
            if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("الملف فارغ.");

            char separator = headerLine.Contains(';') ? ';' : ',';

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(separator).Select(v => v.Trim()).ToArray();

                if (cols.Length > 0 && int.TryParse(cols[0], out int gradeId))
                {
                    // 🌟 التعديل الصارم: تأكيد أن السجل يطابق الـ CourseID الممرر بالإضافة لباقي الشروط
                    var grade = await _context.Grades
                        .Include(g => g.CourseOffering.Course)
                        .Include(g => g.Student)
                        .FirstOrDefaultAsync(g => g.GradeID == gradeId
                                               && g.CourseOffering.CourseID == courseId // 🌟 شرط مطابقة المادة المحددة
                                               && g.AcademicYear == academicYear
                                               && g.Semester == semester
                                               && allowedLevels.Contains(g.Student.Year));

                    // إذا عثرنا على السجل وتطابقت كل الشروط بما فيها المادة، نقوم بالتحديث
                    if (grade != null)
                    {
                        // قراءة الدرجات المرفوعة بأمان
                        grade.AttendanceGrade = cols.Length > 3 && decimal.TryParse(cols[3], out decimal att) ? att : 0m;
                        grade.AssignmentsGrade = cols.Length > 4 && decimal.TryParse(cols[4], out decimal ass) ? ass : 0m;
                        grade.QuizzesGrade = cols.Length > 5 && decimal.TryParse(cols[5], out decimal qz) ? qz : 0m;
                        grade.MidtermExamGrade = cols.Length > 6 && decimal.TryParse(cols[6], out decimal mid) ? mid : 0m;
                        grade.PracticalGrade = cols.Length > 7 && decimal.TryParse(cols[7], out decimal prac) ? prac : 0m;
                        grade.OralGrade = cols.Length > 8 && decimal.TryParse(cols[8], out decimal orl) ? orl : 0m;
                        grade.FinalExamGrade = cols.Length > 9 && decimal.TryParse(cols[9], out decimal fin) ? fin : 0m;

                        // حساب المجموع الكلي للمادة
                        decimal total = (grade.AttendanceGrade ?? 0) + (grade.AssignmentsGrade ?? 0) +
                                        (grade.QuizzesGrade ?? 0) + (grade.MidtermExamGrade ?? 0) +
                                        (grade.PracticalGrade ?? 0) + (grade.OralGrade ?? 0) +
                                        (grade.FinalExamGrade ?? 0);

                        decimal maxGrade = grade.CourseOffering?.Course?.MaxGrade ?? 100m;

                        // حساب النسبة المئوية للمادة لتمريرها للمقيم الحرفي
                        decimal coursePercentage = maxGrade > 0 ? (total / maxGrade) * 100 : 0m;
                        var res = GradingEvaluator.GetResult(coursePercentage);

                        grade.NumericGrade = total;
                        grade.Percentage = Math.Round(coursePercentage, 2);
                        grade.LetterGrade = res.Letter;
                        grade.GradePoints = res.Points;

                        grade.IsPublished = false;
                        grade.Status = "Draft";
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"تم رصد وتحديث درجات مادة {courseId} للمستوى {level} بنجاح كـ مسودة (Draft)." });
        }
        [HttpGet("export-level-sheet-csv")]
        public async Task<IActionResult> ExportLevelSheetCsv(string level, int semester, int academicYear)
        {
            var allowedLevels = GetPossibleLevelNames(level);

            var students = await _context.Students
                .Where(s => allowedLevels.Contains(s.Year))
                .Include(s => s.Grades.Where(g => g.Semester == semester && g.AcademicYear == academicYear))
                    .ThenInclude(g => g.CourseOffering.Course)
                .ToListAsync();

            if (!students.Any()) return BadRequest("لا يوجد طلاب مسجلين في هذا المستوى.");

            var csv = new StringBuilder();

            // 🌟 جلب أسماء المواد باللغة الإنجليزية كما هي محددة في الموديل CourseNameEn
            var allCoursesInTerm = students.SelectMany(s => s.Grades)
                .Where(g => g.CourseOffering?.Course != null)
                .Select(g => g.CourseOffering.Course.CourseNameEn)
                .Distinct().ToList();

            var headerParts = new List<string> { "StudentID", "StudentName" };

            // 🌟 تصليح: استخدام المتغير الصحيح المعرف في لفة الـ foreach
            foreach (var courseName in allCoursesInTerm)
            {
                headerParts.Add(courseName); // تم تعديلها من CourseNameEn إلى courseName
            }

            // الفاصل الآمن للـ Backend
            headerParts.Add("LettersSection");

            // 🌟 تصليح: استخدام المتغير الصحيح المعرف في لفة الـ foreach الحرفية
            foreach (var courseName in allCoursesInTerm)
            {
                headerParts.Add($"{courseName}Letter"); // تم تعديلها من CourseNameEn إلى courseName
            }

            // المعدلات الكلية
            headerParts.AddRange(new[] { "TermPercentage", "GPA", "CGPA", "GPAU", "CGPAU", "OverallStatus" });

            csv.AppendLine(string.Join(",", headerParts));

            foreach (var s in students)
            {
                var studentName = s.FullName ?? "غير معروف";
                var row = new List<string> { s.StudentID.ToString(), studentName };

                // الدرجات الرقمية للمواد بناءً على الـ CourseNameEn
                foreach (var courseName in allCoursesInTerm)
                {
                    var grade = s.Grades.FirstOrDefault(g => g.CourseOffering?.Course?.CourseNameEn == courseName);
                    row.Add(grade?.NumericGrade?.ToString() ?? "0");
                }

                // كلمة الفاصل في الداتا
                row.Add("START");

                // التقديرات الحرفية بناءً على الـ CourseNameEn
                foreach (var courseName in allCoursesInTerm)
                {
                    var grade = s.Grades.FirstOrDefault(g => g.CourseOffering?.Course?.CourseNameEn == courseName);
                    row.Add(grade?.LetterGrade ?? "N/A");
                }

                // الحسابات الإجمالية للترم
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
            return File(bytes, "text/csv", $"ComprehensiveControlSheet_{level}_Sem{semester}_{academicYear}.csv");
        }
        [HttpPost("import-full-sheet-csv")]
        public async Task<IActionResult> ImportFullSheetCsv(
            IFormFile file,
            [FromQuery] string level, // 🌟 إضافة المستوى هنا
            [FromQuery] int academicYear,
            [FromQuery] int semester)
        {
            if (file == null || file.Length == 0)
                return BadRequest("يرجى اختيار ملف صحيح.");

            // 🌟 جلب المسميات المقبولة للمستوى المحدّد لمنع خلط البيانات
            var allowedLevels = GetPossibleLevelNames(level);

            using var reader = new StreamReader(file.OpenReadStream());
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
                return BadRequest("الملف فارغ.");

            // دعم تقسيم السطور بناءً على الفاصلة العادية أو المنقوطة الناتجة عن الـ Excel أوتوماتيكياً
            char separator = headerLine.Contains(';') ? ';' : ',';
            var headers = headerLine.Split(separator).Select(h => h.Trim()).ToList();

            int studentIdIdx = headers.IndexOf("StudentID");
            int lettersSectionIdx = headers.IndexOf("LettersSection");

            if (studentIdIdx == -1 || lettersSectionIdx == -1)
            {
                return BadRequest("الملف المرفوع لا يتطابق مع هيكلة الكنترول المعتمدة (تأكد من وجود StudentID و LettersSection).");
            }

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = line.Split(separator).Select(v => v.Trim()).ToArray();

                if (values.Length <= studentIdIdx || values.Length <= lettersSectionIdx) continue;

                if (int.TryParse(values[studentIdIdx], out int studentId))
                {
                    // 🌟 تعديل الكويري: التأكد من أن الطالب ينتمي للمستوى المحدّد (allowedLevels) لحماية البيانات
                    var studentGrades = await _context.Grades
                        .Include(g => g.CourseOffering.Course)
                        .Include(g => g.Student) // عمل Include للـ Student للتحقق من مستواه
                        .Where(g => g.StudentID == studentId
                                 && g.AcademicYear == academicYear
                                 && g.Semester == semester
                                 && allowedLevels.Contains(g.Student.Year)) // 🌟 شرط الأمان للمستوى
                        .ToListAsync();

                    // إذا لم نجد درجات للطالب في هذا الترم وهذا المستوى تحديداً، نتخطى السطر
                    if (!studentGrades.Any()) continue;

                    // قراءة المواد الإنجليزية خطوة بخطوة
                    for (int i = 2; i < lettersSectionIdx; i++)
                    {
                        if (i >= values.Length) break;

                        string courseName = headers[i]; // الاسم بالإنجليزي من الهيدر
                        string gradeValue = values[i];

                        if (gradeValue != "N/A" && !string.IsNullOrWhiteSpace(gradeValue))
                        {
                            var gradeRecord = studentGrades.FirstOrDefault(g => g.CourseOffering?.Course?.CourseNameEn == courseName);

                            if (gradeRecord != null && decimal.TryParse(gradeValue, out decimal newNumericGrade))
                            {
                                decimal maxGradeValue = gradeRecord.CourseOffering?.Course?.MaxGrade ?? 100m;
                                decimal coursePercentage = maxGradeValue > 0 ? (newNumericGrade / maxGradeValue) * 100 : 0m;

                                var eval = GradingEvaluator.GetResult(coursePercentage);

                                gradeRecord.NumericGrade = newNumericGrade;
                                gradeRecord.Percentage = Math.Round(coursePercentage, 2);
                                gradeRecord.LetterGrade = eval.Letter;
                                gradeRecord.GradePoints = eval.Points;

                                gradeRecord.Status = "Draft";
                                gradeRecord.IsPublished = false;
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"تم رفع شيت المواد للمستوى {level} بنجاح وحساب التقديرات لجميع الطلاب بأمان." });
        }
        [HttpGet("instructors-workload-report")]
        public async Task<IActionResult> GetInstructorsWorkload()
        {
            var report = await _context.SystemUsers
                .Where(u => u.Role == "Instructor")
                .Select(u => new
                {
                    instructorID = u.UserID,
                    instructorName = u.FullName,
                    email = u.Email,
                    teachingDetails = _context.CourseOfferings
                        .Where(co => co.InstructorID == u.UserID)
                        .Select(co => new {
                            courseID = co.CourseID,
                            courseName = co.Course.CourseNameAr,
                            semester = co.Semester,
                            studentCount = _context.CourseRegistrations
                                .Count(r => r.OfferingID == co.OfferingID)
                        }).ToList(),
                    totalStudentsOverall = _context.CourseRegistrations
                        .Count(r => r.CourseOffering.InstructorID == u.UserID)
                })
                .ToListAsync();

            if (!report.Any()) return NotFound("لا يوجد محاضرين مسجلين حالياً.");
            return Ok(report);
        }

        private string GetOverallLetter(decimal gpa)
        {
            if (gpa >= 3.4m) return "امتياز";
            if (gpa >= 2.8m) return "جيد جداً";
            if (gpa >= 2.4m) return "جيد";
            if (gpa >= 2.0m) return "مقبول";
            return "راسب";
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




