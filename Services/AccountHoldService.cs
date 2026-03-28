using AdvisorDb;
using MySql.Data.MySqlClient;

namespace CS_483_CSI_477.Services
{
    public class AccountHoldService
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly IConfiguration _configuration;

        public AccountHoldService(DatabaseHelper dbHelper, IConfiguration configuration)
        {
            _dbHelper = dbHelper;
            _configuration = configuration;
        }

        public string GetActiveHoldsMessage(int studentId)
        {
            var query = @"
                SELECT HoldType, HoldReason, DocumentPath 
                FROM AccountHolds 
                WHERE StudentID = @studentId AND IsActive = 1
                ORDER BY PlacedDate DESC";

            var holds = _dbHelper.ExecuteQuery(query, new[]
            {
                new MySqlParameter("@studentId", MySqlDbType.Int32) { Value = studentId }
            }, out var err);

            if (!string.IsNullOrEmpty(err) || holds == null || holds.Rows.Count == 0)
                return "";

            var messages = new List<string>();
            messages.Add("⚠️ ACCOUNT HOLDS DETECTED:");
            foreach (System.Data.DataRow row in holds.Rows)
            {
                var holdType = row["HoldType"].ToString();
                var reason = row["HoldReason"].ToString();
                messages.Add($"\n• {holdType} Hold: {reason}");
            }
            messages.Add("\n\n Please contact your academic advisor to resolve these holds before registering for classes.");
            return string.Join("", messages);
        }

        public bool HasActiveHolds(int studentId)
        {
            var query = "SELECT COUNT(*) as Count FROM AccountHolds WHERE StudentID = @studentId AND IsActive = 1";
            var result = _dbHelper.ExecuteQuery(query, new[]
            {
                new MySqlParameter("@studentId", MySqlDbType.Int32) { Value = studentId }
            }, out _);

            if (result == null || result.Rows.Count == 0)
                return false;

            return Convert.ToInt32(result.Rows[0]["Count"]) > 0;
        }

        /// Returns all active holds for a student as a DataTable (for admin display).
        public System.Data.DataTable? GetActiveHolds(int studentId)
        {
            var query = @"
                SELECT HoldID, HoldType, HoldReason, PlacedDate, PlacedBy
                FROM AccountHolds
                WHERE StudentID = @studentId AND IsActive = 1
                ORDER BY PlacedDate DESC";

            return _dbHelper.ExecuteQuery(query, new[]
            {
                new MySqlParameter("@studentId", MySqlDbType.Int32) { Value = studentId }
            }, out _);
        }

        /// Adds a new active hold for a student. Returns true on success.
        public bool AddHold(int studentId, string holdType, string holdReason, int placedByAdminId)
        {
            var query = @"
                INSERT INTO AccountHolds (StudentID, HoldType, HoldReason, IsActive, PlacedDate, PlacedBy)
                VALUES (@studentId, @holdType, @holdReason, 1, NOW(), @placedBy)";

            var rows = _dbHelper.ExecuteNonQuery(query, new[]
            {
                new MySqlParameter("@studentId", MySqlDbType.Int32)  { Value = studentId },
                new MySqlParameter("@holdType",  MySqlDbType.VarChar) { Value = holdType },
                new MySqlParameter("@holdReason",MySqlDbType.VarChar) { Value = holdReason },
                new MySqlParameter("@placedBy",  MySqlDbType.Int32)  { Value = placedByAdminId }
            }, out _);

            return rows > 0;
        }

        /// Deactivates a hold by HoldID. Verifies it belongs to the given student. Returns true on success.
        public bool RemoveHold(int holdId, int studentId)
        {
            var query = @"
                UPDATE AccountHolds
                SET IsActive = 0
                WHERE HoldID = @holdId AND StudentID = @studentId AND IsActive = 1";

            var rows = _dbHelper.ExecuteNonQuery(query, new[]
            {
                new MySqlParameter("@holdId",    MySqlDbType.Int32) { Value = holdId },
                new MySqlParameter("@studentId", MySqlDbType.Int32) { Value = studentId }
            }, out _);

            return rows > 0;
        }
    }
}