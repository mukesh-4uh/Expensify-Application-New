using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using ExpensifyApp.Services;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ExpensifyApp.Pages;

public class HistoryGroup : ObservableCollection<HistoryExpenseItem>
{
    public string GroupTitle { get; set; } = "";
    public string GroupDate { get; set; } = "";
    public HistoryGroup(string title, string date) : base()
    {
        GroupTitle = title;
        GroupDate = date;
    }
}

public class HistoryExpenseItem
{
    public int SNo { get; set; }
    public string Category { get; set; } = "";
    public string SubCategory { get; set; } = "";
    public string PayMode { get; set; } = "";
    public decimal Expenses { get; set; }
    public DateTime Date { get; set; }
    public string CategoryEmoji { get; set; } = "📌";
    public Color IconBg { get; set; } = Colors.LightGray;
    public string AmountDisplay { get; set; } = "";
    public string SubCategoryDisplay { get; set; } = "";
    public string TimeDisplay { get; set; } = "";
    public string DateStr { get; set; } = "";
}

public partial class HistoryPage : ContentPage
{
    private readonly ExpenseContext _db;
    private string _currentFilter = "All";
    private string _searchQuery = "";
    private string _currentSort = "Newest";
    private DateTime _selectedFilterDate = DateTime.Today;
    
    public ObservableCollection<HistoryGroup> GroupedHistory { get; set; } = new();
    public ICommand DeleteCommand { get; private set; }
    public ICommand EditCommand { get; private set; }
    public ICommand DuplicateCommand { get; private set; }
    public ICommand RecurringCommand { get; private set; }

    public HistoryPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        BindingContext = this;

        DeleteCommand = new Command<HistoryExpenseItem>(async item => await DeleteItem(item));
        EditCommand = new Command<HistoryExpenseItem>(async item => await EditItem(item));
        DuplicateCommand = new Command<HistoryExpenseItem>(async item => await DuplicateItem(item));
        RecurringCommand = new Command<HistoryExpenseItem>(async item => await SetRecurring(item));

        Appearing += async (s, e) =>
        {
            await EnsureRecurringTableExists();
            await ProcessRecurringExpenses();
            SetActivePill("All");
            await LoadHistory();
        };
    }

    // ─────────────────────────────────────────────────
    // RECURRING TABLE CREATION (safety net)
    // ─────────────────────────────────────────────────
    private async Task EnsureRecurringTableExists()
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS RecurringExpenseTable (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Category TEXT NOT NULL DEFAULT '',
                    SubCategory TEXT NOT NULL DEFAULT '',
                    PayMode TEXT NOT NULL DEFAULT '',
                    Amount INTEGER NOT NULL DEFAULT 0,
                    DayOfMonth INTEGER NOT NULL DEFAULT 1,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );");
        }
        catch { /* table may already exist */ }
    }

    // ─────────────────────────────────────────────────
    // AUTO-CREATE RECURRING EXPENSES FOR TODAY
    // ─────────────────────────────────────────────────
    private async Task ProcessRecurringExpenses()
    {
        try
        {
            var today = DateTime.Today;
            int todayDay = today.Day;

            var activeRecurrings = await _db.RecurringExpenseTable
                .Where(r => r.IsActive && r.DayOfMonth == todayDay)
                .ToListAsync();

            if (!activeRecurrings.Any()) return;

            var todayExpenses = await _db.ExpenseTable
                .Where(e => e.Date.Date == today)
                .ToListAsync();

            foreach (var rec in activeRecurrings)
            {
                // Check if an expense with the same category, subcategory, and amount already exists today
                bool alreadyExists = todayExpenses.Any(e =>
                    e.Category == rec.Category &&
                    e.SubCategory == rec.SubCategory &&
                    e.Expenses == rec.Amount);

                if (!alreadyExists)
                {
                    var newExpense = new ExpenseTable
                    {
                        Category = rec.Category,
                        SubCategory = rec.SubCategory,
                        PayMode = rec.PayMode,
                        Expenses = rec.Amount,
                        Date = DateTime.Now
                    };
                    _db.ExpenseTable.Add(newExpense);
                }
            }

            await _db.SaveChangesAsync();
        }
        catch { /* silently handle — recurring is a convenience feature */ }
    }

    // ─────────────────────────────────────────────────
    // EDIT COMMAND — uses EditExpensePopup
    // ─────────────────────────────────────────────────
    private async Task EditItem(HistoryExpenseItem item)
    {
        if (item == null) return;

        try
        {
            var popup = new EditExpensePopup(
                item.Category,
                item.SubCategory,
                (int)item.Expenses,
                item.Date);

            var result = await this.ShowPopupAsync(popup);

            if (result is not EditExpenseResult editResult) return;

            // Update the database record
            var row = await _db.ExpenseTable.FirstOrDefaultAsync(e => e.SNo == item.SNo);
            if (row != null)
            {
                row.Expenses = editResult.Amount;
                row.Date = editResult.Date;
                _db.ExpenseTable.Update(row);
                await _db.SaveChangesAsync();
                GoogleAuthAndBackupService.TriggerDataReplication(_db);
                await DisplayAlert("Success", "Expense updated successfully! ✅", "OK");
            }

            await LoadHistory();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ─────────────────────────────────────────────────
    // DUPLICATE COMMAND — uses DuplicateExpensePopup
    // ─────────────────────────────────────────────────
    private async Task DuplicateItem(HistoryExpenseItem item)
    {
        if (item == null) return;

        var popup = new DuplicateExpensePopup(
            item.Category,
            item.SubCategory,
            (int)item.Expenses);

        var result = await this.ShowPopupAsync(popup);
        if (result is not DuplicateExpenseResult dupResult) return;

        try
        {
            var original = await _db.ExpenseTable.FirstOrDefaultAsync(e => e.SNo == item.SNo);
            if (original == null)
            {
                await DisplayAlert("Error", "Original expense not found.", "OK");
                return;
            }

            var duplicate = new ExpenseTable
            {
                Category = original.Category,
                SubCategory = original.SubCategory,
                PayMode = original.PayMode,
                Expenses = original.Expenses,
                Date = dupResult.Date.Date.Add(DateTime.Now.TimeOfDay)
            };

            _db.ExpenseTable.Add(duplicate);
            await _db.SaveChangesAsync();

            // Run round-up check
            await AutoSaveHelper.HandleRoundUp(_db, duplicate.Expenses);

            await DisplayAlert("Duplicated ✅", $"Expense duplicated successfully!\n₹{original.Expenses:N0} • {original.Category}", "OK");
            await LoadHistory();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ─────────────────────────────────────────────────
    // RECURRING COMMAND — uses RecurringPopup
    // ─────────────────────────────────────────────────
    private async Task SetRecurring(HistoryExpenseItem item)
    {
        if (item == null) return;

        try
        {
            int originalDay = item.Date.Day;

            var popup = new RecurringPopup(
                item.Category,
                (int)item.Expenses,
                originalDay);

            var result = await this.ShowPopupAsync(popup);

            if (result is not RecurringResult recurringResult) return;

            int dayOfMonth = recurringResult.DayOfMonth;

            await EnsureRecurringTableExists();

            // Check if a similar recurring already exists
            var existing = await _db.RecurringExpenseTable
                .FirstOrDefaultAsync(r =>
                    r.Category == item.Category &&
                    r.SubCategory == (item.SubCategory ?? "") &&
                    r.Amount == (int)item.Expenses &&
                    r.DayOfMonth == dayOfMonth &&
                    r.IsActive);

            if (existing != null)
            {
                await DisplayAlert("Already Recurring",
                    $"This expense is already set as recurring on day {dayOfMonth} of every month.", "OK");
                return;
            }

            var recurring = new RecurringExpenseTable
            {
                Category = item.Category,
                SubCategory = item.SubCategory ?? "",
                PayMode = item.PayMode ?? "Cash",
                Amount = (int)item.Expenses,
                DayOfMonth = dayOfMonth,
                IsActive = true
            };

            _db.RecurringExpenseTable.Add(recurring);
            await _db.SaveChangesAsync();

            await DisplayAlert("Recurring Set ✅",
                $"This expense will appear on the {GetDaySuffix(dayOfMonth)} of every month.\n\n📂 {item.Category}\n💰 ₹{item.Expenses:N0}",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private static string GetDaySuffix(int day)
    {
        if (day >= 11 && day <= 13) return $"{day}th";
        return (day % 10) switch
        {
            1 => $"{day}st",
            2 => $"{day}nd",
            3 => $"{day}rd",
            _ => $"{day}th"
        };
    }

    // ─────────────────────────────────────────────────
    // LOAD HISTORY
    // ─────────────────────────────────────────────────
    private async Task LoadHistory()
    {
        try
        {
            var all = await _db.ExpenseTable.ToListAsync();
            ApplyFilterAndSort(all);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void ApplyFilterAndSort(List<ExpenseTable> all)
    {
        var today = DateTime.Today;
        var filtered = all.AsEnumerable();

        // 1. Period Filter
        if (_currentFilter == "Today")
            filtered = filtered.Where(e => e.Date.Date == today);
        else if (_currentFilter == "Week")
            filtered = filtered.Where(e => e.Date.Date >= today.AddDays(-7));
        else if (_currentFilter == "Month")
            filtered = filtered.Where(e => e.Date.Year == today.Year && e.Date.Month == today.Month);
        else if (_currentFilter == "Date")
            filtered = filtered.Where(e => e.Date.Date == _selectedFilterDate.Date);

        // 2. Search
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            filtered = filtered.Where(e => 
                (e.Category != null && e.Category.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                (e.SubCategory != null && e.SubCategory.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                (e.PayMode != null && e.PayMode.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
            );
        }

        var filteredList = filtered.ToList();

        // Summary
        decimal totalSpent = filteredList.Sum(e => e.Expenses);
        summarySpentLabel.Text = $"-₹{totalSpent:N0}";
        summaryIncomeLabel.Text = "₹0"; // Static income for now
        summaryCountLabel.Text = filteredList.Count.ToString();
        

        // 3. Sort
        if (_currentSort == "Newest")
            filteredList = filteredList.OrderByDescending(e => e.Date).ThenByDescending(e => e.SNo).ToList();
        else if (_currentSort == "Oldest")
            filteredList = filteredList.OrderBy(e => e.Date).ThenBy(e => e.SNo).ToList();
        else if (_currentSort == "Highest amount")
            filteredList = filteredList.OrderByDescending(e => e.Expenses).ToList();
        else if (_currentSort == "Lowest amount")
            filteredList = filteredList.OrderBy(e => e.Expenses).ToList();

        // Group by date or by rank
        GroupedHistory.Clear();

        if (_currentSort == "Highest amount" || _currentSort == "Lowest amount")
        {
            // Single group for amount sorting
            var group = new HistoryGroup("Ranked by Amount", "");
            
            var items = filteredList.AsEnumerable();
            if (_currentSort == "Highest amount") items = items.OrderByDescending(x => x.Expenses);
            else items = items.OrderBy(x => x.Expenses);

            foreach (var e in items)
            {
                group.Add(CreateHistoryExpenseItem(e));
            }
            GroupedHistory.Add(group);
        }
        else
        {
            // Group by date
            var groups = filteredList.GroupBy(e => e.Date.Date);
            groups = _currentSort == "Oldest" ? groups.OrderBy(g => g.Key) : groups.OrderByDescending(g => g.Key);

            foreach (var grp in groups)
            {
                string title = grp.Key == today ? "Today" : grp.Key == today.AddDays(-1) ? "Yesterday" : grp.Key.ToString("dddd, dd MMM");
                var group = new HistoryGroup(title, grp.Key.ToString("MMM dd, yyyy"));
                
                var items = _currentSort == "Oldest" ? grp.OrderBy(x => x.Date).ThenBy(x => x.SNo) : grp.OrderByDescending(x => x.Date).ThenByDescending(x => x.SNo);

                foreach (var e in items)
                {
                    group.Add(CreateHistoryExpenseItem(e));
                }
                GroupedHistory.Add(group);
            }
        }
    }

    private HistoryExpenseItem CreateHistoryExpenseItem(ExpenseTable e)
    {
        return new HistoryExpenseItem
        {
            SNo = e.SNo,
            Category = e.Category ?? "Other",
            SubCategory = e.SubCategory,
            PayMode = e.PayMode ?? "Cash",
            Expenses = e.Expenses,
            Date = e.Date,
            CategoryEmoji = GetEmoji(e.Category),
            IconBg = GetIconBg(e.Category),
            AmountDisplay = $"-₹{e.Expenses:N0}",
            SubCategoryDisplay = string.IsNullOrWhiteSpace(e.SubCategory) ? (e.PayMode ?? "—") : e.SubCategory,
            TimeDisplay = e.Date.ToString("hh:mm tt") + " • " + (e.PayMode ?? "Cash"),
            DateStr = e.Date.ToString("MMM dd, yyyy")
        };
    }


    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = e.NewTextValue;
        _ = LoadHistory();
    }

    private void OnSortChanged(object sender, EventArgs e)
    {
        if (sortPicker.SelectedItem != null)
        {
            _currentSort = sortPicker.SelectedItem.ToString();
            _ = LoadHistory();
        }
    }

    private async Task DeleteItem(HistoryExpenseItem item)
    {
        if (item == null) return;

        var popup = new ConfirmationPopup(
            "Delete Expense 🗑️",
            $"Are you sure you want to delete this expense?\n\n📂 {item.Category}{(string.IsNullOrWhiteSpace(item.SubCategory) ? "" : $" • {item.SubCategory}")}\n💰 ₹{item.Expenses:N0}\n📅 {item.Date:dd MMM yyyy}",
            "This action cannot be undone",
            "Delete",
            "Cancel",
            isDestructive: true);

        var result = await this.ShowPopupAsync(popup);
        if (result is not bool confirm || !confirm) return;

        var row = await _db.ExpenseTable.FirstOrDefaultAsync(e => e.SNo == item.SNo);
        if (row != null)
        {
            _db.ExpenseTable.Remove(row);
            await _db.SaveChangesAsync();
            GoogleAuthAndBackupService.TriggerDataReplication(_db);
        }

        await LoadHistory();
    }

    private void SetActivePill(string filter)
    {
        _currentFilter = filter;
        
        var activeBg = Color.FromArgb("#0D8C87");
        var inactiveBg = Colors.White;
        
        var activeText = Colors.White;
        var inactiveText = Color.FromArgb("#1C2340");
        var inactiveStroke = Color.FromArgb("#E5E7EB");
        
        // Reset all
        ResetPill(pillAll, inactiveBg, inactiveText, inactiveStroke);
        ResetPill(pillToday, inactiveBg, inactiveText, inactiveStroke);
        ResetPill(pillMonth, inactiveBg, inactiveText, inactiveStroke);
        ResetPill(pillDate, inactiveBg, inactiveText, inactiveStroke);
        lblDateFilter.TextColor = inactiveText;
        
        // Set Active
        switch(filter)
        {
            case "All": SetPill(pillAll, activeBg, activeText); break;
            case "Today": SetPill(pillToday, activeBg, activeText); break;
            case "Month": SetPill(pillMonth, activeBg, activeText); break;
            case "Date": 
                SetPill(pillDate, activeBg, activeText);
                lblDateFilter.TextColor = activeText;
                break;
        }
    }

    private void ResetPill(Border pill, Color bg, Color txt, Color stroke)
    {
        pill.BackgroundColor = bg;
        pill.StrokeThickness = 1;
        pill.Stroke = stroke;
        if (pill.Content is Label lbl) lbl.TextColor = txt;
    }

    private void SetPill(Border pill, Color bg, Color txt)
    {
        pill.BackgroundColor = bg;
        pill.StrokeThickness = 0;
        if (pill.Content is Label lbl) lbl.TextColor = txt;
    }

    private async void OnPillAll(object sender, EventArgs e) { SetActivePill("All"); await LoadHistory(); }
    private async void OnPillToday(object sender, EventArgs e) { SetActivePill("Today"); await LoadHistory(); }
    private async void OnPillMonth(object sender, EventArgs e) { SetActivePill("Month"); await LoadHistory(); }

    private void OnPillDateTapped(object sender, EventArgs e)
    {
        datePicker.Focus();
    }

    private async void OnFilterDateSelected(object sender, DateChangedEventArgs e)
    {
        _selectedFilterDate = e.NewDate;
        lblDateFilter.Text = "📅 " + _selectedFilterDate.ToString("MMM d");
        SetActivePill("Date");
        await LoadHistory();
    }

    private async void OnBackTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new DashboardPage());

    private async void OnDashboardTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new DashboardPage());

    private async void OnStatsTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new StatsPage());

    private async void OnProfileTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new ProfilePage());

    private static string GetEmoji(string category) => category?.ToLower() switch
    {
        "food" => "🍔",
        "groceries" => "🛒",
        "travel" => "✈️",
        "shopping" => "🛍️",
        "education" => "📚",
        "medicine" => "💊",
        "entertainment" => "🎬",
        "rent" => "🏠",
        "savings" => "💰",
        "loan" => "💳",
        "lending" => "🤝",
        _ => "📌"
    };

    private static Color GetIconBg(string category) => category?.ToLower() switch
    {
        "food" => Color.FromArgb("#FFF3E0"),
        "groceries" => Color.FromArgb("#E8F5E9"),
        "travel" => Color.FromArgb("#E3F2FD"),
        "shopping" => Color.FromArgb("#FCE4EC"),
        "education" => Color.FromArgb("#F3E5F5"),
        "medicine" => Color.FromArgb("#E8F5E9"),
        "entertainment" => Color.FromArgb("#FFF8E1"),
        "rent" => Color.FromArgb("#E8EAF6"),
        "savings" => Color.FromArgb("#FFFDE7"),
        "loan" => Color.FromArgb("#FCE4EC"),
        "lending" => Color.FromArgb("#E0F2F1"),
        _ => Color.FromArgb("#EEF0FF")
    };

    private async void addButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new MenuPage());
        }
        catch(System.Exception ex)
        {
            await ExpensifyApp.Helpers.UIHelper.HandleException(ex);
        }
    }
}

