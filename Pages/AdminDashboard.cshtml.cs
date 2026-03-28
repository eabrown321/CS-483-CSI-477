using AdvisorDb;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CS_483_CSI_477.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Data;

namespace CS_483_CSI_477.Pages;

public class AdminDashboardModel : PageModel
{
    private readonly DatabaseHelper _dbHelper;
    private readonly IConfiguration _config;
    private readonly AccountHoldService _holdService;

    // Connection status
    public bool IsConnected { get; set; }
    public string ConnectionMessage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    // Student lookup
    [BindProperty]
    public string StudentId { get; set; } = string.Empty;
    public DataTable? StudentResults { get; set; }
    public string SearchMessage { get; set; } = string.Empty;

    // Upload status
    public string UploadMessage { get; set; } = string.Empty;
    public bool UploadSuccess { get; set; }

    // Hold management
    public string HoldMessage { get; set; } = string.Empty;
    public bool HoldSuccess { get; set; }
    public DataTable? StudentHolds { get; set; }
    public int ViewedStudentId { get; set; }
    public string ViewedStudentName { get; set; } = string.Empty;

    [BindProperty] public string HoldType { get; set; } = string.Empty;
    [BindProperty] public string HoldReason { get; set; } = string.Empty;
    [BindProperty] public int HoldStudentId { get; set; }

    // Bulletin and document lists from DB
    public DataTable? Bulletins { get; set; }
    public DataTable? Documents { get; set; }

    // Upload form fields
    [BindProperty] public int BulletinYear { get; set; } = DateTime.Now.Year;
    [BindProperty] public string BulletinCategory { get; set; } = "Major";
    [BindProperty] public string BulletinDescription { get; set; } = string.Empty;
    [BindProperty] public string DocType { get; set; } = "Syllabus";
    [BindProperty] public string CourseCode { get; set; } = string.Empty;
    [BindProperty] public string DocDescription { get; set; } = string.Empty;

    public AdminDashboardModel(DatabaseHelper dbHelper, IConfiguration config, AccountHoldService holdService)
    {
        _dbHelper = dbHelper;
        _config = config;
        _holdService = holdService;
    }

    public IActionResult OnGet(int? viewStudentId = null)
    {
        if (!HttpContext.Session.GetInt32("AdminID").HasValue)
            return RedirectToPage("/Login");

        string role = HttpContext.Session.GetString("Role") ?? "";
        if (role != "Admin")
            return RedirectToPage("/StudentDashboard");

        IsConnected = _dbHelper.TestConnection(out string error);
        ConnectionMessage = IsConnected ? "✓ Database connected successfully" : "";
        ErrorMessage = IsConnected ? "" : $"Database connection failed: {error}";

        if (IsConnected)
        {
            LoadBulletins();
            LoadDocuments();

            // If returning from a hold action, reload that student's holds
            if (viewStudentId.HasValue && viewStudentId.Value > 0)
            {
                ViewedStudentId = viewStudentId.Value;
                LoadStudentHolds(viewStudentId.Value);
            }
        }

        return Page();
    }

    // ── Student Search ────────────────────────────────────────────────────
    public IActionResult OnPostSearch()
    {
        if (!HttpContext.Session.GetInt32("AdminID").HasValue)
            return RedirectToPage("/Login");

        IsConnected = _dbHelper.TestConnection(out string error);
        if (!IsConnected)
        {
            ErrorMessage = $"Connection failed: {error}";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(StudentId))
        {
            SearchMessage = "Please enter a name, Student ID, or email.";
            LoadBulletins(); LoadDocuments();
            return Page();
        }

        string escaped = MySqlEscape(StudentId.Trim());
        bool isNumeric = StudentId.Trim().All(char.IsDigit);

        string query;
        if (isNumeric)
        {
            // Search by StudentID
            query = $@"
                SELECT s.StudentID, 
                       CONCAT(s.FirstName, ' ', s.LastName) AS StudentName,
                       s.Email, s.Major, s.CurrentGPA,
                       s.TotalCreditsEarned, s.EnrollmentStatus
                FROM Students s
                WHERE s.StudentID = '{escaped}'
                LIMIT 20";
        }
        else if (StudentId.Contains("@"))
        {
            // Search by email
            query = $@"
                SELECT s.StudentID,
                       CONCAT(s.FirstName, ' ', s.LastName) AS StudentName,
                       s.Email, s.Major, s.CurrentGPA,
                       s.TotalCreditsEarned, s.EnrollmentStatus
                FROM Students s
                WHERE s.Email = '{escaped}'
                LIMIT 20";
        }
        else
        {
            // Search by name (first, last, or full)
            query = $@"
                SELECT s.StudentID,
                       CONCAT(s.FirstName, ' ', s.LastName) AS StudentName,
                       s.Email, s.Major, s.CurrentGPA,
                       s.TotalCreditsEarned, s.EnrollmentStatus
                FROM Students s
                WHERE s.FirstName  LIKE '%{escaped}%'
                   OR s.LastName   LIKE '%{escaped}%'
                   OR CONCAT(s.FirstName, ' ', s.LastName) LIKE '%{escaped}%'
                ORDER BY s.LastName, s.FirstName
                LIMIT 20";
        }

        StudentResults = _dbHelper.ExecuteQuery(query, out string queryError);

        if (!string.IsNullOrEmpty(queryError))
            ErrorMessage = $"Search error: {queryError}";
        else if (StudentResults == null || StudentResults.Rows.Count == 0)
            SearchMessage = "No student found matching that search.";
        else
            SearchMessage = $"Found {StudentResults.Rows.Count} record(s).";

        LoadBulletins(); LoadDocuments();
        return Page();
    }

    // ── Add Account Hold ─────────────────────────────────────────────────
    public IActionResult OnPostAddHold()
    {
        if (!HttpContext.Session.GetInt32("AdminID").HasValue)
            return RedirectToPage("/Login");

        int adminId = HttpContext.Session.GetInt32("AdminID") ?? 0;

        if (HoldStudentId <= 0 || string.IsNullOrWhiteSpace(HoldType) || string.IsNullOrWhiteSpace(HoldReason))
        {
            HoldMessage = "Hold type, reason, and student ID are all required.";
            HoldSuccess = false;
            LoadBulletins(); LoadDocuments();
            LoadStudentHolds(HoldStudentId);
            ViewedStudentId = HoldStudentId;
            return Page();
        }

        bool success = _holdService.AddHold(HoldStudentId, HoldType, HoldReason, adminId);
        HoldMessage = success ? "Hold added successfully." : "Failed to add hold.";
        HoldSuccess = success;

        return RedirectToPage(new { viewStudentId = HoldStudentId });
    }

    // ── Remove Account Hold ───────────────────────────────────────────────
    public IActionResult OnPostRemoveHold(int holdId, int studentId)
    {
        if (!HttpContext.Session.GetInt32("AdminID").HasValue)
            return RedirectToPage("/Login");

        if (holdId <= 0 || studentId <= 0)
            return RedirectToPage();

        _holdService.RemoveHold(holdId, studentId);
        return RedirectToPage(new { viewStudentId = studentId });
    }

    // ── Load Student Holds (for hold management panel) ────────────────────
    public IActionResult OnPostViewHolds(int studentId)
    {
        if (!HttpContext.Session.GetInt32("AdminID").HasValue)
            return RedirectToPage("/Login");

        return RedirectToPage(new { viewStudentId = studentId });
    }

    // ── Upload Bulletin PDF ───────────────────────────────────────────────
    public async Task<IActionResult> OnPostUploadBulletinAsync()
    {
        IsConnected = _dbHelper.TestConnection(out string error);
        if (!IsConnected) { ErrorMessage = $"Connection failed: {error}"; return Page(); }

        var file = Request.Form.Files["bulletinFile"];
        if (file == null || file.Length == 0)
        {
            UploadMessage = "Please select a PDF file to upload.";
            UploadSuccess = false;
            LoadBulletins(); LoadDocuments();
            return Page();
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
        {
            UploadMessage = "Only PDF files are accepted for bulletins.";
            UploadSuccess = false;
            LoadBulletins(); LoadDocuments();
            return Page();
        }

        try
        {
            var azureConnStr = _config["AzureBlobStorage:ConnectionString"];
            string containerName = BulletinCategory switch
            {
                "Minor" => "minors",
                _ => "bulletins"
            };

            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AdminDashboardModel>>();
            logger.LogWarning("=== AZURE DEBUG ===");
            logger.LogWarning($"Connection String Length: {azureConnStr?.Length ?? 0}");
            logger.LogWarning($"Container Name: {containerName}");
            logger.LogWarning($"Category: {BulletinCategory}");
            logger.LogWarning("===================");

            string fileUrl;
            if (string.IsNullOrEmpty(azureConnStr))
            {
                string localFolder = BulletinCategory == "Minor" ? "minors" : "bulletins";
                fileUrl = await SaveLocalAsync(file, localFolder, BulletinYear.ToString());
            }
            else
            {
                var blobClient = new BlobServiceClient(azureConnStr);
                var container = blobClient.GetBlobContainerClient(containerName);
                await container.CreateIfNotExistsAsync(PublicAccessType.None);
                var blobName = $"{BulletinYear}/Bulletin_{BulletinYear}_{Guid.NewGuid()}{ext}";
                var blob = container.GetBlobClient(blobName);
                using var stream = file.OpenReadStream();
                await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = "application/pdf" });
                fileUrl = blob.Uri.ToString();
            }

            string bulletinYearFormatted = $"{BulletinYear}-{BulletinYear + 1}";
            string insert = $@"
                INSERT INTO Bulletins 
                    (AcademicYear, BulletinYear, BulletinType, BulletinCategory, FileName, FilePath, FileSize, UploadedBy, Description)
                VALUES 
                    ({BulletinYear}, '{bulletinYearFormatted}', 'Undergraduate', '{MySqlEscape(BulletinCategory)}',
                    '{MySqlEscape(file.FileName)}', '{MySqlEscape(fileUrl)}', {file.Length}, 1,
                    '{MySqlEscape(BulletinDescription ?? $"{BulletinCategory} Bulletin {bulletinYearFormatted}")}')";

            int rows = _dbHelper.ExecuteNonQuery(insert, out string dbError);
            UploadMessage = rows > 0 ? $"✓ Bulletin for {BulletinYear} uploaded successfully!" : $"File uploaded but DB save failed: {dbError}";
            UploadSuccess = rows > 0;
        }
        catch (Exception ex)
        {
            UploadMessage = $"Upload failed: {ex.Message}";
            UploadSuccess = false;
        }

        LoadBulletins(); LoadDocuments();
        return Page();
    }

    // ── Upload Supporting Document ────────────────────────────────────────
    public async Task<IActionResult> OnPostUploadDocumentAsync()
    {
        IsConnected = _dbHelper.TestConnection(out string error);
        if (!IsConnected) { ErrorMessage = $"Connection failed: {error}"; return Page(); }

        var file = Request.Form.Files["documentFile"];
        if (file == null || file.Length == 0)
        {
            UploadMessage = "Please select a file to upload.";
            UploadSuccess = false;
            LoadBulletins(); LoadDocuments();
            return Page();
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".pdf", ".docx", ".doc", ".txt" };
        if (!allowed.Contains(ext))
        {
            UploadMessage = $"File type '{ext}' is not allowed.";
            UploadSuccess = false;
            LoadBulletins(); LoadDocuments();
            return Page();
        }

        try
        {
            var azureConnStr = _config["AzureBlobStorage:ConnectionString"];
            var containerName = _config["AzureBlobStorage:SupportingDocsContainer"] ?? "supporting-docs";
            string fileUrl;

            if (string.IsNullOrEmpty(azureConnStr) || azureConnStr == "PASTE_KEY_HERE")
            {
                fileUrl = await SaveLocalAsync(file, "documents", DocType.ToLower());
            }
            else
            {
                var blobClient = new BlobServiceClient(azureConnStr);
                var container = blobClient.GetBlobContainerClient(containerName);
                await container.CreateIfNotExistsAsync(PublicAccessType.None);
                var prefix = string.IsNullOrEmpty(CourseCode) ? "doc" : CourseCode.Replace(" ", "_");
                var blobName = $"{DocType.ToLower()}/{prefix}_{Guid.NewGuid()}{ext}";
                var blob = container.GetBlobClient(blobName);
                using var stream = file.OpenReadStream();
                await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
                fileUrl = blob.Uri.ToString();
            }

            string insert = $@"
                INSERT INTO SupportingDocuments 
                    (DocumentName, DocumentType, FilePath, FileSize, CourseCode, UploadedBy, Description)
                VALUES 
                    ('{MySqlEscape(file.FileName)}', '{MySqlEscape(DocType)}',
                     '{MySqlEscape(fileUrl)}', {file.Length},
                     '{MySqlEscape(CourseCode ?? "")}', 1,
                     '{MySqlEscape(DocDescription ?? DocType + " document")}')";

            int rows = _dbHelper.ExecuteNonQuery(insert, out string dbError);
            UploadMessage = rows > 0 ? "✓ Document uploaded successfully!" : $"File uploaded but DB save failed: {dbError}";
            UploadSuccess = rows > 0;
        }
        catch (Exception ex)
        {
            UploadMessage = $"Upload failed: {ex.Message}";
            UploadSuccess = false;
        }

        LoadBulletins(); LoadDocuments();
        return Page();
    }

    // ── Delete Bulletin ───────────────────────────────────────────────────
    public IActionResult OnPostDeleteBulletin(int id)
    {
        _dbHelper.ExecuteNonQuery($"UPDATE Bulletins SET IsActive=0 WHERE BulletinID={id}", out _);
        return RedirectToPage();
    }

    // ── Delete Document ───────────────────────────────────────────────────
    public IActionResult OnPostDeleteDocument(int id)
    {
        _dbHelper.ExecuteNonQuery($"UPDATE SupportingDocuments SET IsActive=0 WHERE DocumentID={id}", out _);
        return RedirectToPage();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void LoadBulletins()
    {
        Bulletins = _dbHelper.ExecuteQuery(
            @"SELECT BulletinID, AcademicYear, BulletinCategory, FileName, FileSize, UploadDate, Description 
              FROM Bulletins WHERE IsActive=1 
              ORDER BY BulletinCategory, AcademicYear DESC", out _);
    }

    private void LoadDocuments()
    {
        Documents = _dbHelper.ExecuteQuery(
            @"SELECT DocumentID, DocumentName, DocumentType, CourseCode, FileSize, UploadDate 
              FROM SupportingDocuments WHERE IsActive=1 
              ORDER BY UploadDate DESC", out _);
    }

    private void LoadStudentHolds(int studentId)
    {
        StudentHolds = _holdService.GetActiveHolds(studentId);

        // Also get the student name for the panel header
        var nameResult = _dbHelper.ExecuteQuery(
            $"SELECT CONCAT(FirstName, ' ', LastName) AS FullName FROM Students WHERE StudentID = {studentId} LIMIT 1",
            out _);
        if (nameResult != null && nameResult.Rows.Count > 0)
            ViewedStudentName = nameResult.Rows[0]["FullName"].ToString() ?? "";
    }

    private async Task<string> SaveLocalAsync(IFormFile file, string folder, string subfolder)
    {
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder, subfolder);
        Directory.CreateDirectory(uploadPath);
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(uploadPath, fileName);
        using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/{folder}/{subfolder}/{fileName}";
    }

    private static string MySqlEscape(string input) =>
        input?.Replace("'", "''").Replace("\\", "\\\\") ?? string.Empty;
}