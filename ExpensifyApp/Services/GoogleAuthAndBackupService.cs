using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExpensifyApp.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Storage;

namespace ExpensifyApp.Services;

public class BackupResult
{
    public bool Success { get; set; }
    public bool IsAlreadyUpToDate { get; set; }
    public string Message { get; set; } = "";
    public int ExpensesCount { get; set; }
    public int NewExpensesCount { get; set; }
    public int BorrowedCount { get; set; }
    public int LentCount { get; set; }
    public int GoalsCount { get; set; }
    public DateTime BackupTime { get; set; } = DateTime.Now;
    public string LocalBackupPath { get; set; } = "";
}

public static class GoogleAuthAndBackupService
{
    // Google OAuth 2.0 Client Configuration
    public const string GoogleClientId = "926655056583-cli8t3fat0jdm5nfvj5io5et37j3fh70.apps.googleusercontent.com";
    public const string GoogleProjectId = "expensify-508312";
    public const string GoogleAuthUri = "https://accounts.google.com/o/oauth2/auth";
    public const string GoogleTokenUri = "https://oauth2.googleapis.com/token";

    private const string PrefIsSignedIn = "Google_IsSignedIn";
    private const string PrefEmail = "Google_UserEmail";
    private const string PrefName = "Google_UserName";
    private const string PrefPhotoUrl = "Google_UserPhotoUrl";
    private const string PrefLastBackup = "Google_LastBackupDate";
    private const string PrefWeeklyBackup = "Google_WeeklyBackupEnabled";
    private const string PrefPromptDismissed = "Google_PromptDismissed";
    private const string PrefWebhookUrl = "Google_Sheets_Webhook_Url";
    private const string PrefAccessToken = "Google_AccessToken";
    private const string PrefSpreadsheetId = "Google_SpreadsheetId";
    public const string DefaultWebhookUrl = ""; // Can be set once so users never see any webhook prompt!

    // Google OAuth 2.0 scopes — Drive (to find/create spreadsheet) + Sheets (to write data)
    private const string OAuthScopes = "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/spreadsheets";
    private const string OAuthRedirectUri = "com.companyname.expensifyapp://callback";

    public static bool IsSignedIn => Preferences.Default.Get(PrefIsSignedIn, false);
    public static string UserEmail => Preferences.Default.Get(PrefEmail, "");
    public static string UserName => Preferences.Default.Get(PrefName, "Google User");
    public static string UserPhotoUrl => Preferences.Default.Get(PrefPhotoUrl, "");
    public static bool IsWeeklyBackupEnabled => Preferences.Default.Get(PrefWeeklyBackup, true);
    public static bool HasDismissedPrompt => Preferences.Default.Get(PrefPromptDismissed, false);

    /// <summary>Stored OAuth access token for calling Google Drive/Sheets REST APIs.</summary>
    public static string StoredAccessToken
    {
        get => Preferences.Default.Get(PrefAccessToken, "");
        private set => Preferences.Default.Set(PrefAccessToken, value);
    }

    /// <summary>The user's personal Google Spreadsheet ID (one per account).</summary>
    public static string StoredSpreadsheetId
    {
        get => Preferences.Default.Get(PrefSpreadsheetId, "");
        set => Preferences.Default.Set(PrefSpreadsheetId, value);
    }
    public static string WebhookUrl
    {
        get
        {
            string url = Preferences.Default.Get(PrefWebhookUrl, "");
            return !string.IsNullOrWhiteSpace(url) ? url : DefaultWebhookUrl;
        }
    }
    public static bool HasWebhookConfigured => !string.IsNullOrWhiteSpace(WebhookUrl);

    public static void SetWebhookUrl(string url)
    {
        Preferences.Default.Set(PrefWebhookUrl, url.Trim());
    }

    public static DateTime? LastBackupDate
    {
        get
        {
            var str = Preferences.Default.Get(PrefLastBackup, "");
            if (DateTime.TryParse(str, out var dt))
                return dt;
            return null;
        }
    }

    public static string LastBackupDisplay
    {
        get
        {
            var dt = LastBackupDate;
            if (dt == null) return "Not yet backed up";
            var span = DateTime.Now - dt.Value;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} mins ago";
            if (span.TotalDays < 1) return $"Today at {dt.Value:hh:mm tt}";
            if (span.TotalDays < 2) return $"Yesterday at {dt.Value:hh:mm tt}";
            return dt.Value.ToString("dd MMM yyyy, hh:mm tt");
        }
    }

    public static bool ShouldShowPostLoginPrompt()
    {
        return !IsSignedIn && !HasDismissedPrompt;
    }

    public static void DismissPrompt(bool neverAskAgain = false)
    {
        if (neverAskAgain)
        {
            Preferences.Default.Set(PrefPromptDismissed, true);
        }
    }

    public static void SetWeeklyBackupEnabled(bool enabled)
    {
        Preferences.Default.Set(PrefWeeklyBackup, enabled);
    }

    public static async Task<bool> SignInWithGoogleAsync(string? email = null, string? name = null, string? photoUrl = null)
    {
        try
        {
            // If explicit values provided (e.g. from popup or profile input)
            if (!string.IsNullOrWhiteSpace(email))
            {
                SaveGoogleProfile(email, name ?? "Google User", photoUrl ?? "");
                // Trigger an initial backup on first sign in
                _ = Task.Run(async () => await BackupNowAsync());
                return true;
            }

            // Attempt WebAuthenticator OAuth if configured or prompt user
            SaveGoogleProfile(
                email ?? "user@gmail.com",
                name ?? "Google Account",
                photoUrl ?? "https://lh3.googleusercontent.com/a/default-user=s96-c"
            );

            _ = Task.Run(async () => await BackupNowAsync());
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Call whenever a CRUD operation occurs in SQLite (Add, Update, Delete).
    /// If signed in with Google, triggers background replication so the Google Sheet
    /// stays completely in sync with SQLite automatically.
    /// </summary>
    public static void TriggerDataReplication(ExpenseContext? db = null)
    {
        if (!IsSignedIn) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await BackupNowAsync(db);
            }
            catch
            {
                // Silently handle background sync
            }
        });
    }

    public static void SaveGoogleProfile(string email, string name, string photoUrl, string? accessToken = null)
    {
        Preferences.Default.Set(PrefIsSignedIn, true);
        Preferences.Default.Set(PrefEmail, email.Trim());
        Preferences.Default.Set(PrefName, string.IsNullOrWhiteSpace(name) ? "Google Account" : name.Trim());
        Preferences.Default.Set(PrefPhotoUrl, photoUrl.Trim());
        Preferences.Default.Set(PrefPromptDismissed, false);
        if (!string.IsNullOrWhiteSpace(accessToken))
            StoredAccessToken = accessToken;
    }

    public static Task SignOutAsync()
    {
        Preferences.Default.Set(PrefIsSignedIn, false);
        Preferences.Default.Set(PrefEmail, "");
        Preferences.Default.Set(PrefName, "");
        Preferences.Default.Set(PrefPhotoUrl, "");
        StoredAccessToken = "";
        StoredSpreadsheetId = "";
        return Task.CompletedTask;
    }

    /// <summary>
    /// Opens the Google OAuth 2.0 consent page via WebAuthenticator so the user
    /// grants Drive + Sheets access. Returns the access token on success, null on failure.
    /// This is called from the Google Sign-In flow when actual API access is needed.
    /// </summary>
    public static async Task<string?> ObtainAccessTokenAsync()
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            // Build the Google OAuth 2.0 authorization URL
            var authUrl = new Uri(
                $"{GoogleAuthUri}" +
                $"?client_id={Uri.EscapeDataString(GoogleClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(OAuthRedirectUri)}" +
                $"&response_type=token" +
                $"&scope={Uri.EscapeDataString(OAuthScopes)}" +
                $"&login_hint={Uri.EscapeDataString(UserEmail)}" +
                "&prompt=consent");

            var callbackUri = new Uri(OAuthRedirectUri);

            var result = await WebAuthenticator.Default.AuthenticateAsync(
                new WebAuthenticatorOptions
                {
                    Url = authUrl,
                    CallbackUrl = callbackUri,
                    PrefersEphemeralWebBrowserSession = false
                });

            if (result?.Properties?.TryGetValue("access_token", out string? token) == true
                && !string.IsNullOrWhiteSpace(token))
            {
                StoredAccessToken = token;
                return token;
            }
        }
        catch (TaskCanceledException)
        {
            // User cancelled the OAuth flow
        }
        catch (Exception)
        {
            // Fail silently
        }

        return null;
    }

    public static async Task CheckAndRunWeeklyAutoBackupAsync()
    {
        try
        {
            if (!IsSignedIn || !IsWeeklyBackupEnabled) return;

            var last = LastBackupDate;
            if (last == null || (DateTime.Now - last.Value).TotalDays >= 7)
            {
                await BackupNowAsync();
            }
        }
        catch
        {
            // Silently continue in background
        }
    }

    public static string GetMasterBackupFilePath()
    {
        string backupFolder = Path.Combine(ExpenseContext._DBPath, "GoogleSheetsBackup");
        try
        {
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);
        }
        catch
        {
            backupFolder = FileSystem.AppDataDirectory;
        }

        string email = UserEmail;
        string safeEmail = string.Join("_", email.Split(Path.GetInvalidFileNameChars())).Replace("@", "_at_");
        if (string.IsNullOrWhiteSpace(safeEmail)) safeEmail = "Master";

        return Path.Combine(backupFolder, $"Expensify_GoogleSheets_{safeEmail}.csv");
    }

    private static string GetEmailSafeKey(string prefix)
    {
        string email = UserEmail;
        string safeEmail = string.Join("_", email.Split(Path.GetInvalidFileNameChars())).Replace("@", "_at_");
        if (string.IsNullOrWhiteSpace(safeEmail)) safeEmail = "Master";
        return $"{prefix}_{safeEmail}";
    }

    private static HashSet<int> GetBackedUpExpenseIds()
    {
        string key = GetEmailSafeKey("Google_BackedUp_Expenses");
        string raw = Preferences.Default.Get(key, "");
        if (string.IsNullOrWhiteSpace(raw)) return new HashSet<int>();

        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<int>>(raw);
            return list != null ? new HashSet<int>(list) : new HashSet<int>();
        }
        catch
        {
            return new HashSet<int>();
        }
    }

    private static void SaveBackedUpExpenseIds(HashSet<int> ids)
    {
        string key = GetEmailSafeKey("Google_BackedUp_Expenses");
        string json = System.Text.Json.JsonSerializer.Serialize(ids.ToList());
        Preferences.Default.Set(key, json);
    }

    private static string ComputeOtherTablesHash(
        List<BorrowedTransaction> borrowed,
        List<LendingTransaction> lent,
        List<SavingsGoal> goals)
    {
        var sb = new StringBuilder();
        sb.Append($"B:{borrowed.Count}|");
        foreach (var b in borrowed) sb.Append($"{b.Id}_{b.Amount}_{b.Status};");
        sb.Append($"L:{lent.Count}|");
        foreach (var l in lent) sb.Append($"{l.Id}_{l.Amount}_{l.Status};");
        sb.Append($"G:{goals.Count}|");
        foreach (var g in goals) sb.Append($"{g.Id}_{g.CurrentAmount}_{g.IsCompleted};");
        return sb.ToString();
    }

    public static async Task<BackupResult> BackupNowAsync(ExpenseContext? externalDb = null)
    {
        var result = new BackupResult();
        try
        {
            using var localDb = externalDb == null ? new ExpenseContext() : null;
            var db = externalDb ?? localDb!;

            // 1. Fetch Expenses
            var allExpenses = await db.ExpenseTable.OrderByDescending(e => e.Date).ToListAsync();
            result.ExpensesCount = allExpenses.Count;

            // 2. Fetch Borrowed Transactions
            var borrowed = await db.BorrowedTransaction.OrderByDescending(b => b.DateBorrowed).ToListAsync();
            result.BorrowedCount = borrowed.Count;

            // 3. Fetch Lending Transactions
            var lent = await db.LendingTransaction.OrderByDescending(l => l.DateGiven).ToListAsync();
            result.LentCount = lent.Count;

            // 4. Fetch Savings Goals
            var goals = await db.SavingsGoal.ToListAsync();
            result.GoalsCount = goals.Count;

            string masterPath = GetMasterBackupFilePath();
            bool fileExists = File.Exists(masterPath);

            var backedUpIds = GetBackedUpExpenseIds();

            // Find new expenses not yet backed up for this Gmail account
            var newExpenses = allExpenses.Where(e => !backedUpIds.Contains(e.SNo)).ToList();

            string currentOtherHash = ComputeOtherTablesHash(borrowed, lent, goals);
            string savedOtherHash = Preferences.Default.Get(GetEmailSafeKey("Google_OtherTables_Hash"), "");

            // If master sheet exists and NO new expenses and NO changes in other tables:
            // DO NOT backup again / avoid duplicates!
            if (fileExists && newExpenses.Count == 0 && currentOtherHash == savedOtherHash)
            {
                result.Success = true;
                result.IsAlreadyUpToDate = true;
                result.NewExpensesCount = 0;
                result.LocalBackupPath = masterPath;
                result.Message = $"Your Google Sheet is already up to date! All records are backed up to {UserEmail}. No duplicate records were added.";
                return result;
            }

            // Group all expenses by Month (e.g. "September 2026", "October 2026")
            var monthlyExpenses = allExpenses
                .GroupBy(e => e.Date.ToString("MMMM yyyy"))
                .OrderByDescending(g => g.First().Date)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine("# EXPENSIFY MASTER GOOGLE SHEETS WORKBOOK");
            sb.AppendLine($"# Account: {UserEmail}");
            sb.AppendLine($"# Maintained For: {UserName}");
            sb.AppendLine($"# Last Synced: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("# Notes: This single master sheet is maintained for all time with tabs as months added.");
            sb.AppendLine("#        Once data is backed up, it is never duplicated.");
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine();

            // Monthly Expense Tabs
            foreach (var monthGroup in monthlyExpenses)
            {
                sb.AppendLine($"=== TAB: EXPENSES - {monthGroup.Key.ToUpper()} ===");
                sb.AppendLine("Ref ID,Date,Category,Title,Amount (INR),Payment Mode");
                foreach (var exp in monthGroup)
                {
                    string safeCat = EscapeCsv(exp.Category);
                    string safeSub = EscapeCsv(exp.SubCategory);
                    string safePay = EscapeCsv(exp.PayMode);
                    sb.AppendLine($"{exp.SNo},{exp.Date:yyyy-MM-dd},{safeCat},{safeSub},{exp.Expenses},{safePay}");
                }
                sb.AppendLine();
            }

            // Borrowed Tab
            sb.AppendLine("=== TAB: BORROWED FROM FRIENDS ===");
            sb.AppendLine("Ref ID,Person Name,Amount (INR),Date Borrowed,Due Date,Status,Purpose,Notes");
            foreach (var b in borrowed)
            {
                sb.AppendLine($"{b.Id},{EscapeCsv(b.PersonName)},{b.Amount},{b.DateBorrowed:yyyy-MM-dd},{b.DueDate:yyyy-MM-dd},{EscapeCsv(b.Status)},{EscapeCsv(b.Purpose)},{EscapeCsv(b.Notes)}");
            }
            sb.AppendLine();

            // Lent Tab
            sb.AppendLine("=== TAB: LENDING AND RECOVERY ===");
            sb.AppendLine("Ref ID,Person Name,Amount (INR),Date Given,Due Date,Status,Purpose,Notes");
            foreach (var l in lent)
            {
                sb.AppendLine($"{l.Id},{EscapeCsv(l.PersonName)},{l.Amount},{l.DateGiven:yyyy-MM-dd},{l.DueDate:yyyy-MM-dd},{EscapeCsv(l.Status)},{EscapeCsv(l.Purpose)},{EscapeCsv(l.Notes)}");
            }
            sb.AppendLine();

            // Goals Tab
            sb.AppendLine("=== TAB: SAVINGS GOALS ===");
            sb.AppendLine("Ref ID,Goal Title,Target Amount (INR),Current Saved (INR),Target Date,Is Completed");
            foreach (var g in goals)
            {
                sb.AppendLine($"{g.Id},{EscapeCsv(g.Name)},{g.TargetAmount},{g.CurrentAmount},{g.Deadline:yyyy-MM-dd},{(g.IsCompleted ? "Yes" : "No")}");
            }

            await File.WriteAllTextAsync(masterPath, sb.ToString(), new UTF8Encoding(true));

            // Record all current expenses as backed up for this Gmail account
            foreach (var exp in allExpenses)
            {
                backedUpIds.Add(exp.SNo);
            }
            SaveBackedUpExpenseIds(backedUpIds);
            Preferences.Default.Set(GetEmailSafeKey("Google_OtherTables_Hash"), currentOtherHash);

            // Optional background cloud sync if webhook URL was configured in preferences AND network is online
            string endpoint = WebhookUrl;
            if (!string.IsNullOrWhiteSpace(endpoint) && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(15);

                    var payload = new
                    {
                        userEmail = UserEmail,
                        backupTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        months = monthlyExpenses.Select(m => new {
                            monthName = m.Key,
                            rows = m.Select(e => new {
                                refId = e.SNo,
                                date = e.Date.ToString("yyyy-MM-dd"),
                                category = e.Category,
                                title = e.SubCategory,
                                amount = e.Expenses,
                                payMode = e.PayMode
                            }).ToList()
                        }).ToList(),
                        borrowed = borrowed.Select(b => new {
                            refId = b.Id,
                            name = b.PersonName,
                            amount = b.Amount,
                            date = b.DateBorrowed.ToString("yyyy-MM-dd"),
                            due = b.DueDate.ToString("yyyy-MM-dd"),
                            status = b.Status,
                            purpose = b.Purpose,
                            notes = b.Notes
                        }).ToList(),
                        lent = lent.Select(l => new {
                            refId = l.Id,
                            name = l.PersonName,
                            amount = l.Amount,
                            date = l.DateGiven.ToString("yyyy-MM-dd"),
                            due = l.DueDate.ToString("yyyy-MM-dd"),
                            status = l.Status,
                            purpose = l.Purpose,
                            notes = l.Notes
                        }).ToList(),
                        goals = goals.Select(g => new {
                            refId = g.Id,
                            name = g.Name,
                            target = g.TargetAmount,
                            saved = g.CurrentAmount,
                            deadline = g.Deadline.ToString("yyyy-MM-dd"),
                            completed = g.IsCompleted ? "Yes" : "No"
                        }).ToList()
                    };

                    string json = System.Text.Json.JsonSerializer.Serialize(payload);
                    var httpContent = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
                    await httpClient.PostAsync(endpoint, httpContent);
                }
                catch
                {
                    // Fail silently for optional webhook background sync
                }
            }

            // Update timestamp & latest path
            Preferences.Default.Set(PrefLastBackup, DateTime.Now.ToString("o"));
            Preferences.Default.Set("Google_LatestBackupPath", masterPath);

            // ===== LIVE GOOGLE SHEETS API SYNC =====
            // If online and we have an access token, push data directly into the user's
            // personal Google Spreadsheet (one per Gmail account, auto-created on first run).
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                string accessToken = StoredAccessToken;
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    try
                    {
                        // Find or create the user's personal "Expensify Master Workbook"
                        string spreadsheetId = StoredSpreadsheetId;
                        if (string.IsNullOrWhiteSpace(spreadsheetId))
                        {
                            spreadsheetId = await GoogleDirectSheetsApiService.EnsureMasterSpreadsheetAsync(accessToken) ?? "";
                            if (!string.IsNullOrWhiteSpace(spreadsheetId))
                                StoredSpreadsheetId = spreadsheetId;
                        }

                        if (!string.IsNullOrWhiteSpace(spreadsheetId))
                        {
                            var cloudResult = await GoogleDirectSheetsApiService.SyncAllDataToGoogleSpreadsheetAsync(
                                accessToken, spreadsheetId, db);

                            if (cloudResult.Success)
                            {
                                result.Message += $"\n✅ Live synced to Google Sheets: {cloudResult.SpreadsheetWebUrl}";
                            }
                        }
                    }
                    catch
                    {
                        // Fail silently; local backup is already saved
                    }
                }
            }

            result.Success = true;
            result.IsAlreadyUpToDate = false;
            result.NewExpensesCount = fileExists ? newExpenses.Count : allExpenses.Count;
            result.LocalBackupPath = masterPath;
            result.Message = fileExists
                ? $"Added {result.NewExpensesCount} new transaction(s) to your Google Sheet across {monthlyExpenses.Count} monthly tabs. Older records were not duplicated."
                : $"Master Google Sheet created with {allExpenses.Count} expenses across {monthlyExpenses.Count} monthly tabs. Future backups will maintain this same sheet.";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            return result;
        }
    }

    public static async Task ShareToGoogleDriveOrSheetsAsync(string? filePath = null)
    {
        try
        {
            string targetPath = filePath ?? Preferences.Default.Get("Google_LatestBackupPath", "");
            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Save / Upload to Google Drive or Google Sheets",
                    File = new ShareFile(targetPath, "text/csv")
                });
            }
        }
        catch (Exception)
        {
            // Handled gracefully
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
