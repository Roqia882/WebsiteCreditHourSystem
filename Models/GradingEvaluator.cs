namespace WebsiteCreditHoursSystem.Models
{
    public class GradingEvaluator
    {
            public static (string Letter, decimal Points) GetResult(decimal score)
            {
                if (score >= 90) return ("A", 4.0m);
                if (score >= 85) return ("-A", 3.7m);
                if (score >= 80) return ("+B", 3.3m);
                if (score >= 75) return ("B", 3.0m);
                if (score >= 70) return ("-B", 2.7m);
                if (score >= 65) return ("+C", 2.3m);
                if (score >= 60) return ("C", 2.0m);
                if (score >= 55) return ("-C", 1.7m);
                if (score >= 50) return ("D", 1.3m);
                return ("F", 0.0m); // راسب
            }
        }
    }

