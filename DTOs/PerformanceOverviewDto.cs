namespace WebsiteCreditHoursSystem.DTOs
{
    public class PerformanceOverviewDto
    {
        public string Title { get; set; } = "Semester Performance Overview";
        public string Subtitle { get; set; } = "Aggregated student engagement levels across modules";
        public string LegendLabel { get; set; } = "ENGAGEMENT TREND";
        public List<PerformanceData> Data { get; set; } = new List<PerformanceData>();
    }

    // الكلاس الصغير اللي بيمثل كل عمود (Bar) في الرسم البياني
    public class PerformanceData
    {
        public string Label { get; set; } // PHASE 01, PHASE 02...
        public double Percentage { get; set; } // القيمة زي 92
        public bool IsCurrent { get; set; } // عشان يظهر باللون الأسود في الفلتر
    }
}

