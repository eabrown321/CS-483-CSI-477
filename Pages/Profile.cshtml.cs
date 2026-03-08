using AdvisorDb;
using CS_483_CSI_477.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace CS_483_CSI_477.Pages
{
    public class CourseHistoryItem
    {
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public int CreditHours { get; set; }
        public string Term { get; set; } = "";
        public int AcademicYear { get; set; }
        public string? Grade { get; set; }
        public string Status { get; set; } = "";
    }

    public class ProfileModel : PageModel
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly ILogger<ProfileModel> _logger;
        private readonly IConfiguration _configuration;

        public string FullName { get; set; } = "";
        public string StudentIDNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public string Major { get; set; } = "";
        public string EnrollmentStatus { get; set; } = "";
        public int EnrollmentYear { get; set; }
        public decimal CurrentGPA { get; set; }
        public int TotalCreditsEarned { get; set; }
        public int CourseCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ProfilePhotoUrl { get; set; }
        public string Initials { get; set; } = "";
        public List<CourseHistoryItem> CourseHistory { get; set; } = new();

        public ProfileModel(DatabaseHelper dbHelper, ILogger<ProfileModel> logger, IConfiguration configuration)
        {
            _dbHelper = dbHelper;
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult OnGet()
        {
            var sid = HttpContext.Session.GetInt32("StudentID");
            if (!sid.HasValue)
                return RedirectToPage("/Login");

            LoadProfile(sid.Value);
            LoadCourseHistory(sid.Value);
            return Page();
        }

        private void LoadProfile(int studentId)
        {
            var sql = @"
                SELECT 
                    CONCAT(FirstName, ' ', LastName) as FullName,
                    StudentIDNumber, Email, Major,
                    EnrollmentStatus, EnrollmentYear,
                    COALESCE(CurrentGPA, 0) as CurrentGPA,
                    COALESCE(TotalCreditsEarned, 0) as TotalCreditsEarned,
                    COALESCE(CreatedAt, NOW()) as CreatedAt,
                    FirstName, LastName
                FROM Students
                WHERE StudentID = @sid LIMIT 1";

            var result = _dbHelper.ExecuteQuery(sql, new[]
            {
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = studentId }
            }, out var err);

            if (!string.IsNullOrEmpty(err) || result == null || result.Rows.Count == 0) return;

            var row = result.Rows[0];
            FullName = row["FullName"]?.ToString() ?? "";
            StudentIDNumber = row["StudentIDNumber"]?.ToString() ?? "";
            Email = row["Email"]?.ToString() ?? "";
            Major = row["Major"]?.ToString() ?? "";
            EnrollmentStatus = row["EnrollmentStatus"]?.ToString() ?? "";
            EnrollmentYear = row["EnrollmentYear"] != DBNull.Value ? Convert.ToInt32(row["EnrollmentYear"]) : DateTime.Now.Year;
            CurrentGPA = row["CurrentGPA"] != DBNull.Value ? Convert.ToDecimal(row["CurrentGPA"]) : 0;
            TotalCreditsEarned = row["TotalCreditsEarned"] != DBNull.Value ? Convert.ToInt32(row["TotalCreditsEarned"]) : 0;
            CreatedAt = row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.Now;

            var firstName = row["FirstName"]?.ToString() ?? "";
            var lastName = row["LastName"]?.ToString() ?? "";
            Initials = $"{(firstName.Length > 0 ? firstName[0] : ' ')}{(lastName.Length > 0 ? lastName[0] : ' ')}".Trim().ToUpperInvariant();

            // Check for profile photo in Azure
            ProfilePhotoUrl = GetProfilePhotoUrl(studentId);
        }

        private void LoadCourseHistory(int studentId)
        {
            var sql = @"
                SELECT 
                    c.CourseCode, c.CourseName, c.CreditHours,
                    sch.Term, sch.AcademicYear, sch.Grade, sch.Status
                FROM StudentCourseHistory sch
                JOIN Courses c ON sch.CourseID = c.CourseID
                WHERE sch.StudentID = @sid
                ORDER BY sch.AcademicYear DESC, 
                    FIELD(sch.Term, 'Fall', 'Summer', 'Spring') ASC,
                    sch.Status ASC";

            var result = _dbHelper.ExecuteQuery(sql, new[]
            {
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = studentId }
            }, out var err);

            if (!string.IsNullOrEmpty(err) || result == null) return;

            foreach (DataRow row in result.Rows)
            {
                CourseHistory.Add(new CourseHistoryItem
                {
                    CourseCode = row["CourseCode"]?.ToString() ?? "",
                    CourseName = row["CourseName"]?.ToString() ?? "",
                    CreditHours = row["CreditHours"] != DBNull.Value ? Convert.ToInt32(row["CreditHours"]) : 0,
                    Term = row["Term"]?.ToString() ?? "",
                    AcademicYear = row["AcademicYear"] != DBNull.Value ? Convert.ToInt32(row["AcademicYear"]) : 0,
                    Grade = row["Grade"]?.ToString(),
                    Status = row["Status"]?.ToString() ?? ""
                });
            }

            CourseCount = CourseHistory.Count;
        }

        private string? GetProfilePhotoUrl(int studentId)
        {
            // Check if a profile photo exists in the DB or Azure
            var sql = "SELECT ProfilePhotoUrl FROM Students WHERE StudentID = @sid LIMIT 1";
            var result = _dbHelper.ExecuteQuery(sql, new[]
            {
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = studentId }
            }, out _);

            if (result != null && result.Rows.Count > 0 && result.Rows[0][0] != DBNull.Value)
                return result.Rows[0][0]?.ToString();

            return null;
        }

        // ── CHANGE EMAIL ──────────────────────────────────────────────
        public IActionResult OnPostChangeEmail(string NewEmail, string CurrentPassword)
        {
            var sid = HttpContext.Session.GetInt32("StudentID");
            if (!sid.HasValue) return RedirectToPage("/Login");

            NewEmail = (NewEmail ?? "").Trim().ToLowerInvariant();
            CurrentPassword = (CurrentPassword ?? "").Trim();

            if (string.IsNullOrEmpty(NewEmail) || !NewEmail.Contains("@"))
            {
                TempData["ProfileError"] = "Please enter a valid email address.";
                return RedirectToPage();
            }

            if (!NewEmail.EndsWith(".edu", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ProfileError"] = "Email must be a .edu address.";
                return RedirectToPage();
            }

            if (string.IsNullOrEmpty(CurrentPassword))
            {
                TempData["ProfileError"] = "Please enter your current password to confirm.";
                return RedirectToPage();
            }

            // Verify current password
            if (!VerifyPassword(sid.Value, CurrentPassword))
            {
                TempData["ProfileError"] = "Incorrect password.";
                return RedirectToPage();
            }

            // Check email not already taken
            var check = _dbHelper.ExecuteQuery(
                "SELECT StudentID FROM Students WHERE Email = @email AND StudentID != @sid LIMIT 1",
                new[]
                {
                    new MySqlParameter("@email", MySqlDbType.VarChar) { Value = NewEmail },
                    new MySqlParameter("@sid", MySqlDbType.Int32) { Value = sid.Value }
                }, out _);

            if (check != null && check.Rows.Count > 0)
            {
                TempData["ProfileError"] = "That email is already in use by another account.";
                return RedirectToPage();
            }

            var updateSql = "UPDATE Students SET Email = @email, EmailVerified = 0 WHERE StudentID = @sid";
            _dbHelper.ExecuteNonQuery(updateSql, new[]
            {
                new MySqlParameter("@email", MySqlDbType.VarChar) { Value = NewEmail },
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = sid.Value }
            }, out var err);

            if (!string.IsNullOrEmpty(err))
            {
                TempData["ProfileError"] = "Failed to update email. Please try again.";
                return RedirectToPage();
            }

            // Clear cached student context so it reloads
            HttpContext.Session.Remove("StudentContextText");
            TempData["ProfileSuccess"] = "Email updated successfully.";
            return RedirectToPage();
        }

        // ── CHANGE PASSWORD ───────────────────────────────────────────
        public IActionResult OnPostChangePassword(string CurrentPassword, string NewPassword, string ConfirmNewPassword)
        {
            var sid = HttpContext.Session.GetInt32("StudentID");
            if (!sid.HasValue) return RedirectToPage("/Login");

            CurrentPassword = (CurrentPassword ?? "").Trim();
            NewPassword = (NewPassword ?? "").Trim();
            ConfirmNewPassword = (ConfirmNewPassword ?? "").Trim();

            if (string.IsNullOrEmpty(CurrentPassword))
            {
                TempData["ProfileError"] = "Please enter your current password.";
                return RedirectToPage();
            }

            if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < 8)
            {
                TempData["ProfileError"] = "New password must be at least 8 characters.";
                return RedirectToPage();
            }

            if (NewPassword != ConfirmNewPassword)
            {
                TempData["ProfileError"] = "New passwords do not match.";
                return RedirectToPage();
            }

            if (!VerifyPassword(sid.Value, CurrentPassword))
            {
                TempData["ProfileError"] = "Current password is incorrect.";
                return RedirectToPage();
            }

            var newHash = HashPassword(NewPassword);
            var updateSql = "UPDATE Students SET PasswordHash = @hash WHERE StudentID = @sid";
            _dbHelper.ExecuteNonQuery(updateSql, new[]
            {
                new MySqlParameter("@hash", MySqlDbType.VarChar) { Value = newHash },
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = sid.Value }
            }, out var err);

            if (!string.IsNullOrEmpty(err))
            {
                TempData["ProfileError"] = "Failed to update password. Please try again.";
                return RedirectToPage();
            }

            TempData["ProfileSuccess"] = "Password updated successfully.";
            return RedirectToPage();
        }

        // ── UPLOAD PHOTO ──────────────────────────────────────────────
        public async Task<IActionResult> OnPostUploadPhotoAsync(IFormFile PhotoFile)
        {
            var sid = HttpContext.Session.GetInt32("StudentID");
            if (!sid.HasValue) return RedirectToPage("/Login");

            if (PhotoFile == null || PhotoFile.Length == 0)
            {
                TempData["ProfileError"] = "No photo selected.";
                return RedirectToPage();
            }

            var ext = Path.GetExtension(PhotoFile.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
            {
                TempData["ProfileError"] = "Only JPG, PNG, or WebP images are allowed.";
                return RedirectToPage();
            }

            const long maxBytes = 5L * 1024L * 1024L;
            if (PhotoFile.Length > maxBytes)
            {
                TempData["ProfileError"] = "Image must be under 5MB.";
                return RedirectToPage();
            }

            try
            {
                var azureConnStr = _configuration["AzureBlobStorage:ConnectionString"];
                if (string.IsNullOrEmpty(azureConnStr))
                {
                    TempData["ProfileError"] = "Azure storage not configured.";
                    return RedirectToPage();
                }

                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(azureConnStr);
                var containerClient = blobServiceClient.GetBlobContainerClient("profile-photos");
                await containerClient.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

                var blobName = $"student_{sid.Value}{ext}";
                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = PhotoFile.OpenReadStream();
                await blobClient.UploadAsync(stream, overwrite: true);

                var photoUrl = blobClient.Uri.ToString();

                // Check if ProfilePhotoUrl column exists before updating
                var updateSql = "UPDATE Students SET ProfilePhotoUrl = @url WHERE StudentID = @sid";
                _dbHelper.ExecuteNonQuery(updateSql, new[]
                {
                    new MySqlParameter("@url", MySqlDbType.VarChar) { Value = photoUrl },
                    new MySqlParameter("@sid", MySqlDbType.Int32) { Value = sid.Value }
                }, out var err);

                if (!string.IsNullOrEmpty(err))
                {
                    _logger.LogWarning("ProfilePhotoUrl update failed (column may not exist): {Err}", err);
                    TempData["ProfileError"] = "Photo uploaded but could not save URL. You may need to add a ProfilePhotoUrl column to the Students table.";
                    return RedirectToPage();
                }

                TempData["ProfileSuccess"] = "Profile photo updated!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo upload failed");
                TempData["ProfileError"] = "Photo upload failed. Please try again.";
            }

            return RedirectToPage();
        }

        // ── HELPERS ───────────────────────────────────────────────────
        private bool VerifyPassword(int studentId, string password)
        {
            var hash = HashPassword(password);
            var sql = "SELECT StudentID FROM Students WHERE StudentID = @sid AND PasswordHash = @hash LIMIT 1";
            var result = _dbHelper.ExecuteQuery(sql, new[]
            {
                new MySqlParameter("@sid", MySqlDbType.Int32) { Value = studentId },
                new MySqlParameter("@hash", MySqlDbType.VarChar) { Value = hash }
            }, out _);

            return result != null && result.Rows.Count > 0;
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
