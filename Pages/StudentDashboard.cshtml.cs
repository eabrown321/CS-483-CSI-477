using AdvisorDb;
using CS_483_CSI_477.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CS_483_CSI_477.Pages
{
    public class StudentDashboardModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly PrerequisiteService _prereqService;
        private readonly AccountHoldService _accountHoldService;

        public string StudentName { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public decimal CurrentGPA { get; set; }
        public int TotalCreditsEarned { get; set; }
        public int CoursesCompleted { get; set; }
        public string EnrollmentStatus { get; set; } = string.Empty;
        public bool HasAccountHolds { get; set; }
        public string AccountHoldMessage { get; set; } = string.Empty;

        public StudentDashboardModel(DatabaseHelper dbHelper, PrerequisiteService prereqService, AccountHoldService accountHoldService)
        {
            _dbHelper = dbHelper;
            _prereqService = prereqService;
            _accountHoldService = accountHoldService;
        }

        public IActionResult OnGet()
        {
            if (!HttpContext.Session.GetInt32("StudentID").HasValue)
                return RedirectToPage("/Login");

            // Prevent admins from landing on student pages
            if (HttpContext.Session.GetString("Role") == "Admin")
                return RedirectToPage("/AdminDashboard");

            LoadStudentInfo();
            return Page();
        }

        private void LoadStudentInfo()
        {
            int studentId = HttpContext.Session.GetInt32("StudentID") ?? 0;

            string query = $@"
                SELECT 
                    CONCAT(s.FirstName, ' ', s.LastName) as FullName,
                    s.Major,
                    s.CurrentGPA,
                    s.TotalCreditsEarned,
                    s.EnrollmentStatus
                FROM Students s
                WHERE s.StudentID = {studentId}";

            var result = _dbHelper.ExecuteQuery(query, out _);
            if (result != null && result.Rows.Count > 0)
            {
                var row = result.Rows[0];
                StudentName = row["FullName"].ToString() ?? "Student";
                Major = row["Major"].ToString() ?? "Undeclared";
                CurrentGPA = decimal.Parse(row["CurrentGPA"].ToString() ?? "0");
                TotalCreditsEarned = int.Parse(row["TotalCreditsEarned"].ToString() ?? "0");
                EnrollmentStatus = row["EnrollmentStatus"].ToString() ?? "Active";
            }

            string courseQuery = $@"
                SELECT COUNT(*) as CourseCount
                FROM StudentCourseHistory
                WHERE StudentID = {studentId}
                  AND Status = 'Completed'";

            var courseResult = _dbHelper.ExecuteQuery(courseQuery, out _);
            if (courseResult != null && courseResult.Rows.Count > 0)
                CoursesCompleted = int.Parse(courseResult.Rows[0]["CourseCount"].ToString() ?? "0");

            HasAccountHolds = _accountHoldService.HasActiveHolds(studentId);
            if (HasAccountHolds)
                AccountHoldMessage = _accountHoldService.GetActiveHoldsMessage(studentId);
        }
    }
}