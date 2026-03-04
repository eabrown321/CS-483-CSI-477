using AdvisorDb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Data;

namespace CS_483_CSI_477.Pages
{
    public class AdminAnalyticsModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;

        // Analytics Data
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int TotalCourses { get; set; }
        public decimal AverageGpa { get; set; }

        public DataTable? EnrollmentByMajor { get; set; }
        public DataTable? PopularCourses { get; set; }
        public DataTable? GpaByMajor { get; set; }
        public DataTable? CompletionRates { get; set; }
        public int TotalChatMessages { get; set; }
        public int ActiveChatUsers { get; set; }
        public DataTable? ChatUsageByDay { get; set; }
        public int TotalBulletins { get; set; }
        public int TotalSupportingDocs { get; set; }
        public DataTable? RecentUploads { get; set; }

        public AdminAnalyticsModel(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        public IActionResult OnGet()
        {
            if (!HttpContext.Session.GetInt32("AdminID").HasValue)
                return RedirectToPage("/Login");

            string role = HttpContext.Session.GetString("Role") ?? "";
            if (role != "Admin")
                return RedirectToPage("/StudentDashboard");

            LoadAnalytics();
            return Page();
        }

        private void LoadAnalytics()
        {
            LoadStudentStatistics();
            LoadEnrollmentByMajor();
            LoadPopularCourses();
            LoadGpaByMajor();
            LoadCompletionRates();
            LoadChatUsageAnalytics();
            LoadFileUploadStatistics();
        }

        private void LoadStudentStatistics()
        {
            var query = @"
                SELECT 
                    COUNT(*) as Total,
                    SUM(CASE WHEN EnrollmentStatus IN ('Full-Time', 'Part-Time') THEN 1 ELSE 0 END) as Active,
                    AVG(CurrentGPA) as AvgGPA
                FROM Students";

            var result = _dbHelper.ExecuteQuery(query, out var err);

            if (!string.IsNullOrEmpty(err) || result == null || result.Rows.Count == 0)
                return;

            var row = result.Rows[0];
            TotalStudents = Convert.ToInt32(row["Total"]);
            ActiveStudents = Convert.ToInt32(row["Active"]);
            AverageGpa = row["AvgGPA"] != DBNull.Value ? Math.Round(Convert.ToDecimal(row["AvgGPA"]), 2) : 0;

            var courseQuery = "SELECT COUNT(*) as Total FROM Courses WHERE IsActive = 1";
            var courseResult = _dbHelper.ExecuteQuery(courseQuery, out _);
            if (courseResult != null && courseResult.Rows.Count > 0)
                TotalCourses = Convert.ToInt32(courseResult.Rows[0]["Total"]);
        }

        private void LoadEnrollmentByMajor()
        {
            var query = @"
                SELECT 
                    Major,
                    COUNT(*) as StudentCount,
                    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Students), 1) as Percentage
                FROM Students
                WHERE Major IS NOT NULL AND Major != ''
                GROUP BY Major
                ORDER BY StudentCount DESC";

            EnrollmentByMajor = _dbHelper.ExecuteQuery(query, out _);
        }

        private void LoadPopularCourses()
        {
            var query = @"
                SELECT 
                    c.CourseCode,
                    c.CourseName,
                    COUNT(DISTINCT sch.StudentID) as EnrollmentCount,
                    AVG(CASE 
                        WHEN sch.Grade = 'A' THEN 4.0
                        WHEN sch.Grade = 'A-' THEN 3.7
                        WHEN sch.Grade = 'B+' THEN 3.3
                        WHEN sch.Grade = 'B' THEN 3.0
                        WHEN sch.Grade = 'B-' THEN 2.7
                        WHEN sch.Grade = 'C+' THEN 2.3
                        WHEN sch.Grade = 'C' THEN 2.0
                        WHEN sch.Grade = 'C-' THEN 1.7
                        WHEN sch.Grade = 'D+' THEN 1.3
                        WHEN sch.Grade = 'D' THEN 1.0
                        WHEN sch.Grade = 'F' THEN 0.0
                        ELSE NULL
                    END) as AverageGrade
                FROM Courses c
                LEFT JOIN StudentCourseHistory sch ON c.CourseID = sch.CourseID
                WHERE c.IsActive = 1
                GROUP BY c.CourseID, c.CourseCode, c.CourseName
                HAVING EnrollmentCount > 0
                ORDER BY EnrollmentCount DESC
                LIMIT 10";

            PopularCourses = _dbHelper.ExecuteQuery(query, out _);
        }

        private void LoadGpaByMajor()
        {
            var query = @"
                SELECT 
                    Major,
                    COUNT(*) as StudentCount,
                    ROUND(AVG(CurrentGPA), 2) as AverageGPA,
                    ROUND(MIN(CurrentGPA), 2) as MinGPA,
                    ROUND(MAX(CurrentGPA), 2) as MaxGPA
                FROM Students
                WHERE Major IS NOT NULL AND Major != ''
                GROUP BY Major
                ORDER BY AverageGPA DESC";

            GpaByMajor = _dbHelper.ExecuteQuery(query, out _);
        }

        private void LoadCompletionRates()
        {
            var query = @"
                SELECT 
                    s.Major,
                    COUNT(DISTINCT s.StudentID) as TotalStudents,
                    SUM(CASE WHEN s.TotalCreditsEarned >= 120 THEN 1 ELSE 0 END) as CompletedStudents,
                    ROUND(SUM(CASE WHEN s.TotalCreditsEarned >= 120 THEN 1 ELSE 0 END) * 100.0 / COUNT(DISTINCT s.StudentID), 1) as CompletionRate,
                    ROUND(AVG(s.TotalCreditsEarned), 1) as AvgCreditsEarned
                FROM Students s
                WHERE s.Major IS NOT NULL AND s.Major != ''
                GROUP BY s.Major
                ORDER BY CompletionRate DESC";

            CompletionRates = _dbHelper.ExecuteQuery(query, out _);
        }

        private void LoadChatUsageAnalytics()
        {
            // Total messages and active users
            var summaryQuery = @"
                    SELECT 
                        SUM(MessageCount) as TotalMessages,
                        COUNT(DISTINCT StudentID) as ActiveUsers
                    FROM ChatUsageLogs";

            var summary = _dbHelper.ExecuteQuery(summaryQuery, out var err);
            if (!string.IsNullOrEmpty(err) || summary == null || summary.Rows.Count == 0)
                return;

            var row = summary.Rows[0];
            TotalChatMessages = row["TotalMessages"] != DBNull.Value ? Convert.ToInt32(row["TotalMessages"]) : 0;
            ActiveChatUsers = row["ActiveUsers"] != DBNull.Value ? Convert.ToInt32(row["ActiveUsers"]) : 0;

            // Usage by day (last 30 days)
            var dailyQuery = @"
                    SELECT 
                        SessionDate,
                        SUM(MessageCount) as DailyMessages,
                        COUNT(DISTINCT StudentID) as DailyUsers
                    FROM ChatUsageLogs
                    WHERE SessionDate >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
                    GROUP BY SessionDate
                    ORDER BY SessionDate DESC
                    LIMIT 30";

            ChatUsageByDay = _dbHelper.ExecuteQuery(dailyQuery, out _);
        }

        private void LoadFileUploadStatistics()
        {
            // Count bulletins
            var bulletinQuery = "SELECT COUNT(*) as Total FROM Bulletins WHERE IsActive = 1";
            var bulletins = _dbHelper.ExecuteQuery(bulletinQuery, out _);
            if (bulletins != null && bulletins.Rows.Count > 0)
                TotalBulletins = Convert.ToInt32(bulletins.Rows[0]["Total"]);

            // Count supporting documents
            var docsQuery = "SELECT COUNT(*) as Total FROM SupportingDocuments WHERE IsActive = 1";
            var docs = _dbHelper.ExecuteQuery(docsQuery, out _);
            if (docs != null && docs.Rows.Count > 0)
                TotalSupportingDocs = Convert.ToInt32(docs.Rows[0]["Total"]);

            // Recent uploads
            var recentQuery = @"
                    SELECT 'Bulletin' as Type, FileName, UploadDate, FileSize
                    FROM Bulletins WHERE IsActive = 1
                    UNION ALL
                    SELECT 'Document' as Type, DocumentName as FileName, UploadDate, FileSize
                    FROM SupportingDocuments WHERE IsActive = 1
                    ORDER BY UploadDate DESC
                    LIMIT 10";

            RecentUploads = _dbHelper.ExecuteQuery(recentQuery, out _);
        }

    }
}
