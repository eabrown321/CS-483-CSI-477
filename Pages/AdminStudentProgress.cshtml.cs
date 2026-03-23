using AdvisorDb;
using CS_483_CSI_477.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;

namespace CS_483_CSI_477.Pages
{
    public class AdminStudentProgressModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly AccountHoldService _accountHoldService;

        public string StudentName { get; set; } = string.Empty;
        public string Major { get; set; } = string.Empty;
        public decimal CurrentGPA { get; set; }
        public int TotalCreditsEarned { get; set; }
        public int TotalCreditsRequired { get; set; } = 120;
        public int CompletionPercentage { get; set; }
        public int ViewedStudentId { get; set; }

        public bool HasAccountHolds { get; set; }
        public string AccountHoldMessage { get; set; } = string.Empty;

        public DataTable? CompletedCourses { get; set; }

        public string CoreRequirement1Label { get; set; } = "CS Core Requirements";
        public int CoreRequirement1Credits { get; set; }
        public int CoreRequirement1Required { get; set; } = 40;

        public string CoreRequirement2Label { get; set; } = "Advanced CS Courses";
        public int CoreRequirement2Credits { get; set; }
        public int CoreRequirement2Required { get; set; } = 12;

        public int ElectiveCredits { get; set; }
        public int Core39Credits { get; set; }

        private static readonly HashSet<string> Core39CourseCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "ENG 101", "ENG 201",
            "CMST 101", "CMST 201", "CMST 102",
            "MATH 111", "MATH 114", "MATH 115", "MATH 215", "MATH 230",
            "PHYS 101", "PHYS 201", "PHYS 202", "PHYS 301",
            "BIOL 101", "BIOL 121", "BIOL 201",
            "CHEM 101", "CHEM 111", "CHEM 112",
            "GEOL 101", "GEOL 111",
            "ECON 175", "ECON 208", "ECON 209",
            "SOC 121", "SOC 201",
            "PSYC 201", "PSYC 101", "PSY 201",
            "POLS 101", "POLS 201",
            "ANTH 121", "ANTH 201",
            "HIST 101", "HIST 102", "HIST 201", "HIST 202",
            "PHIL 101", "PHIL 201",
            "ART 101", "ART 201",
            "MUS 101", "MUS 201",
            "THTR 101", "THTR 201",
            "ENG 105", "ENG 185", "ENG 205",
            "GEOG 101",
            "BCOM 231", "MNGT 452",
            "ECON 241"
        };

        public AdminStudentProgressModel(DatabaseHelper dbHelper, AccountHoldService accountHoldService)
        {
            _dbHelper = dbHelper;
            _accountHoldService = accountHoldService;
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

            LoadStudentProgress(studentId);
            LoadAllCourses(studentId);
            LoadRequirementBreakdown(studentId);

            HasAccountHolds = _accountHoldService.HasActiveHolds(studentId);
            if (HasAccountHolds)
                AccountHoldMessage = _accountHoldService.GetActiveHoldsMessage(studentId);

            return Page();
        }

        private void LoadStudentProgress(int studentId)
        {
            string query = $@"
                SELECT 
                    CONCAT(s.FirstName, ' ', s.LastName) as FullName,
                    s.Major,
                    s.CurrentGPA,
                    s.TotalCreditsEarned,
                    dp.TotalCreditsRequired
                FROM Students s
                LEFT JOIN DegreePrograms dp ON s.Major = dp.DegreeName
                WHERE s.StudentID = {studentId}";

            var result = _dbHelper.ExecuteQuery(query, out _);
            if (result != null && result.Rows.Count > 0)
            {
                var row = result.Rows[0];
                StudentName = row["FullName"].ToString() ?? "Student";
                Major = row["Major"].ToString() ?? "Undeclared";
                CurrentGPA = decimal.Parse(row["CurrentGPA"].ToString() ?? "0");
                TotalCreditsEarned = int.Parse(row["TotalCreditsEarned"].ToString() ?? "0");

                if (row["TotalCreditsRequired"] != DBNull.Value)
                    TotalCreditsRequired = int.Parse(row["TotalCreditsRequired"].ToString()!);

                CompletionPercentage = TotalCreditsRequired > 0
                    ? (TotalCreditsEarned * 100 / TotalCreditsRequired)
                    : 0;
            }
        }

        private void LoadAllCourses(int studentId)
        {
            string query = $@"
                SELECT 
                    c.CourseCode, c.CourseName, c.CreditHours,
                    sch.Grade, sch.Term, sch.AcademicYear, sch.Status
                FROM StudentCourseHistory sch
                JOIN Courses c ON sch.CourseID = c.CourseID
                WHERE sch.StudentID = {studentId}
                ORDER BY 
                    sch.AcademicYear ASC,
                    FIELD(sch.Term, 'Spring', 'Summer', 'Fall') ASC,
                    sch.Status ASC";

            CompletedCourses = _dbHelper.ExecuteQuery(query, out _);
        }

        private void LoadRequirementBreakdown(int studentId)
        {
            string degreeQuery = $@"
                SELECT dp.DegreeID, dp.DegreeCode
                FROM Students s
                JOIN DegreePrograms dp ON s.Major = dp.DegreeName
                WHERE s.StudentID = {studentId}";

            var degreeResult = _dbHelper.ExecuteQuery(degreeQuery, out _);
            if (degreeResult == null || degreeResult.Rows.Count == 0) return;

            int degreeId = int.Parse(degreeResult.Rows[0]["DegreeID"].ToString()!);
            string degreeCode = degreeResult.Rows[0]["DegreeCode"].ToString()!;

            string breakdownQuery = $@"
                SELECT dr.RequirementCategory, SUM(c.CreditHours) as EarnedCredits
                FROM StudentCourseHistory sch
                JOIN Courses c ON sch.CourseID = c.CourseID
                JOIN DegreeRequirements dr ON c.CourseID = dr.CourseID
                WHERE sch.StudentID = {studentId}
                  AND dr.DegreeID = {degreeId}
                  AND sch.Status = 'Completed'
                GROUP BY dr.RequirementCategory";

            var breakdown = _dbHelper.ExecuteQuery(breakdownQuery, out _);
            if (breakdown != null)
            {
                foreach (DataRow row in breakdown.Rows)
                {
                    string category = row["RequirementCategory"].ToString() ?? "";
                    int credits = int.Parse(row["EarnedCredits"].ToString() ?? "0");

                    if (degreeCode == "CS-BS")
                    {
                        if (category.Contains("CS Core") || category.Contains("Hardware") ||
                            category.Contains("Information Systems") || category.Contains("Mathematics"))
                            CoreRequirement1Credits += credits;
                        else if (category.Contains("Advanced") || category.Contains("Programming"))
                            CoreRequirement2Credits += credits;
                        else if (category.Contains("Elective"))
                            ElectiveCredits += credits;
                    }
                    else
                    {
                        CoreRequirement1Label = "Business Core";
                        CoreRequirement1Required = 34;
                        CoreRequirement2Label = "CIS Core";
                        CoreRequirement2Required = 12;

                        if (category.Contains("Business Core")) CoreRequirement1Credits += credits;
                        else if (category.Contains("CIS Core")) CoreRequirement2Credits += credits;
                        else if (category.Contains("Elective")) ElectiveCredits += credits;
                    }
                }
            }

            string allCompletedQuery = $@"
                SELECT c.CourseCode, c.CreditHours
                FROM StudentCourseHistory sch
                JOIN Courses c ON sch.CourseID = c.CourseID
                WHERE sch.StudentID = {studentId} AND sch.Status = 'Completed'";

            var completedResult = _dbHelper.ExecuteQuery(allCompletedQuery, out _);
            if (completedResult != null)
            {
                foreach (DataRow row in completedResult.Rows)
                {
                    string code = row["CourseCode"].ToString() ?? "";
                    if (Core39CourseCodes.Contains(code))
                        Core39Credits += int.Parse(row["CreditHours"].ToString() ?? "0");
                }
            }
        }
    }
}