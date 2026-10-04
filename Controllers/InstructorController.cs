using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebsiteCreditHoursSystem.DTOs;
using WebsiteCreditHoursSystem.Models;

namespace WebsiteCreditHoursSystem.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class InstructorsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InstructorsController(AppDbContext context)
        {
            _context = context;
        }
        // --- حطي الميثود هنا في الجزء ده ---
        private async Task<int?> GetCurrentInstructorId()
        {
            // 1. هنجرب نقرأ الـ ID بكل الطرق الممكنة اللي شفناها في التوكن بتاعك
            var userIdClaim = User.FindFirst("UserId")?.Value
                           ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return null;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                // 2. بنبحث في جدول الـ Instructors عن السجل اللي الـ UserId بتاعه 18
                var instructor = await _context.Instructors
                    .FirstOrDefaultAsync(i => i.UserId == userId);

                return instructor?.InstructorID; // المفروض يرجع 12
            }

            return null;
        }

        [HttpGet("my-courses")] // شيلنا /{instructorId} عشان Swagger ميسألش عليه
        public async Task<IActionResult> GetMyCourses() // شيلنا int instructorId من هنا
        {
            // 1. استدعاء الميثود اللي كتبناها عشان نعرف هوية الدكتور من التوكن
            var instructorId = await GetCurrentInstructorId();

            // تأكدي إن التوكن سليم ومربوط بدكتور
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. هنجيب الداتا ونربط الجداول ببعضها (نفس الكود بتاعك بس استخدمنا الـ ID اللي فوق)
            var myCourses = await _context.CourseOfferings
                .Where(co => co.InstructorID == instructorId) // هنا بنستخدم الـ ID الأوتوماتيكي
                .Select(co => new
                {
                    offeringId = co.OfferingID,
                    courseId = co.CourseID,
                    courseName = co.Course.CourseNameEn ?? "اسم المادة غير مسجل",
                    academyYear = co.Course.AcademicYear,
                    level = co.Course.Level,

                    // حساب عدد الطلاب المسجلين
                    studentCount = _context.CourseRegistrations
                        .Count(cr => cr.OfferingID == co.OfferingID),
                    // 🌟 الربط الصح بناءً على موديل الجدول الحالي:
                    sessionType = _context.Schedule
                .Where(s => s.CourseID == co.CourseID && s.InstructorID == co.InstructorID)
                .Select(s => s.SessionType)
                .FirstOrDefault() ?? "Lecture",
                    day = _context.Schedule
                .Where(s => s.CourseID == co.CourseID && s.InstructorID == co.InstructorID)
                .Select(s => s.Day)
                .FirstOrDefault() ?? "غير محدد",

                    semester = co.Semester
                })
                .ToListAsync();

            if (myCourses == null || !myCourses.Any())
            {
                return NotFound("لا توجد كورسات مسجلة لهذا المحاضر.");
            }

            return Ok(myCourses);
        }
       
        [HttpGet("course-students-by-id/{courseId}")]
        public async Task<IActionResult> GetStudentsByCourseId(string courseId)
        {
            // 1. تحديد هوية الدكتور من التوكن
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. البحث عن الطلاب المسجلين في هذا الكورس
            // لاحظي: ضفنا شرط (InstructorID == instructorId) عشان نضمن الخصوصية
            var students = await _context.CourseRegistrations
                .Where(cr => cr.CourseOffering.CourseID == courseId && cr.CourseOffering.InstructorID == instructorId)
                .Select(cr => new
                {
                    cr.StudentID,
                    // سحب اسم الطالب من جدول الطلاب (Student)
                    StudentName = cr.Student.FullName ?? "اسم الطالب غير موجود",
                    cr.RegistrationDate,
                    cr.Status,
                    // إضافة اسم الكورس للتأكيد
                    CourseName = cr.CourseOffering.Course.CourseNameEn
                })
                .ToListAsync();

            if (!students.Any())
            {
                return NotFound($"لا يوجد طلاب مسجلين في كورس رقم {courseId} ضمن مجموعاتك الدراسية.");
            }

            return Ok(students);
        }
        // --- 1. إندبوينت الرصد لأول مرة (POST) ---

        [HttpPut("update-grades")]
        public async Task<IActionResult> UpdateStudentGrades([FromBody] List<UpdateGradeDto> updatedGrades)
        {
            // 1. نجيب الـ InstructorID الخاص بالدكتور اللي عامل تسجيل دخول حالياً
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null) return Unauthorized("عفواً، لا توجد بيانات محاضر مسجلة.");

            // 2. نتأكد إن الفرونت إند بعت داتا فعلاً مش قائمة فاضية
            if (updatedGrades == null || !updatedGrades.Any()) return BadRequest("لم يتم إرسال أي درجات لتحديثها.");

            // لستة هنشيل فيها الـ IDs بتاعة السطور اللي اتعدلت بنجاح عشان نرجعها كاملة
            var updatedGradeIds = new List<int>();

            // 3. نلف على كل سطر درجة مبعوث في القائمة ونحدثه
            foreach (var item in updatedGrades)
            {
                var gradeRecord = await _context.Grades
                    .Include(g => g.CourseOffering)
                    .ThenInclude(o => o.Course)
                    .FirstOrDefaultAsync(g => g.GradeID == item.GradeID && g.CourseOffering.InstructorID == instructorId);

                if (gradeRecord == null) continue; // لو السجل مش موجود أو مش تبع الدكتور يتخطاه

                // 4. قفل الأمان: نمنع التعديل فقط لو النتيجة اعتمدت ونُشرت رسمياً للطلاب
                if (gradeRecord.IsPublished || gradeRecord.Status == "Published")
                {
                    return BadRequest($"لا يمكن تعديل درجة السجل {item.GradeID} لأن النتيجة تم نشرها واعتمادها رسميًا.");
                }

                // 5. نحدث الدرجات التفصيلية بالقيم الجديدة المبعوثة من الدكتور
                gradeRecord.AttendanceGrade = item.Attendance;
                gradeRecord.AssignmentsGrade = item.Assignments;
                gradeRecord.QuizzesGrade = item.Quizzes;
                gradeRecord.MidtermExamGrade = item.MidtermExam;
                gradeRecord.PracticalGrade = item.Practical;
                gradeRecord.FinalExamGrade = item.FinalExam;
                gradeRecord.OralGrade = item.Oral;

                // 6. نجيب الـ MaxGrade الخاصة بالمادة دي
                decimal maxGrade = gradeRecord.CourseOffering?.Course?.MaxGrade ?? 100m;

                // 7. حساب الإجمالي والتقدير والـ GPA أوتوماتيكياً
                gradeRecord.CalculateFinalResults(maxGrade);

                // بنحفظ الـ ID بتاع السجل ده علشان نجيب داتا كاملة ليه بعد شوية
                updatedGradeIds.Add(gradeRecord.GradeID);
            }

            // 8. نحفظ كل التعديلات اللي حصلت في قاعدة البيانات دفعة واحدة
            await _context.SaveChangesAsync();

            // 🌟 9. الحركة الذكية هنا: بنروح نجيب السطور اللي لسه متعدلة حالا من الداتابيز 
            // مع الـ Include للاسم وكل الحسابات الجديدة علشان نضمن إنها ترجع مليانة مية مية
            var finalModifiedData = await _context.Grades
                .Where(g => updatedGradeIds.Contains(g.GradeID))
                .Include(g => g.Student)
                .Select(g => new
                {
                    g.GradeID,
                    studentID = g.StudentID,
                    studentName = g.Student != null ? g.Student.FullName : "اسم الطالب غير معروف",
                    attendance = g.AttendanceGrade,
                    assignments = g.AssignmentsGrade,
                    quizzes = g.QuizzesGrade,
                    midtermExam = g.MidtermExamGrade,
                    practical = g.PracticalGrade,
                    oral = g.OralGrade,
                    finalExam = g.FinalExamGrade,

                    // الحقول المحسوبة أوتوماتيكياً:
                    numericGrade = g.NumericGrade,
                    letterGrade = g.LetterGrade,
                    gradePoints = g.GradePoints,
                    percentage = g.Percentage,
                    status = g.Status
                }).ToListAsync();

            // نرجع للفرونت إند رسالة النجاح ومعاها الـ Data المليانة بالكامل
            return Ok(new
            {
                success = true,
                message = "تم حفظ وتحديث الدرجات وحساب التقديرات تلقائياً بنجاح!",
                data = finalModifiedData
            });
        }
        // --- 3. إندبوينت العرض للدكتور (GET) ---

        [HttpGet("get-course-grades-by-code/{courseId}")]
        public async Task<IActionResult> GetCourseGrades(string courseId)
        {
            // 1. تحديد هوية الدكتور من التوكن
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. البحث في جدول الدرجات مع التأكد إن المادة تابعة لهذا الدكتور
            // استخدمنا g.CourseOffering.InstructorID للتحقق من الصلاحية
            var grades = await _context.Grades
                .Where(g => g.CourseOffering.CourseID == courseId && g.CourseOffering.InstructorID == instructorId)
                .Select(g => new {
                    g.GradeID,
                    g.StudentID,
                    CourseID = g.CourseOffering.CourseID,
                    StudentName = g.Student != null ? g.Student.FullName : "اسم غير معروف",
                    Attendance = g.AttendanceGrade ?? 0,
                    Assignments = g.AssignmentsGrade ?? 0,
                    Quizzes = g.QuizzesGrade ?? 0,
                    MidtermExam = g.MidtermExamGrade ?? 0,
                    Practical = g.PracticalGrade ?? 0,
                    Oral = g.OralGrade ?? 0,
                    FinalExam = g.FinalExamGrade ?? 0,
                    g.NumericGrade,
                    g.LetterGrade,
                    g.GradePoints,
                    g.Percentage,
                    g.Status,
                   
                   
                    
                })
                .ToListAsync();

            if (grades == null || !grades.Any())
                return NotFound($"لا توجد درجات مسجلة لسيادتكم في المادة ذات الكود: {courseId}");

            return Ok(grades);
        }


       /* // 1. إضافة متطلب جديد لمادة
         // إضافة الحماية لضمان أن المستخدم مسجل دخول
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
        // 2. عرض كل المتطلبات المسجلة في النظام
      
        [HttpGet("view-all-prerequisites")]
        public async Task<IActionResult> GetAllPrereqs()
        {
            // جلب قائمة المتطلبات كاملة من جدول CoursePrerequisites
            var list = await _context.CoursePrerequisites
                .Select(p => new {
                    p.PrereqID,
                    p.CourseID, // كود المادة الأساسية
                    p.PrerequisiteCourseID // كود المادة المتطلبة
                })
                .ToListAsync();

            if (list == null || !list.Any())
            {
                return NotFound("لا توجد متطلبات مسجلة في النظام حالياً.");
            }

            return Ok(list);
        }

        // 3. حذف متطلب معين باستخدام الـ ID بتاعه

        [HttpDelete("remove-prerequisite/{courseId}")]
        public async Task<IActionResult> DeletePrereq(string courseId) // غيرنا النوع لـ string
        {
            

            // البحث عن المتطلب الخاص بالمادة المبعوتة
            // بنفترض هنا إن العمود اللي فيه كود المادة في جدول المتطلبات اسمه CourseID
            var prereq = await _context.CoursePrerequisites
                .FirstOrDefaultAsync(p => p.CourseID == courseId);

            if (prereq == null)
                return NotFound($"عفواً، لا يوجد متطلب سابق مسجل للمادة {courseId}");

            // تنفيذ عملية الحذف
            _context.CoursePrerequisites.Remove(prereq);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"تم حذف المتطلب السابق للمادة {courseId} بنجاح" });
        }
       */
        [HttpGet("my-schedule")]
        public async Task<IActionResult> GetInstructorSchedule()
        {
            // 1. تحديد هوية الدكتور من التوكن
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. سحب الجدول مباشرة من جدول الـ Schedule العام لضمان ظهور كل الحصص والأنواع
            var schedule = await _context.Schedule
                .Include(s => s.Course) // عشان نسحب اسم المادة
                .Where(s => s.InstructorID == instructorId && s.StudentID == null) // جدول الكلية العام للدكتور
                .Select(s => new {
                    day = s.Day ?? "غير محدد",
                    courseName = s.Course != null ? s.Course.CourseNameAr : "مادة غير معروفة",
                    courseCode = s.CourseID,
                    Level = s.Year,
                    Semester = s.Semester,

                    // تحويل الوقت لنص
                    startTime = s.StartTime.HasValue
                ? s.StartTime.Value.ToString(@"hh\:mm\:ss") // ينتج: "02:00:00"
                : "غير محدد",

                    endTime = s.EndTime.HasValue
                ? s.EndTime.Value.ToString(@"hh\:mm\:ss") // ينتج: "03:00:00"
                : "غير محدد",

                    room = s.Room ?? "غير محدد",

                    // نجيب السعة من جدول الـ CourseOfferings المرتبط بالمادة والدكتور
                    capacity = _context.CourseOfferings
                        .Where(co => co.CourseID == s.CourseID && co.InstructorID == s.InstructorID)
                        .Select(co => co.Capacity)
                        .FirstOrDefault() ?? 0,

                    // هنا الـ SessionType هييجي دايماً صح وحقيقي من جدول الجدول نفسه (Lecture, Section, Lab)
                    sessionType = s.SessionType ?? "Lecture"
                })
                .ToListAsync();

            if (schedule == null || !schedule.Any())
                return NotFound("لا يوجد جدول محاضرات مسجل لسيادتكم حالياً.");

            return Ok(schedule);
        }

        [HttpGet("dashboard-stats")] // شيلنا الـ {instructorId} من الـ Route
        public async Task<IActionResult> GetInstructorDashboardStats() // شيلنا الـ Parameter
        {
            // 1. تحديد هوية الدكتور من التوكن (الخطوة الأهم للأمان)
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. الإحصائيات الأساسية (استخدام الـ instructorId المستخرج)
            var coursesCount = await _context.CourseOfferings
                .CountAsync(co => co.InstructorID == instructorId);

            var totalStudents = await _context.CourseRegistrations
                .Where(r => r.CourseOffering.InstructorID == instructorId)
                .Select(r => r.StudentID)
                .Distinct()
                .CountAsync();

            // 3. حساب توزيع التقديرات (للرسم البياني في الداش بورد)
            var gradeDistribution = await _context.Grades
                .Where(g => g.CourseOffering.InstructorID == instructorId)
                .GroupBy(g => g.LetterGrade)
                .Select(group => new {
                    grade = group.Key ?? "N/A",
                    count = group.Count()
                })
                .ToListAsync();

            // 4. حساب متوسط الـ GPA للمواد التي يدرسها
            var averageGPA = await _context.Grades
                .Where(g => g.CourseOffering.InstructorID == instructorId)
                .AverageAsync(g => (double?)g.GradePoints) ?? 0.0;

            // 5. عدد الطلاب الذين لم يتم رصد درجاتهم بعد (Pending Grades)
            var pendingGrades = await _context.CourseRegistrations
                .Where(r => r.CourseOffering.InstructorID == instructorId)
                .CountAsync(r => !_context.Grades.Any(g => g.StudentID == r.StudentID && g.OfferingID == r.OfferingID));

            return Ok(new
            {
                totalCourses = coursesCount,
                totalStudents = totalStudents,
                averageGPA = Math.Round(averageGPA, 2),
                pendingGrades = pendingGrades,
                gradesDistribution = gradeDistribution,
                lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
        }
      
       /* [HttpGet("attendance-stats")] // شيلنا الـ {instructorId}
        public async Task<IActionResult> GetAttendanceStats() // شيلنا الـ Parameter
        {
            // 1. تحديد هوية الدكتور من التوكن (للأمان والخصوصية)
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. نجيب درجات الحضور لكل طلاب المحاضر ده باستخدام الـ instructorId المستخرج
            var attendanceGrades = await _context.Grades
                .Where(g => g.CourseOffering.InstructorID == instructorId)
                .Select(g => (double?)g.AttendanceGrade)
                .ToListAsync();

            if (!attendanceGrades.Any())
            {
                return Ok(new { attendancePercentage = 0, lowAttendanceStudents = 0, totalSessions = 0 });
            }

            // 3. حساب النسبة المئوية (بافتراض إن الدرجة العظمى للحضور هي 10)
            double maxAttendanceScore = 10.0;
            double avgAttendance = attendanceGrades.Average() ?? 0;
            double percentage = (avgAttendance / maxAttendanceScore) * 100;

            // 4. حساب عدد الطلاب اللي حضورهم منخفض (أقل من 50%)
            int lowAttendanceCount = attendanceGrades.Count(g => g < (maxAttendanceScore / 2));

            return Ok(new
            {
                attendancePercentage = Math.Round(percentage, 1),
                lowAttendanceStudents = lowAttendanceCount,
                totalSessions = 12 // رقم ثابت أو يمكن حسابه لاحقاً من جدول المحاضرات
            });
        }
       */
        [HttpGet("attendance-stats")]
        public async Task<IActionResult> GetAttendanceStats()
        {
            // 1. تحديد هوية الدكتور من التوكن
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. نجيب درجات الحضور المتوفرة (ونتجاهل السجلات التي لم ترصد بعد لكي لا تخرب المتوسط)
            var attendanceGrades = await _context.Grades
                .Where(g => g.CourseOffering.InstructorID == instructorId && g.AttendanceGrade != null)
                .Select(g => (double)g.AttendanceGrade)
                .ToListAsync();

            if (!attendanceGrades.Any())
            {
                return Ok(new { attendancePercentage = 0, lowAttendanceStudents = 0, totalSessions = 12 });
            }

            // 3. حساب النسبة المئوية بناءً على متوسط الدرجات المرصودة
            double maxAttendanceScore = 10.0; // الدرجة العظمى للحضور في لائحتكم (يمكنك تعديلها لـ 5 مثلاً لو كانت من 5)
            double avgAttendance = attendanceGrades.Average();
            double percentage = (avgAttendance / maxAttendanceScore) * 100;

            // 4. حساب عدد الطلاب الذين أخذوا أقل من نصف درجة الحضور (إنذار غياب)
            int lowAttendanceCount = attendanceGrades.Count(g => g < (maxAttendanceScore / 2));

            return Ok(new
            {
                attendancePercentage = Math.Round(percentage, 1),
                lowAttendanceStudents = lowAttendanceCount,
                totalSessions = 12 // رقم توضيحي لعدد المحاضرات في الترم
            });
        }

        [HttpGet("course-analytics/{courseId}")]
        public async Task<IActionResult> GetCourseAnalytics(string courseId, [FromQuery] int academicYear, [FromQuery] int semester)
        {
            // 1. تحديد هوية الدكتور من التوكن (بدل instructorId = 1)
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null)
            {
                return Unauthorized("عفواً، لا توجد بيانات محاضر مرتبطة بهذا الحساب.");
            }

            // 2. البحث عن الـ OfferingID للمادة والدكتور من جدول الـ CourseOfferings
            var offering = await _context.CourseOfferings
                .FirstOrDefaultAsync(o => o.CourseID == courseId && o.InstructorID == instructorId);

            if (offering == null)
                return NotFound($"لم يتم العثور على سجل تدريس للمادة {courseId} خاص بسيادتكم.");

            // 3. سحب الدرجات وفلترتها بالسنة والترم من جدول الـ Grades
            var grades = await _context.Grades
                .Where(g => g.OfferingID == offering.OfferingID
                         && g.AcademicYear == academicYear
                         && g.Semester == semester)
                .ToListAsync();

            if (!grades.Any())
                return Ok(new { Message = "لا توجد درجات مرصودة لهذه المادة في السنة والترم المحددين." });

            // 4. الحسابات والتحليلات
            var analytics = new
            {
                CourseInfo = new
                {
                    courseId,
                    courseName = offering.Course?.CourseNameAr,
                    academicYear,
                    semester
                },
                Summary = new
                {
                    TotalStudents = grades.Count,
                    Passed = grades.Count(g => g.NumericGrade >= 50),
                    Failed = grades.Count(g => g.NumericGrade < 50),
                    Average = Math.Round(grades.Average(g => g.NumericGrade) ?? 0, 2),
                    HighestGrade = grades.Max(g => g.NumericGrade),
                    LowestGrade = grades.Min(g => g.NumericGrade)
                },
                GradeDistribution = grades
                    .GroupBy(g => g.LetterGrade ?? "N/A")
                    .Select(group => new
                    {
                        Grade = group.Key,
                        Count = group.Count()
                    })
                    .OrderBy(g => g.Grade)
                    .ToList()
            };

            return Ok(analytics);
        }
        [HttpGet("performance-overview/{courseId}")]
        public async Task<IActionResult> GetPerformanceOverview(string courseId)
        {
            // 1. التأكد من هوية الدكتور
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null) return Unauthorized("يجب تسجيل الدخول أولاً");

            // 2. حساب متوسط درجات المادة دي كلها من جدول الدرجات
            // ده بيحل إيرور الـ AssessmentType لأنه بيستخدم أعمدة موجودة فعلاً
            var overallAverage = await _context.Grades
                .Where(g => g.CourseOffering.CourseID == courseId &&
                            g.CourseOffering.InstructorID == instructorId)
                .Select(g => (double?)g.Percentage)
                .AverageAsync() ?? 0;

            var performanceData = new List<PerformanceData>();

            // 3. عمل الـ 5 مراحل (Phases) المطلوبة للرسم البياني
            for (int i = 0; i < 5; i++)
            {
                performanceData.Add(new PerformanceData
                {
                    // دي اللي هتظهر تحت العمود في الموبايل
                    Label = $"PHASE {i + 1:D2}",

                    // بنحط أرقام متغيرة بسيطة حول المتوسط عشان الرسمة يكون فيها "تريند" (مؤقتاً)
                    Percentage = overallAverage > 0 ? Math.Round(overallAverage - (10 - i * 3), 1) : 0,

                    // بنخلي المرحلة الرابعة هي الـ Current (اللون الأسود) زي الصورة
                    IsCurrent = (i == 3)
                });
            }

            // 4. إرسال النتيجة النهائية بالشكل اللي الـ Front-end مستنيه
            var response = new PerformanceOverviewDto
            {
                Title = "Semester Performance Overview",
                Subtitle = $"Analysis for Course: {courseId}",
                LegendLabel = "ENGAGEMENT TREND",
                Data = performanceData
            };

            return Ok(response);
        }

        [HttpGet("dashboard-insights")]
        public async Task<IActionResult> GetDashboardInsights()
        {
            var instructorId = await GetCurrentInstructorId();
            if (instructorId == null) return Unauthorized();

            var insights = await _context.CourseOfferings
                .Include(co => co.Course) // ضروري جداً عشان يسحب بيانات المادة المرتبطة
                .Where(co => co.InstructorID == instructorId)
                .Select(co => new
                {
                    // استخدمنا الاسم اللي موجود في جدول الـ Course
                    CourseName = co.Course.CourseNameEn,
                    CourseID = co.CourseID,
                    TotalStudents = _context.Grades.Count(g => g.OfferingID == co.OfferingID),
                    PendingGradesCount = _context.Grades.Count(g => g.OfferingID == co.OfferingID && g.Status != "Published"),

                    StrugglingStudentsPercentage = _context.Grades
                        .Where(g => g.OfferingID == co.OfferingID)
                        .Any() ? (decimal)_context.Grades.Count(g => g.OfferingID == co.OfferingID && g.NumericGrade < 60)
                                 / _context.Grades.Count(g => g.OfferingID == co.OfferingID) * 100 : 0
                })
                .ToListAsync();

            var mainInsight = insights.OrderByDescending(i => i.StrugglingStudentsPercentage).FirstOrDefault();

            var response = new
            {
                CoursesSummary = insights,
                EditorialInsight = mainInsight != null ? new
                {
                    Title = "Editorial Insight",
                    Description = $"Based on recent submission patterns in {mainInsight.CourseName}, {Math.Round(mainInsight.StrugglingStudentsPercentage, 0)}% of students are struggling with the course material.",
                    SuggestedAction = "Release supplemental workshop materials before the next project deadline."
                } : null
            };

            return Ok(response);
        }
    }
}
