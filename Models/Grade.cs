using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebsiteCreditHoursSystem.Models
{
    [Table("Grades")]
    public class Grade
    {
        [Key]
        public int GradeID { get; set; }
        public int StudentID { get; set; }

        [ForeignKey("StudentID")]
        public virtual Student Student { get; set; }

        public int OfferingID { get; set; }

        [ForeignKey("OfferingID")]
        public virtual CourseOffering CourseOffering { get; set; }

        public decimal? AttendanceGrade { get; set; }
        public decimal? AssignmentsGrade { get; set; }
        public decimal? QuizzesGrade { get; set; }
        public decimal? MidtermExamGrade { get; set; }
        public decimal? PracticalGrade { get; set; }
        public decimal? FinalExamGrade { get; set; }
        public decimal? OralGrade { get; set; }

        public decimal? NumericGrade { get; set; }
        public decimal? Percentage { get; set; }
        public string? LetterGrade { get; set; }
        public decimal? GradePoints { get; set; }
        public string? Status { get; set; } = "Draft";

        public int AcademicYear { get; set; }
        public int Semester { get; set; }
        public bool IsPublished { get; set; } = false;
        public DateTime? ApprovedDate { get; set; }

        // 🌟 الميثود الذكية لحساب التقديرات أوتوماتيكياً بناءً على مجموع المادة 🌟
        public void CalculateFinalResults(decimal maxCourseGrade)
        {
            // أمان: لو درجة المادة مش محددة أو بـ 0 بنثبتها على 100 منعاً للقسمة على صفر
            if (maxCourseGrade <= 0) maxCourseGrade = 100.00m;

            // 1️⃣ جمع كل الدرجات الفردية اللي الدكتور مدخلها
            decimal totalEarned = (AttendanceGrade ?? 0) +
                                  (AssignmentsGrade ?? 0) +
                                  (QuizzesGrade ?? 0) +
                                  (MidtermExamGrade ?? 0) +
                                  (PracticalGrade ?? 0) +
                                  (FinalExamGrade ?? 0) +
                                  (OralGrade ?? 0);

            NumericGrade = totalEarned;

            // 2️⃣ حساب النسبة المئوية الدقيقة للمادة
            Percentage = (totalEarned / maxCourseGrade) * 100;

            // 3️⃣ تحديد التقدير الحرفي والنقاط بناءً على لائحة الكلية
            if (Percentage >= 95) { LetterGrade = "A+"; GradePoints = 4.00m; Status = "Pass"; }
            else if (Percentage >= 90) { LetterGrade = "A"; GradePoints = 3.75m; Status = "Pass"; }
            else if (Percentage >= 85) { LetterGrade = "B+"; GradePoints = 3.50m; Status = "Pass"; }
            else if (Percentage >= 80) { LetterGrade = "B"; GradePoints = 3.00m; Status = "Pass"; }
            else if (Percentage >= 75) { LetterGrade = "C+"; GradePoints = 2.50m; Status = "Pass"; }
            else if (Percentage >= 70) { LetterGrade = "C"; GradePoints = 2.00m; Status = "Pass"; }
            else if (Percentage >= 65) { LetterGrade = "D+"; GradePoints = 1.50m; Status = "Pass"; }
            else if (Percentage >= 60) { LetterGrade = "D"; GradePoints = 1.00m; Status = "Pass"; }
            else { LetterGrade = "F"; GradePoints = 0.00m; Status = "Fail"; }
        }
    }
}