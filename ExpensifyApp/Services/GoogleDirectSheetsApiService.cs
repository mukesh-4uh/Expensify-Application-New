using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ExpensifyApp.DataBase;
using Microsoft.EntityFrameworkCore;

namespace ExpensifyApp.Services;

public class CloudSyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string SpreadsheetId { get; set; } = "";
    public string SpreadsheetWebUrl { get; set; } = "";
}

public static class GoogleDirectSheetsApiService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static string GetSpreadsheetWebUrl(string spreadsheetId)
    {
        if (string.IsNullOrWhiteSpace(spreadsheetId)) return "https://docs.google.com/spreadsheets";
        return $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/edit";
    }

    /// <summary>
    /// Searches the user's personal Google Drive for "Expensify Master Workbook".
    /// If not found, creates a new spreadsheet directly in their Google account.
    /// Returns the Google Spreadsheet ID.
    /// </summary>
    public static async Task<string?> EnsureMasterSpreadsheetAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;

        try
        {
            // 1. Check if user already has an "Expensify Master Workbook" in Google Drive
            using var searchRequest = new HttpRequestMessage(HttpMethod.Get,
                "https://www.googleapis.com/drive/v3/files?q=name='Expensify Master Workbook' and mimeType='application/vnd.google-apps.spreadsheet' and trashed=false&fields=files(id,name,webViewLink)");
            searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var searchResponse = await _httpClient.SendAsync(searchRequest);
            if (searchResponse.IsSuccessStatusCode)
            {
                var searchJson = await searchResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(searchJson);
                if (doc.RootElement.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
                {
                    string existingId = files[0].GetProperty("id").GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(existingId))
                    {
                        return existingId;
                    }
                }
            }

            // 2. Not found: Create a brand new Google Spreadsheet in their account
            using var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://sheets.googleapis.com/v4/spreadsheets");
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var createPayload = new
            {
                properties = new
                {
                    title = "Expensify Master Workbook"
                },
                sheets = new[]
                {
                    new
                    {
                        properties = new
                        {
                            title = "Expenses Summary",
                            gridProperties = new { rowCount = 100, columnCount = 10 }
                        }
                    }
                }
            };

            createRequest.Content = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");

            var createResponse = await _httpClient.SendAsync(createRequest);
            if (createResponse.IsSuccessStatusCode)
            {
                var createJson = await createResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(createJson);
                if (doc.RootElement.TryGetProperty("spreadsheetId", out var idProp))
                {
                    return idProp.GetString();
                }
            }
        }
        catch (Exception)
        {
            // Fail gracefully
        }

        return null;
    }

    /// <summary>
    /// Live syncs all SQLite records directly into the user's online Google Spreadsheet.
    /// Creates monthly tabs, Borrowed, Lent, and Goals tabs in their sheet.
    /// </summary>
    public static async Task<CloudSyncResult> SyncAllDataToGoogleSpreadsheetAsync(
        string accessToken,
        string spreadsheetId,
        ExpenseContext db)
    {
        var result = new CloudSyncResult { SpreadsheetId = spreadsheetId };

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(spreadsheetId))
        {
            result.Success = false;
            result.Message = "Missing Google authorization token or Spreadsheet ID.";
            return result;
        }

        try
        {
            // Fetch SQLite data
            var allExpenses = await db.ExpenseTable.OrderByDescending(e => e.Date).ToListAsync();
            var borrowed = await db.BorrowedTransaction.OrderByDescending(b => b.DateBorrowed).ToListAsync();
            var lent = await db.LendingTransaction.OrderByDescending(l => l.DateGiven).ToListAsync();
            var goals = await db.SavingsGoal.ToListAsync();

            // Group expenses by Month (e.g. "September 2026")
            var monthlyExpenses = allExpenses
                .GroupBy(e => e.Date.ToString("MMMM yyyy"))
                .OrderByDescending(g => g.First().Date)
                .ToList();

            // Ensure sheets/tabs exist in the spreadsheet
            var existingTabs = await GetSpreadsheetTabNamesAsync(accessToken, spreadsheetId);
            var tabsToCreate = new List<string>();

            foreach (var m in monthlyExpenses)
            {
                string tabName = $"EXP - {m.Key}";
                if (!existingTabs.Contains(tabName))
                    tabsToCreate.Add(tabName);
            }

            if (!existingTabs.Contains("BORROWED")) tabsToCreate.Add("BORROWED");
            if (!existingTabs.Contains("LENT")) tabsToCreate.Add("LENT");
            if (!existingTabs.Contains("SAVINGS GOALS")) tabsToCreate.Add("SAVINGS GOALS");

            if (tabsToCreate.Count > 0)
            {
                await AddTabsToSpreadsheetAsync(accessToken, spreadsheetId, tabsToCreate);
            }

            // Prepare batch value data
            var valueData = new List<object>();

            // Monthly Expense Tabs
            foreach (var m in monthlyExpenses)
            {
                string tabName = $"EXP - {m.Key}";
                var rows = new List<List<object>>
                {
                    new() { "Transaction ID", "Date", "Category", "Title", "Amount (INR)", "Payment Mode" }
                };

                foreach (var exp in m)
                {
                    rows.Add(new() { exp.SNo, exp.Date.ToString("yyyy-MM-dd"), exp.Category ?? "", exp.SubCategory ?? "", exp.Expenses, exp.PayMode ?? "" });
                }

                valueData.Add(new
                {
                    range = $"'{tabName}'!A1:F{rows.Count}",
                    majorDimension = "ROWS",
                    values = rows
                });
            }

            // Borrowed Tab
            var borrowedRows = new List<List<object>>
            {
                new() { "ID", "Person Name", "Amount (INR)", "Date Borrowed", "Due Date", "Status", "Purpose", "Notes" }
            };
            foreach (var b in borrowed)
            {
                borrowedRows.Add(new() { b.Id, b.PersonName ?? "", b.Amount, b.DateBorrowed.ToString("yyyy-MM-dd"), b.DueDate.ToString("yyyy-MM-dd"), b.Status ?? "", b.Purpose ?? "", b.Notes ?? "" });
            }
            valueData.Add(new
            {
                range = "'BORROWED'!A1:H" + borrowedRows.Count,
                majorDimension = "ROWS",
                values = borrowedRows
            });

            // Lent Tab
            var lentRows = new List<List<object>>
            {
                new() { "ID", "Person Name", "Amount (INR)", "Date Given", "Due Date", "Status", "Purpose", "Notes" }
            };
            foreach (var l in lent)
            {
                lentRows.Add(new() { l.Id, l.PersonName ?? "", l.Amount, l.DateGiven.ToString("yyyy-MM-dd"), l.DueDate.ToString("yyyy-MM-dd"), l.Status ?? "", l.Purpose ?? "", l.Notes ?? "" });
            }
            valueData.Add(new
            {
                range = "'LENT'!A1:H" + lentRows.Count,
                majorDimension = "ROWS",
                values = lentRows
            });

            // Savings Goals Tab
            var goalsRows = new List<List<object>>
            {
                new() { "ID", "Goal Title", "Target Amount (INR)", "Current Saved (INR)", "Deadline", "Status" }
            };
            foreach (var g in goals)
            {
                goalsRows.Add(new() { g.Id, g.Name ?? "", g.TargetAmount, g.CurrentAmount, g.Deadline.ToString("yyyy-MM-dd"), (g.IsCompleted ? "Completed" : "In Progress") });
            }
            valueData.Add(new
            {
                range = "'SAVINGS GOALS'!A1:F" + goalsRows.Count,
                majorDimension = "ROWS",
                values = goalsRows
            });

            // Execute Batch Update to Google Sheets API
            using var updateRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}/values:batchUpdate");
            updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var batchBody = new
            {
                valueInputOption = "USER_ENTERED",
                data = valueData
            };

            updateRequest.Content = new StringContent(JsonSerializer.Serialize(batchBody), Encoding.UTF8, "application/json");

            var updateResponse = await _httpClient.SendAsync(updateRequest);
            if (updateResponse.IsSuccessStatusCode)
            {
                result.Success = true;
                result.SpreadsheetWebUrl = GetSpreadsheetWebUrl(spreadsheetId);
                result.Message = $"Synced {allExpenses.Count} expenses across {monthlyExpenses.Count} monthly tabs to your Google Sheets account!";
            }
            else
            {
                string errContent = await updateResponse.Content.ReadAsStringAsync();
                result.Success = false;
                result.Message = $"Google Sheets update returned: {updateResponse.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    private static async Task<HashSet<string>> GetSpreadsheetTabNamesAsync(string accessToken, string spreadsheetId)
    {
        var tabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}?fields=sheets.properties.title");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var res = await _httpClient.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("sheets", out var sheets))
                {
                    foreach (var s in sheets.EnumerateArray())
                    {
                        if (s.TryGetProperty("properties", out var props) &&
                            props.TryGetProperty("title", out var title))
                        {
                            string t = title.GetString() ?? "";
                            if (!string.IsNullOrEmpty(t)) tabs.Add(t);
                        }
                    }
                }
            }
        }
        catch { }
        return tabs;
    }

    private static async Task AddTabsToSpreadsheetAsync(string accessToken, string spreadsheetId, List<string> newTabs)
    {
        try
        {
            var requests = new List<object>();
            foreach (var tab in newTabs)
            {
                requests.Add(new
                {
                    addSheet = new
                    {
                        properties = new
                        {
                            title = tab,
                            gridProperties = new { rowCount = 100, columnCount = 10 }
                        }
                    }
                });
            }

            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}:batchUpdate");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var body = new { requests };
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            await _httpClient.SendAsync(req);
        }
        catch { }
    }
}
