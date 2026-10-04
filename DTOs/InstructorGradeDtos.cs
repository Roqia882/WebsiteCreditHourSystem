namespace WebsiteCreditHoursSystem.DTOs
{
    public class InstructorGradeDtos
    {
            public int GradeID { get; set; }
            public int StudentID { get; set; }
            public string StudentNameAr { get; set; }
            public string StudentNameEn { get; set; }
            public decimal? AttendanceGrade { get; set; }
            public decimal? AssignmentsGrade { get; set; }
            public decimal? QuizzesGrade { get; set; }
            public decimal? MidtermExamGrade { get; set; }
            public decimal? PracticalGrade { get; set; }
            public decimal? FinalExamGrade { get; set; }
            public decimal? OralGrade { get; set; }
            public decimal? NumericGrade { get; set; }
            public string LetterGrade { get; set; }
            public string Status { get; set; }
        }

        // 2. لاستقبال الدرجات اللي الدكتور هيعدلها من الفرونت إند
        public class UpdateGradeDto
        {
       
            public int GradeID { get; set; }
            public decimal? Attendance { get; set; }
            public decimal? Assignments { get; set; }
            public decimal? Quizzes { get; set; }
            public decimal? MidtermExam { get; set; }
            public decimal? Practical { get; set; }
            public decimal? Oral { get; set; }
            public decimal? FinalExam { get; set; }
        }
    }


