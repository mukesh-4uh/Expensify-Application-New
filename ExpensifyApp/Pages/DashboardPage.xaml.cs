using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Shapes;

namespace ExpensifyApp.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly ExpenseContext _db;
    
    private bool _isBudgetMode = true;
    public bool IsBudgetMode
    {
        get => _isBudgetMode;
        set { _isBudgetMode = value; OnPropertyChanged(); }
    }

    private bool _isCommitmentsMode = false;
    public bool IsCommitmentsMode
    {
        get => _isCommitmentsMode;
        set { _isCommitmentsMode = value; OnPropertyChanged(); }
    }

    // Monthly budget — user can set this in profile; default ₹20,000
    private const decimal DefaultBudget = 20000m;

    public ObservableCollection<DashboardCategoryItem> CategoryItems { get; set; } = new();
    public ObservableCollection<DashboardExpenseItem> RecentTransactions { get; set; } = new();

    public DashboardPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        BindingContext = this;

        Appearing += async (s, e) =>
        {
            try
            {
                await AppLoadActivityHelper.DoAppInitWork();
                var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
                if (profile == null)
                {
                    await Navigation.PushAsync(new FinancialSetupPage());
                    return;
                }
                await LoadDashboardData();
            }
            catch
            {
                await LoadDashboardData();
            }
        };
    }

    private async Task LoadDashboardData()
    {
        try
        {
            var targetDate = DateTime.Today;
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1);
            var monthEnd   = monthStart.AddMonths(1).AddDays(-1);

            var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
            if (profile == null) return;

            // Set Mode Bindings
            IsBudgetMode = profile.DashboardMode == "Monthly budget";
            IsCommitmentsMode = profile.DashboardMode == "Monthly commitments";

            var allExpenses = await _db.ExpenseTable.ToListAsync();
            var monthExpenses = allExpenses.Where(e =>
                e.Date.Date >= monthStart.Date && e.Date.Date <= monthEnd.Date).ToList();
            decimal monthTotal = monthExpenses.Sum(e => e.Expenses);

            // Read user budget from BudgetTable
            var budgetRecord = await _db.BudgetTable.FirstOrDefaultAsync();
            decimal budget = budgetRecord != null ? budgetRecord.Amount : 0;
            double usedPct = budget > 0 ? Math.Min((double)(monthTotal / budget) * 100.0, 100.0) : 0;
            decimal remaining = budget > 0 ? Math.Max(budget - monthTotal, 0) : 0;

            int baseIncomeForSavings = profile.MonthlyIncome > 0 ? profile.MonthlyIncome : (int)budget;
            int savingsTargetVal = (int)(baseIncomeForSavings * (profile.SavingsPercentage / 100.0));
            int currentSavingsVal = 0;
            var savingsTxs = await _db.SavingsTransaction.ToListAsync();
            foreach (var tx in savingsTxs)
            {
                if (tx.Type == "Add" || tx.Type == "Transfer")
                    currentSavingsVal += tx.Amount;
                else
                    currentSavingsVal -= tx.Amount;
            }
            currentSavingsVal = Math.Max(currentSavingsVal, 0);

            var rawLendingTxs = await _db.LendingTransaction.ToListAsync();
            int outstandingLentVal = rawLendingTxs.Where(l => l.Status != "Recovered").Sum(l => l.Amount);

            var rawCommitments = await _db.MonthlyCommitment.ToListAsync();
            int commitmentsTotalVal = rawCommitments.Where(c => c.IsActive).Sum(c => c.Amount);

            // Calculate Available Balance
            int availableBalanceVal = (int)(profile.MonthlyIncome - commitmentsTotalVal - savingsTargetVal - monthTotal - outstandingLentVal);
            availableBalanceVal = Math.Max(availableBalanceVal, 0);

            // ---- Savings Card Widget (Visible in both modes) ----
            commitSavingsProgressLabel.Text = $"Saved ₹{currentSavingsVal:N0} of ₹{savingsTargetVal:N0}";
            int remainingSavings = Math.Max(savingsTargetVal - currentSavingsVal, 0);
            commitSavingsRemainingLabel.Text = $"₹{remainingSavings:N0} remaining to reach goal";
            
            double savingsProgressPct = savingsTargetVal > 0 ? ((double)currentSavingsVal / savingsTargetVal) * 100.0 : 0.0;
            commitSavingsProgressBar.WidthRequest = Math.Max(280.0 * (Math.Min(savingsProgressPct, 100.0) / 100.0), 4.0);

            // ---- Lending Card Widget (Visible in both modes) ----
            commitLendingOutstandingLabel.Text = $"₹{outstandingLentVal:N0}";
            int expectedRecovery = rawLendingTxs
                .Where(l => l.Status != "Recovered" && l.DueDate.Year == targetDate.Year && l.DueDate.Month == targetDate.Month)
                .Sum(l => l.Amount);
            commitLendingRecoveryLabel.Text = $"₹{expectedRecovery:N0}";

            if (IsBudgetMode)
            {
                // ---- Hero Budget card ----
                heroMonthLabel.Text = $"{targetDate.ToString("MMMM")} budget";
                if (budget > 0)
                {
                    if (usedPct >= 100)
                    {
                        heroStatusLabel.Text = "Over budget";
                        heroStatusLabel.TextColor = Color.FromArgb("#E53935");
                        heroRemainingLabel.Text = $"₹{Math.Abs(budget - monthTotal):N0} over budget";
                        heroRemainingLabel.TextColor = Color.FromArgb("#E53935");
                    }
                    else
                    {
                        heroStatusLabel.Text = "On track";
                        heroStatusLabel.TextColor = Color.FromArgb("#10CFC9");
                        heroRemainingLabel.Text = $"₹{remaining:N0} left";
                        heroRemainingLabel.TextColor = Color.FromArgb("#1C2340");
                    }
                    heroSpentLabel.Text = $"Spent ₹{monthTotal:N0} of ₹{budget:N0}";
                }
                else
                {
                    heroStatusLabel.Text = "No budget set";
                    heroStatusLabel.TextColor = Color.FromArgb("#8A94A6");
                    heroRemainingLabel.Text = "Tap here to set budget";
                    heroRemainingLabel.TextColor = Color.FromArgb("#10CFC9");
                    heroSpentLabel.Text = $"Spent ₹{monthTotal:N0}";
                }

                // ---- Safe to Spend Today ----
                int daysLeft = DateTime.DaysInMonth(targetDate.Year, targetDate.Month) - targetDate.Day + 1;
                if (budget > 0 && remaining > 0 && daysLeft > 0)
                {
                    decimal safeDaily = remaining / daysLeft;
                    safeDailyLabel.Text = $"₹{safeDaily:N0}";
                }
                else
                {
                    safeDailyLabel.Text = "₹0";
                }

                // ---- Today's Spending ----
                var todayExpenses = allExpenses.Where(e => e.Date.Date == targetDate.Date).ToList();
                decimal todayTotal = todayExpenses.Sum(e => e.Expenses);
                todayTotalLabel.Text = $"₹{todayTotal:N0}";
                todayTransactionsLabel.Text = todayExpenses.Count == 1 ? "1 transaction" : $"{todayExpenses.Count} transactions";

                // ---- Remaining Budget (Today's Card Right Side) ----
                remainingTotalLabel.Text = $"₹{remaining:N0}";
                if (budget > 0)
                {
                    remainingStatusLabel.Text = usedPct >= 100 ? "Over budget" : "Budget available";
                    remainingStatusLabel.TextColor = usedPct >= 100 ? Color.FromArgb("#E53935") : Color.FromArgb("#8A94A6");
                }
                else
                {
                    remainingStatusLabel.Text = "No budget set";
                    remainingStatusLabel.TextColor = Color.FromArgb("#8A94A6");
                }

                // ---- This Month at a Glance ----
                breakdownPeriodLabel.Text = targetDate.ToString("MMMM yyyy");
                monthTransactionsLabel.Text = $"{monthExpenses.Count}";
                
                // ---- Monthly Trend ----
                var lastMonthStart = monthStart.AddMonths(-1);
                var lastMonthEnd = monthStart.AddDays(-1);
                var lastMonthExpenses = allExpenses.Where(e => e.Date.Date >= lastMonthStart.Date && e.Date.Date <= lastMonthEnd.Date).ToList();
                decimal lastMonthTotal = lastMonthExpenses.Sum(e => e.Expenses);
                if (lastMonthTotal > 0)
                {
                    decimal diff = monthTotal - lastMonthTotal;
                    decimal pctDiff = (diff / lastMonthTotal) * 100;
                    string sign = pctDiff > 0 ? "+" : "";
                    monthTrendLabel.Text = $"{sign}{pctDiff:F0}%";
                    monthTrendLabel.TextColor = pctDiff > 0 ? Color.FromArgb("#E53935") : Color.FromArgb("#10CFC9");
                }
                else
                {
                    monthTrendLabel.Text = "N/A";
                    monthTrendLabel.TextColor = Color.FromArgb("#8A94A6");
                }
                
                int daysPassed  = Math.Max(targetDate.Day, 1);
                decimal dailyAvg = monthTotal > 0 ? monthTotal / daysPassed : 0;
                dailyAvgLabel.Text = $"₹{dailyAvg:N0}";

                var highestExpense = monthExpenses.OrderByDescending(e => e.Expenses).FirstOrDefault();
                if (highestExpense != null)
                {
                    highestExpenseCategoryLabel.Text = highestExpense.Category;
                    highestExpenseAmountLabel.Text = $"₹{highestExpense.Expenses:N0}";
                    highestExpenseAmountLabel.IsVisible = true;
                }
                else
                {
                    highestExpenseCategoryLabel.Text = "None";
                    highestExpenseAmountLabel.IsVisible = false;
                }
            }
            else
            {
                // ---- Commitments Mode Hero Card ----
                commitHeroIncomeLabel.Text = $"₹{profile.MonthlyIncome:N0}";
                commitHeroCommittedLabel.Text = $"₹{commitmentsTotalVal:N0}";
                commitHeroSavingsLabel.Text = $"₹{savingsTargetVal:N0}";
                commitHeroAvailableLabel.Text = $"₹{availableBalanceVal:N0}";



                // ---- Upcoming Commitments widget ----
                int totalCount = rawCommitments.Count(c => c.IsActive);
                int paidCount = rawCommitments.Count(c => c.IsActive && c.DueDay <= targetDate.Day);
                commitProgressLabel.Text = $"{paidCount}/{totalCount} paid";

                double commitProgressPct = totalCount > 0 ? ((double)paidCount / totalCount) * 100.0 : 0.0;
                commitProgressBar.WidthRequest = Math.Max(280.0 * (Math.Min(commitProgressPct, 100.0) / 100.0), 4.0);

                // Populating Upcoming commitments stack dynamically
                dashboardCommitmentsStack.Children.Clear();
                var sortedCommitments = rawCommitments.OrderBy(c => c.DueDay).ToList();

                foreach (var c in sortedCommitments)
                {
                    bool isPaid = c.DueDay <= targetDate.Day;
                    string statusText = isPaid ? "Paid" : "Upcoming";
                    Color statusColor = isPaid ? Color.FromArgb("#2E7D32") : Color.FromArgb("#1565C0");
                    Color statusBg = isPaid ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#E3F2FD");

                    var itemGrid = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = GridLength.Auto }
                        },
                        ColumnSpacing = 10
                    };

                    // Emoji Border
                    var emojiBorder = new Border
                    {
                        WidthRequest = 36,
                        HeightRequest = 36,
                        BackgroundColor = Color.FromArgb("#E0F2F1"),
                        StrokeThickness = 0,
                        StrokeShape = new RoundRectangle { CornerRadius = 10 }
                    };
                    emojiBorder.Content = new Label
                    {
                        Text = "🔁",
                        FontSize = 16,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    };
                    itemGrid.Children.Add(emojiBorder);
                    Grid.SetColumn(emojiBorder, 0);

                    // Details Stack
                    var detailsStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
                    detailsStack.Children.Add(new Label { Text = c.Name, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1C2340") });
                    detailsStack.Children.Add(new Label { Text = $"Due on the {c.DueDay}th • Auto-add: {(c.AutoAdd ? "On" : "Off")}", FontSize = 11, TextColor = Color.FromArgb("#8A94A6") });
                    itemGrid.Children.Add(detailsStack);
                    Grid.SetColumn(detailsStack, 1);

                    // Value and Status Stack
                    var valueStack = new VerticalStackLayout { Spacing = 2, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
                    valueStack.Children.Add(new Label { Text = $"₹{c.Amount:N0}", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1C2340"), HorizontalOptions = LayoutOptions.End });
                    
                    var statusBorder = new Border
                    {
                        BackgroundColor = statusBg,
                        StrokeThickness = 0,
                        Padding = new Thickness(6, 2),
                        HorizontalOptions = LayoutOptions.End,
                        StrokeShape = new RoundRectangle { CornerRadius = 8 }
                    };
                    statusBorder.Content = new Label { Text = statusText, TextColor = statusColor, FontSize = 9, FontAttributes = FontAttributes.Bold };
                    valueStack.Children.Add(statusBorder);
                    
                    itemGrid.Children.Add(valueStack);
                    Grid.SetColumn(valueStack, 2);

                    // Action Buttons
                    var actionsStack = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
                    
                    var editBtn = new Button { Text = "✎", FontSize = 11, TextColor = Color.FromArgb("#0D8C87"), BackgroundColor = Colors.Transparent, Padding = 0, WidthRequest = 24, HeightRequest = 24 };
                    editBtn.Clicked += async (s, e) => await EditCommitmentAsync(c);
                    actionsStack.Children.Add(editBtn);

                    var deleteBtn = new Button { Text = "✕", FontSize = 11, TextColor = Color.FromArgb("#D32F2F"), BackgroundColor = Colors.Transparent, Padding = 0, WidthRequest = 24, HeightRequest = 24 };
                    deleteBtn.Clicked += async (s, e) => await DeleteCommitmentAsync(c);
                    actionsStack.Children.Add(deleteBtn);

                    itemGrid.Children.Add(actionsStack);
                    Grid.SetColumn(actionsStack, 3);

                    dashboardCommitmentsStack.Children.Add(itemGrid);
                    
                    if (c != sortedCommitments.Last())
                    {
                        dashboardCommitmentsStack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#F0F3F8") });
                    }
                }
            }

            // ---- SMART INSIGHTS ENGINE ----
            if (IsBudgetMode)
            {
                if (budget > 0)
                {
                    var highestExpense = monthExpenses.OrderByDescending(e => e.Expenses).FirstOrDefault();
                    string highestCategory = highestExpense?.Category?.ToLower() ?? "expenses";
                    if (usedPct >= 100)
                    {
                        smartInsightLabel.Text = $"You’ve used 100% of your monthly budget. Consider reducing {highestCategory} expenses.";
                    }
                    else if (usedPct >= 80)
                    {
                        smartInsightLabel.Text = $"You’ve used {usedPct:F0}% of your monthly budget. Keep an eye on {highestCategory} spending.";
                    }
                    else
                    {
                        smartInsightLabel.Text = $"You're on track! You have ₹{remaining:N0} remaining in your budget.";
                    }
                }
                else
                {
                    smartInsightLabel.Text = "Set a monthly budget to get smart insights on your spending!";
                }
            }
            else
            {
                var overdueLentTx = rawLendingTxs.FirstOrDefault(l => l.Status == "Overdue");
                var upcomingCommit = rawCommitments.Where(c => c.IsActive && c.DueDay > targetDate.Day).OrderBy(c => c.DueDay).FirstOrDefault();
                int savingsDiff = savingsTargetVal - currentSavingsVal;

                if (overdueLentTx != null)
                {
                    int overdueDays = (int)(targetDate - overdueLentTx.DueDate).TotalDays;
                    smartInsightLabel.Text = $"{overdueLentTx.PersonName}’s repayment is overdue by {overdueDays} days. Send a reminder.";
                }
                else if (upcomingCommit != null && (upcomingCommit.DueDay - targetDate.Day) <= 3)
                {
                    int diffDays = upcomingCommit.DueDay - targetDate.Day;
                    smartInsightLabel.Text = $"Your '{upcomingCommit.Name}' commitment of ₹{upcomingCommit.Amount:N0} is due in {diffDays} days.";
                }
                else if (savingsDiff > 0)
                {
                    smartInsightLabel.Text = $"You are behind your savings target by ₹{savingsDiff:N0}. Try transferring funds from available balance.";
                }
                else
                {
                    int daysLeftVal = DateTime.DaysInMonth(targetDate.Year, targetDate.Month) - targetDate.Day + 1;
                    decimal safeDailySpend = daysLeftVal > 0 ? (decimal)availableBalanceVal / daysLeftVal : 0;
                    smartInsightLabel.Text = $"You can safely spend ₹{safeDailySpend:N0} per day for the rest of the month.";
                }
            }

            // Recent transactions
            BuildRecentTransactions(monthExpenses);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
    private async void OnSetBudgetTapped(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Set Budget", "Enter your monthly target budget (₹):", keyboard: Keyboard.Numeric);
        if (!string.IsNullOrWhiteSpace(result) && int.TryParse(result, out int newBudget) && newBudget > 0)
        {
            var budgetRecord = await _db.BudgetTable.FirstOrDefaultAsync();
            if (budgetRecord != null)
            {
                budgetRecord.Amount = newBudget;
                _db.BudgetTable.Update(budgetRecord);
            }
            else
            {
                _db.BudgetTable.Add(new BudgetTable { Amount = newBudget });
            }
            await _db.SaveChangesAsync();
            await LoadDashboardData();
        }
    }

    private readonly List<Color> _palette = new()
    {
        Color.FromArgb("#10CFC9"), Color.FromArgb("#0D9C96"), Color.FromArgb("#FF6B6B"),
        Color.FromArgb("#FFC107"), Color.FromArgb("#00C9A7"), Color.FromArgb("#9C27B0"),
        Color.FromArgb("#FF9800"), Color.FromArgb("#2196F3"), Color.FromArgb("#795548"),
        Color.FromArgb("#E91E63")
    };

    private void BuildCategoryBars(List<ExpenseTable> expenses)
    {
        CategoryItems.Clear();

        var grouped = expenses
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Expenses) })
            .OrderByDescending(g => g.Total)
            .ToList();

        if (!grouped.Any())
        {
            CategoryItems.Add(new DashboardCategoryItem
            {
                Category = "No expenses",
                Amount   = 0,
                BarWidth = 4,
                BarColor = Colors.LightGray
            });
            return;
        }

        decimal max = grouped.Max(g => g.Total);
        int colorIdx = 0;
        const double MaxBar = 200.0;

        foreach (var item in grouped)
        {
            double barW = max > 0 ? (double)(item.Total / max) * MaxBar : 4;
            if (barW < 4) barW = 4;

            CategoryItems.Add(new DashboardCategoryItem
            {
                Category = item.Category,
                Amount   = item.Total,
                BarWidth = barW,
                BarColor = _palette[colorIdx % _palette.Count]
            });
            colorIdx++;
        }
    }

    private void BuildRecentTransactions(List<ExpenseTable> expenses)
    {
        RecentTransactions.Clear();

        var recent = expenses
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.SNo)
            .Take(10)
            .ToList();

        if (!recent.Any())
        {
            emptyState.IsVisible = true;
            return;
        }

        emptyState.IsVisible = false;

        foreach (var e in recent)
        {
            string subCat = string.IsNullOrWhiteSpace(e.SubCategory) ? e.Category : e.SubCategory;
            string payMode = string.IsNullOrWhiteSpace(e.PayMode) ? "Cash" : e.PayMode;
            
            RecentTransactions.Add(new DashboardExpenseItem
            {
                SNo           = e.SNo,
                Category      = e.Category,
                SubCategory   = e.SubCategory,
                Expenses      = e.Expenses,
                Date          = e.Date,
                CategoryEmoji = GetEmoji(e.Category),
                IconBg        = GetIconBg(e.Category),
                AmountDisplay = $"-₹{e.Expenses:N0}",
                TitleDisplay  = subCat,
                SubtitleDisplay = $"{e.Category} • {payMode}",
                DateDisplay   = e.Date.Date == DateTime.Today
                    ? "Today"
                    : e.Date.Date == DateTime.Today.AddDays(-1)
                        ? "Yesterday"
                        : e.Date.ToString("dd MMM yyyy")
            });
        }
    }

    private static string GetEmoji(string category) => category?.ToLower() switch
    {
        "food"          => "🍔",
        "groceries"     => "🛒",
        "travel"        => "✈️",
        "transport"     => "🚗",
        "shopping"      => "🛍️",
        "education"     => "📚",
        "medicine"      => "💊",
        "entertainment" => "🎬",
        "housing"       => "🏠",
        "rent"          => "🏠",
        "savings"       => "💰",
        "loan"          => "💳",
        "lending"       => "🤝",
        _               => "📌"
    };

    private static Color GetIconBg(string category) => category?.ToLower() switch
    {
        "food"          => Color.FromArgb("#FFF3E0"),
        "groceries"     => Color.FromArgb("#E8F5E9"),
        "travel"        => Color.FromArgb("#E3F2FD"),
        "transport"     => Color.FromArgb("#E3F2FD"),
        "shopping"      => Color.FromArgb("#FCE4EC"),
        "education"     => Color.FromArgb("#F3E5F5"),
        "medicine"      => Color.FromArgb("#E8F5E9"),
        "entertainment" => Color.FromArgb("#FFF8E1"),
        "housing"       => Color.FromArgb("#E8EAF6"),
        "rent"          => Color.FromArgb("#E8EAF6"),
        "savings"       => Color.FromArgb("#FFFDE7"),
        "loan"          => Color.FromArgb("#FCE4EC"),
        "lending"       => Color.FromArgb("#E0F2F1"),
        _               => Color.FromArgb("#EEF0FF")
    };

    private async void OnSavingsCardTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SavingsPlannerPage());
    }

    private async void OnLendingCardTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new LendingRecoveryPage());
    }

    private async Task EditCommitmentAsync(MonthlyCommitment c)
    {
        string newName = await DisplayPromptAsync("Edit Commitment", "Name:", "Save", "Cancel", c.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;

        string amountStr = await DisplayPromptAsync("Edit Commitment", "Amount (₹):", "Save", "Cancel", c.Amount.ToString(), keyboard: Keyboard.Numeric);
        if (!int.TryParse(amountStr, out int newAmount) || newAmount <= 0) return;

        string dayStr = await DisplayPromptAsync("Edit Commitment", "Due Day of Month (1-31):", "Save", "Cancel", c.DueDay.ToString(), keyboard: Keyboard.Numeric);
        if (!int.TryParse(dayStr, out int newDay) || newDay < 1 || newDay > 31) return;

        try
        {
            c.Name = newName;
            c.Amount = newAmount;
            c.DueDay = newDay;
            _db.MonthlyCommitment.Update(c);
            await _db.SaveChangesAsync();
            await UIHelper.ShowToastMessage("Commitment updated!");
            await LoadDashboardData();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task DeleteCommitmentAsync(MonthlyCommitment c)
    {
        var confirmPopup = new ConfirmationPopup(
            "Delete Commitment ✕",
            $"Are you sure you want to remove '{c.Name}'?",
            "This will remove the commitment permanently.",
            "Delete",
            "Cancel",
            isDestructive: true);

        var confirmResult = await this.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirm || !confirm) return;

        try
        {
            _db.MonthlyCommitment.Remove(c);
            await _db.SaveChangesAsync();
            await UIHelper.ShowToastMessage("Commitment deleted.");
            await LoadDashboardData();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnHistoryTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new HistoryPage());

    private async void OnStatsTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new StatsPage());

    private async void OnProfileTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new ProfilePage());

    private async void OnSeeAllTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new HistoryPage());
    private async void addButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            await Navigation.PushAsync(new MenuPage());
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }
    }

}


public class DashboardCategoryItem
{
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public double BarWidth { get; set; }
    public Color BarColor { get; set; } = Colors.Gray;
}

public class DashboardExpenseItem
{
    public int SNo { get; set; }
    public string Category { get; set; } = "";
    public string SubCategory { get; set; } = "";
    public decimal Expenses { get; set; }
    public DateTime Date { get; set; }
    public string CategoryEmoji { get; set; } = "📌";
    public Color IconBg { get; set; } = Colors.LightGray;
    public string AmountDisplay { get; set; } = "";
    public string TitleDisplay { get; set; } = "";
    public string SubtitleDisplay { get; set; } = "";
    public string DateDisplay { get; set; } = "";
}
