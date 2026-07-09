using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ExpensifyApp.Pages;

public partial class TodayPage : ContentPage
{
    private readonly ExpenseContext _db;
    private List<ExpenseTable> _allExpenses = new();

    public ObservableCollection<CategoryExpenseModel> ChartItems { get; set; } = new();
    public ObservableCollection<ExpenseDisplay> ExpenseDisplays { get; set; } = new();

    public DateTime SelectedDate { get; set; } = DateTime.Today;
    private string CurrentFilter = "Date";

    public ICommand DeleteExpenseCommand { get; private set; }

    public TodayPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        BindingContext = this;

        DeleteExpenseCommand = new Command<ExpenseDisplay>(async (exp) => await DeleteExpense(exp));

        Appearing += async (s, e) => await LoadCurrentData();
    }

    private async Task DeleteExpense(ExpenseDisplay exp)
    {
        if (exp == null) return;

        bool confirm = await DisplayAlert("Delete Expense",
            $"Delete {exp.Category} - {exp.SubCategory} (₹{exp.Expenses:N0})",
            "Yes", "No");

        if (!confirm) return;

        var dbItem = await _db.ExpenseTable.FirstOrDefaultAsync(x => x.SNo == exp.SNo);
        if (dbItem != null)
        {
            _db.ExpenseTable.Remove(dbItem);
            await _db.SaveChangesAsync();

            ExpenseDisplays.Remove(exp);
            await LoadChartFiltered(_allExpenses.Where(x => ExpenseDisplays.Any(d => d.SNo == x.SNo)).ToList());
            UpdateTotal();
        }
    }

    private async Task LoadCurrentData()
    {
        _allExpenses = await _db.ExpenseTable.ToListAsync();

        List<ExpenseTable> filtered = CurrentFilter switch
        {
            "Weekly" => GetWeekExpenses(),
            "Monthly" => GetMonthExpenses(),
            _ => _allExpenses.Where(x => x.Date.Date == SelectedDate.Date).ToList()
        };

        var displayList = filtered.OrderByDescending(x => x.SNo)
            .Select(x => new ExpenseDisplay
            {
                SNo = x.SNo,
                Category = x.Category,
                SubCategory = x.SubCategory,
                Expenses = x.Expenses,
                Date = x.Date
            }).ToList();

        ExpenseDisplays.Clear();
        foreach (var item in displayList)
            ExpenseDisplays.Add(item);

        await LoadChartFiltered(filtered);
        UpdateTotal();
    }

    private void UpdateTotal()
    {
        decimal total = ExpenseDisplays.Sum(x => x.Expenses);
        bottomTotalLabel.Text = $"Total: ₹{total:N0}";
    }

    private async void OnDateChanged(object sender, DateChangedEventArgs e)
    {
        SelectedDate = e.NewDate;
        await LoadCurrentData();
    }

    private async void OnWeekChanged(object sender, EventArgs e) => await LoadCurrentData();
    private async void OnMonthChanged(object sender, EventArgs e) => await LoadCurrentData();

    private async void OnAddClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new MenuPage());

    private async void OnFilterButtonClicked(object sender, EventArgs e)
    {
        var popup = new FilterPopup();
        popup.OptionSelected += async (selected) =>
        {
            CurrentFilter = selected;
            datePicker.IsVisible = selected == "Date";
            weekPicker.IsVisible = selected == "Weekly";
            monthPicker.IsVisible = selected == "Monthly";

            if (selected == "Weekly") SetupWeekPicker();
            if (selected == "Monthly") SetupMonthPicker();

            await LoadCurrentData();
        };
        this.ShowPopup(popup);
    }

    private void SetupWeekPicker()
    {
        if (weekPicker.Items.Count == 0)
            for (int w = 1; w <= 5; w++) weekPicker.Items.Add($"Week {w}");
        weekPicker.SelectedIndex = 0;
    }

    private void SetupMonthPicker()
    {
        if (monthPicker.Items.Count == 0)
        {
            var today = DateTime.Today;
            for (int m = -12; m <= 12; m++)
            {
                var date = today.AddMonths(m);
                monthPicker.Items.Add($"{date:MMMM yyyy}");
            }
        }
        monthPicker.SelectedIndex = 12;
    }

    private List<ExpenseTable> GetWeekExpenses()
    {
        int week = weekPicker.SelectedIndex + 1;
        var firstDay = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var start = firstDay.AddDays((week - 1) * 7);
        var end = start.AddDays(6);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        if (end > lastDay) end = lastDay;
        return _allExpenses.Where(x => x.Date.Date >= start.Date && x.Date.Date <= end.Date).ToList();
    }

    private List<ExpenseTable> GetMonthExpenses()
    {
        if (monthPicker.SelectedItem is not string s) return new();
        var parts = s.Split(' ');
        var monthName = parts[0];
        var year = int.Parse(parts[1]);
        var monthNum = DateTime.ParseExact(monthName, "MMMM", null).Month;
        return _allExpenses.Where(x => x.Date.Year == year && x.Date.Month == monthNum).ToList();
    }

    private const double MaxBarWidth = 260.0;
    private readonly List<Color> ColorPalette = new()
    {
        Color.FromArgb("#4CAF50"), Color.FromArgb("#2196F3"), Color.FromArgb("#FF9800"),
        Color.FromArgb("#9C27B0"), Color.FromArgb("#F44336"), Color.FromArgb("#009688"),
        Color.FromArgb("#3F51B5"), Color.FromArgb("#795548"), Color.FromArgb("#607D8B")
    };

    private async Task LoadChartFiltered(List<ExpenseTable> list)
    {
        ChartItems.Clear();

        var grouped = list
            .GroupBy(x => x.Category)
            .Select(g => new CategoryExpenseModel
            {
                Category = g.Key,
                Amount = g.Sum(x => x.Expenses)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        if (!grouped.Any())
        {
            ChartItems.Add(new CategoryExpenseModel
            {
                Category = "No Data",
                Amount = 0,
                BarWidth = 4,
                BarColor = Colors.Gray
            });
            return;
        }

        decimal maxAmount = grouped.Max(x => x.Amount);
        if (maxAmount <= 0) maxAmount = 1;

        int colorIndex = 0;
        foreach (var item in grouped)
        {
            double width = (double)(item.Amount / maxAmount) * MaxBarWidth;
            if (width < 4) width = 4;

            ChartItems.Add(new CategoryExpenseModel
            {
                Category = item.Category,
                Amount = item.Amount,
                BarWidth = width,
                BarColor = ColorPalette[colorIndex % ColorPalette.Count]
            });
            colorIndex++;
        }
    }
}

public class CategoryExpenseModel
{
    public string Category { get; set; }
    public decimal Amount { get; set; }
    public double BarWidth { get; set; }
    public Color BarColor { get; set; }
}

public class ExpenseDisplay
{
    public int SNo { get; set; }
    public string Category { get; set; }
    public string SubCategory { get; set; }
    public decimal Expenses { get; set; }
    public DateTime Date { get; set; }
}