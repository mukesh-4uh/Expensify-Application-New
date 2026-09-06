using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using CommunityToolkit.Maui.Views;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages;

public class RecurringDisplayItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isActive;

    public int Id { get; set; }
    public string Category { get; set; } = "";
    public string SubCategory { get; set; } = "";
    public string PayMode { get; set; } = "";
    public int Amount { get; set; }
    public int DayOfMonth { get; set; }
    
    public string CategoryEmoji { get; set; } = "📌";
    public Color IconBg { get; set; } = Colors.LightGray;
    public string AmountDisplay { get; set; } = "";
    public string RepeatSchedule { get; set; } = "";

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(RowOpacity));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBgColor));
                OnPropertyChanged(nameof(StatusTextColor));
                OnPropertyChanged(nameof(AutoAddText));
                OnPropertyChanged(nameof(AutoAddTextColor));
            }
        }
    }

    public double RowOpacity => IsActive ? 1.0 : 0.6;
    public string StatusText => IsActive ? "Active" : "Paused";
    public Color StatusBgColor => IsActive ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE");
    public Color StatusTextColor => IsActive ? Color.FromArgb("#2E7D32") : Color.FromArgb("#D32F2F");
    
    public string AutoAddText => IsActive ? "Enabled" : "Disabled";
    public Color AutoAddTextColor => IsActive ? Color.FromArgb("#0D8C87") : Color.FromArgb("#8A94A6");

    public string TitleDisplay => string.IsNullOrWhiteSpace(SubCategory) ? Category : SubCategory;
    public string NextPaymentDateStr => CalculateNextPaymentDate(DayOfMonth).ToString("dd MMM yyyy");
    public string CategoryName => Category;

    private static DateTime CalculateNextPaymentDate(int dayOfMonth)
    {
        var today = DateTime.Today;
        try
        {
            var candidate = new DateTime(today.Year, today.Month, Math.Min(dayOfMonth, DateTime.DaysInMonth(today.Year, today.Month)));
            if (candidate >= today)
            {
                return candidate;
            }
            var nextMonth = today.AddMonths(1);
            return new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(dayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
        }
        catch
        {
            return today;
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}

public partial class ManageRecurringPage : ContentPage
{
    private readonly ExpenseContext _db;
    public ObservableCollection<RecurringDisplayItem> RecurringExpenses { get; set; } = new();

    private int _activeCommitmentsCount;
    public int ActiveCommitmentsCount
    {
        get => _activeCommitmentsCount;
        set { _activeCommitmentsCount = value; OnPropertyChanged(nameof(ActiveCommitmentsCount)); }
    }

    private string _upcomingPaymentsText = "0 payments";
    public string UpcomingPaymentsText
    {
        get => _upcomingPaymentsText;
        set { _upcomingPaymentsText = value; OnPropertyChanged(nameof(UpcomingPaymentsText)); }
    }

    private string _monthlyTotalText = "₹0";
    public string MonthlyTotalText
    {
        get => _monthlyTotalText;
        set { _monthlyTotalText = value; OnPropertyChanged(nameof(MonthlyTotalText)); }
    }

    private void RecalculateStats()
    {
        int activeCount = RecurringExpenses.Count(r => r.IsActive);
        int totalActiveAmount = RecurringExpenses.Where(r => r.IsActive).Sum(r => r.Amount);

        ActiveCommitmentsCount = activeCount;
        UpcomingPaymentsText = $"{activeCount} payment" + (activeCount == 1 ? "" : "s");
        MonthlyTotalText = $"₹{totalActiveAmount:N0}";
    }

    public ManageRecurringPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        
        BindingContext = this;
        recurringList.ItemsSource = RecurringExpenses;

        Appearing += async (s, e) => await LoadRecurringExpenses();
    }

    private async Task LoadRecurringExpenses()
    {
        try
        {
            var raw = await _db.RecurringExpenseTable.ToListAsync();
            RecurringExpenses.Clear();

            if (raw.Any())
            {
                emptyState.IsVisible = false;
                recurringList.IsVisible = true;

                foreach (var item in raw)
                {
                    RecurringExpenses.Add(new RecurringDisplayItem
                    {
                        Id = item.Id,
                        Category = item.Category ?? "Other",
                        SubCategory = item.SubCategory ?? "",
                        PayMode = item.PayMode ?? "Cash",
                        Amount = item.Amount,
                        DayOfMonth = item.DayOfMonth,
                        CategoryEmoji = GetEmoji(item.Category),
                        IconBg = GetIconBg(item.Category),
                        AmountDisplay = $"₹{item.Amount:N0}",
                        RepeatSchedule = $"Every month on the {GetDaySuffix(item.DayOfMonth)}",
                        IsActive = item.IsActive
                    });
                }
            }
            else
            {
                recurringList.IsVisible = false;
                emptyState.IsVisible = true;
            }

            RecalculateStats();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnActiveToggled(object sender, ToggledEventArgs e)
    {
        var activeSwitch = sender as Switch;
        var item = activeSwitch?.BindingContext as RecurringDisplayItem;
        if (item == null) return;

        // Prevent recursive triggers
        if (item.IsActive == e.Value) return;

        try
        {
            var record = await _db.RecurringExpenseTable.FirstOrDefaultAsync(r => r.Id == item.Id);
            if (record != null)
            {
                record.IsActive = e.Value;
                _db.RecurringExpenseTable.Update(record);
                await _db.SaveChangesAsync();

                // Directly update backing state to trigger visual row opacity binding update
                item.IsActive = e.Value;

                RecalculateStats();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnDeleteTapped(object sender, EventArgs e)
    {
        var gesture = sender as BindableObject;
        var item = gesture?.BindingContext as RecurringDisplayItem;
        if (item == null) return;

        var confirmPopup = new ConfirmationPopup(
            "Remove Recurring 🔁",
            $"Are you sure you want to stop repeating this expense?\n\n📂 {item.Category}\n💰 {item.AmountDisplay}\n\nIt will no longer automatically appear each month.",
            "Confirm to stop recurring",
            "Stop Repeating",
            "Cancel",
            isDestructive: true);

        var confirmResult = await this.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirm || !confirm) return;

        try
        {
            var record = await _db.RecurringExpenseTable.FirstOrDefaultAsync(r => r.Id == item.Id);
            if (record != null)
            {
                // Soft delete by setting active to false or hard delete from DB
                _db.RecurringExpenseTable.Remove(record);
                await _db.SaveChangesAsync();
            }

            await LoadRecurringExpenses();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnAddTapped(object sender, EventArgs e)
    {
        try
        {
            var popup = new AddCommitmentPopup();
            var result = await this.ShowPopupAsync(popup);

            if (result is not AddCommitmentResult addResult) return;

            // Verify: Don't allow duplicate entries if already there
            var existing = await _db.RecurringExpenseTable.FirstOrDefaultAsync(r =>
                r.Category == addResult.Category &&
                r.SubCategory == addResult.SubCategory &&
                r.DayOfMonth == addResult.DayOfMonth &&
                r.IsActive);

            if (existing != null)
            {
                await DisplayAlert("Duplicate Entry ⚠️", 
                    $"A recurring commitment for category '{addResult.Category}' on day {addResult.DayOfMonth} already exists.", 
                    "OK");
                return;
            }

            var newCommitment = new RecurringExpenseTable
            {
                Category = addResult.Category,
                SubCategory = addResult.SubCategory,
                PayMode = "Cash",
                Amount = addResult.Amount,
                DayOfMonth = addResult.DayOfMonth,
                IsActive = true
            };

            _db.RecurringExpenseTable.Add(newCommitment);
            await _db.SaveChangesAsync();

            await UIHelper.ShowToastMessage("Commitment added successfully!");
            await LoadRecurringExpenses();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnEditTapped(object sender, EventArgs e)
    {
        var gesture = sender as BindableObject;
        var item = gesture?.BindingContext as RecurringDisplayItem;
        if (item == null) return;

        try
        {
            var popup = new EditRecurringPopup(
                item.Category,
                item.SubCategory,
                item.Amount,
                item.DayOfMonth);

            var result = await this.ShowPopupAsync(popup);
            if (result is not EditRecurringResult editResult) return;

            var record = await _db.RecurringExpenseTable.FirstOrDefaultAsync(r => r.Id == item.Id);
            if (record != null)
            {
                record.Amount = editResult.Amount;
                record.DayOfMonth = editResult.DayOfMonth;
                _db.RecurringExpenseTable.Update(record);
                await _db.SaveChangesAsync();

                await UIHelper.ShowToastMessage("Recurring expense updated.");
                await LoadRecurringExpenses();
            }
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
}
