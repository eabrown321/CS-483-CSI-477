using AdvisorDb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Data;

namespace CS_483_CSI_477.Pages
{
    public class AdminStudentPlannerModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;

        public string StudentName { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public int ViewedStudentId { get; set; }
        public int StartYear { get; set; } = DateTime.Now.Year;
        public List<SemesterPlanView> Semesters { get; set; } = new();

        public AdminStudentPlannerModel(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        public IActionResult OnGet(int studentId)
        {
            // Admin guard
            if (!HttpContext.Session.GetInt32("AdminID").HasValue)
                return RedirectToPage("/Login");
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/StudentDashboard");

            if (studentId <= 0)
                return RedirectToPage("/AdminDashboard");

            ViewedStudentId = studentId;
            LoadStudentInfo(studentId);
            LoadPlannedCourses(studentId);
            return Page();
        }

        private void LoadStudentInfo(int studentId)
        {
            string query = @"
                SELECT CONCAT(FirstName, ' ', LastName) AS FullName, Major, EnrollmentYear
                FROM Students WHERE StudentID = @sid LIMIT 1;";

            var result = _dbHelper.ExecuteQuery(query, new[]
            {
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = studentId }
            }, out _);

            if (result != null && result.Rows.Count > 0)
            {
                var row = result.Rows[0];
                StudentName = row["FullName"]?.ToString() ?? "Student";
                Major = row["Major"]?.ToString() ?? "Undeclared";

                var enrollVal = row["EnrollmentYear"];
                if (enrollVal != DBNull.Value &&
                    int.TryParse(enrollVal.ToString(), out var enrollYear) &&
                    enrollYear > 1900)
                    StartYear = enrollYear;
                else
                    StartYear = DateTime.Now.Year;
            }
        }

        private void LoadPlannedCourses(int studentId)
        {
            // Build 4-year semester grid
            Semesters.Clear();
            for (int year = 0; year < 4; year++)
            {
                Semesters.Add(new SemesterPlanView { Term = "Fall", Year = StartYear + year });
                Semesters.Add(new SemesterPlanView { Term = "Spring", Year = StartYear + year + 1 });
            }

            string plannedQuery = @"
                SELECT 
                    pc.PlannedCourseID, pc.CourseID,
                    c.CourseCode, c.CourseName, c.CreditHours, c.TypicalTermsOffered,
                    pc.PlannedTerm, pc.PlannedYear,
                    pc.IsCompleted, pc.CompletedWithGrade
                FROM PlannedCourses pc
                JOIN Courses c ON pc.CourseID = c.CourseID
                JOIN StudentDegreePlans sdp ON pc.PlanID = sdp.PlanID
                WHERE sdp.StudentID = @sid
                  AND (sdp.IsActive = 1 OR sdp.IsActive IS NULL)
                ORDER BY pc.PlannedYear,
                         CASE WHEN pc.PlannedTerm = 'Spring' THEN 1
                              WHEN pc.PlannedTerm = 'Summer' THEN 2
                              WHEN pc.PlannedTerm = 'Fall'   THEN 3
                              ELSE 4 END,
                         c.CourseCode;";

            var plannedResult = _dbHelper.ExecuteQuery(plannedQuery, new[]
            {
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = studentId }
            }, out _);

            if (plannedResult != null)
            {
                foreach (DataRow row in plannedResult.Rows)
                {
                    bool isCompleted = false;
                    var ic = row["IsCompleted"];
                    if (ic != DBNull.Value)
                    {
                        if (ic is bool b) isCompleted = b;
                        else if (int.TryParse(ic.ToString(), out var n)) isCompleted = (n != 0);
                    }

                    string term = row["PlannedTerm"]?.ToString() ?? "";
                    int year = Convert.ToInt32(row["PlannedYear"]);
                    var semester = Semesters.FirstOrDefault(s => s.Term == term && s.Year == year);
                    semester?.Courses.Add(new PlannedCourseView
                    {
                        CourseCode = row["CourseCode"]?.ToString() ?? "",
                        CourseName = row["CourseName"]?.ToString() ?? "",
                        CreditHours = Convert.ToInt32(row["CreditHours"]),
                        TermsOffered = row["TypicalTermsOffered"]?.ToString() ?? "",
                        IsCompleted = isCompleted,
                        CompletedWithGrade = row["CompletedWithGrade"]?.ToString() ?? ""
                    });
                }
            }

            // Also pull completed courses from StudentCourseHistory
            string historyQuery = @"
                SELECT 
                    c.CourseCode, c.CourseName, c.CreditHours, c.TypicalTermsOffered,
                    sch.Status, sch.Term AS PlannedTerm, sch.AcademicYear AS PlannedYear, sch.Grade
                FROM StudentCourseHistory sch
                JOIN Courses c ON sch.CourseID = c.CourseID
                WHERE sch.StudentID = @sid
                  AND sch.Status IN ('Completed', 'In Progress');";

            var historyResult = _dbHelper.ExecuteQuery(historyQuery, new[]
            {
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = studentId }
            }, out _);

            if (historyResult != null)
            {
                foreach (DataRow row in historyResult.Rows)
                {
                    string term = row["PlannedTerm"]?.ToString() ?? "";
                    int year = Convert.ToInt32(row["PlannedYear"]);
                    var semester = Semesters.FirstOrDefault(s => s.Term == term && s.Year == year);
                    if (semester == null) continue;

                    string courseCode = row["CourseCode"]?.ToString() ?? "";
                    if (semester.Courses.Any(c => c.CourseCode == courseCode)) continue;

                    semester.Courses.Add(new PlannedCourseView
                    {
                        CourseCode = courseCode,
                        CourseName = row["CourseName"]?.ToString() ?? "",
                        CreditHours = Convert.ToInt32(row["CreditHours"]),
                        TermsOffered = row["TypicalTermsOffered"]?.ToString() ?? "",
                        IsCompleted = row["Status"]?.ToString() == "Completed",
                        CompletedWithGrade = row["Grade"]?.ToString() ?? ""
                    });
                }
            }
        }

        public class SemesterPlanView
        {
            public string Term { get; set; } = "";
            public int Year { get; set; }
            public List<PlannedCourseView> Courses { get; set; } = new();
            public int TotalCredits => Courses.Sum(c => c.CreditHours);
        }

        public class PlannedCourseView
        {
            public string CourseCode { get; set; } = "";
            public string CourseName { get; set; } = "";
            public int CreditHours { get; set; }
            public string TermsOffered { get; set; } = "";
            public bool IsCompleted { get; set; }
            public string CompletedWithGrade { get; set; } = "";
        }
    }
}