using CS_483_CSI_477.Pages;
using System.Text;
using System.Text.RegularExpressions;

namespace CS_483_CSI_477.Services
{
    public class ChatMemoryService
    {
        private static readonly Regex TokenRx = new(@"[A-Za-z0-9]+", RegexOptions.Compiled);
        private static readonly Regex CourseCodeRx = new(@"\b[A-Z]{2,5}\s?\d{3}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public async Task<string> BuildMemoryBlockAsync(
            int studentId,
            string currentChatId,
            string currentQuestion,
            List<ChatMessage> currentMessages,
            IChatLogStore store)
        {
            var sb = new StringBuilder();

            var currentSummary = await store.GetSummaryAsync(studentId, currentChatId);
            if (!string.IsNullOrWhiteSpace(currentSummary))
            {
                sb.AppendLine("CURRENT CHAT SUMMARY:");
                sb.AppendLine(currentSummary.Trim());
                sb.AppendLine();
            }

            var recentTurns = currentMessages.TakeLast(6).ToList();
            if (recentTurns.Count > 0)
            {
                sb.AppendLine("RECENT TURNS:");
                foreach (var msg in recentTurns)
                {
                    var role = msg.Role == "assistant" ? "Advisor" : "Student";
                    sb.AppendLine($"- {role}: {TrimForPrompt(msg.Content, 240)}");
                }
                sb.AppendLine();
            }

            var relevantFromOlderChats = await FindRelevantOlderChatSnippetsAsync(
                studentId, currentChatId, currentQuestion, store);

            if (relevantFromOlderChats.Count > 0)
            {
                sb.AppendLine("RELEVANT PRIOR CHAT CONTEXT:");
                foreach (var hit in relevantFromOlderChats)
                    sb.AppendLine($"- {hit}");
                sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        public async Task UpdateSummaryAsync(
            int studentId,
            string chatId,
            List<ChatMessage> messages,
            IChatLogStore store)
        {
            if (messages.Count == 0)
            {
                await store.SaveSummaryAsync(studentId, chatId, "");
                return;
            }

            var summary = BuildDeterministicSummary(messages);
            await store.SaveSummaryAsync(studentId, chatId, summary);
        }

        private async Task<List<string>> FindRelevantOlderChatSnippetsAsync(
            int studentId,
            string currentChatId,
            string currentQuestion,
            IChatLogStore store)
        {
            var results = new List<(double score, string text)>();
            var qTokens = Tokenize(currentQuestion).Where(t => t.Length >= 3).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (qTokens.Count == 0)
                return new List<string>();

            var chats = await store.GetChatsForStudentAsync(studentId);

            foreach (var chat in chats.Where(c => c.ChatId != currentChatId))
            {
                var messages = await store.LoadAsync(chat.ChatId);

                foreach (var msg in messages.Where(m => !string.IsNullOrWhiteSpace(m.Content)))
                {
                    var tokens = Tokenize(msg.Content).Where(t => t.Length >= 3).ToList();
                    if (tokens.Count == 0) continue;

                    int overlap = tokens.Count(t => qTokens.Contains(t));
                    if (overlap == 0) continue;

                    var score = overlap / Math.Sqrt(tokens.Count);
                    if (score <= 0) continue;

                    var label = msg.Role == "assistant" ? "Advisor" : "Student";
                    var text = $"{chat.Title}: {label} said \"{TrimForPrompt(msg.Content, 180)}\"";
                    results.Add((score, text));
                }

                if (!string.IsNullOrWhiteSpace(chat.Summary))
                {
                    var tokens = Tokenize(chat.Summary).Where(t => t.Length >= 3).ToList();
                    if (tokens.Count > 0)
                    {
                        int overlap = tokens.Count(t => qTokens.Contains(t));
                        if (overlap > 0)
                        {
                            var score = overlap / Math.Sqrt(tokens.Count) + 0.15;
                            results.Add((score, $"{chat.Title}: Summary says \"{TrimForPrompt(chat.Summary, 220)}\""));
                        }
                    }
                }
            }

            return results
                .OrderByDescending(r => r.score)
                .Take(4)
                .Select(r => r.text)
                .ToList();
        }

        private static string BuildDeterministicSummary(List<ChatMessage> messages)
        {
            var sb = new StringBuilder();

            var userQuestions = messages
                .Where(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(8)
                .Select(m => TrimForPrompt(m.Content, 160))
                .ToList();

            var courseCodes = messages
                .SelectMany(m => CourseCodeRx.Matches(m.Content ?? "").Select(x => NormalizeCourseCode(x.Value)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            var lastAssistant = messages.LastOrDefault(m => m.Role == "assistant" && !string.IsNullOrWhiteSpace(m.Content));

            sb.AppendLine("This conversation has discussed:");
            foreach (var q in userQuestions)
                sb.AppendLine($"- {q}");

            if (courseCodes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Courses mentioned:");
                foreach (var c in courseCodes)
                    sb.AppendLine($"- {c}");
            }

            if (lastAssistant != null)
            {
                sb.AppendLine();
                sb.AppendLine("Most recent advisor answer:");
                sb.AppendLine($"- {TrimForPrompt(lastAssistant.Content, 220)}");
            }

            return sb.ToString().Trim();
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            foreach (Match m in TokenRx.Matches(text ?? ""))
                yield return m.Value;
        }

        private static string NormalizeCourseCode(string raw)
        {
            raw = raw.Trim().ToUpperInvariant();
            raw = Regex.Replace(raw, @"\s+", " ");
            raw = Regex.Replace(raw, @"^([A-Z]{2,5})(\d{3})$", "$1 $2");
            return raw;
        }

        private static string TrimForPrompt(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text[..max] + "...";
        }
    }
}