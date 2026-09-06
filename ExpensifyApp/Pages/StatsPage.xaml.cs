using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using Microcharts;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages;

public partial class StatsPage : ContentPage
{
    private readonly ExpenseContext _db;
    private string _currentPeriod = "Monthly"; // Daily, Weekly, Monthly

    public StatsPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        BindingContext = this;

        // Initialize pickers
        weeklyPicker.ItemsSource = new List<string> { "Week 1 (1st-7th)", "Week 2 (8th-14th)", "Week 3 (15th-21st)", "Week 4 (22nd-28th)", "Week 5 (29th-End)" };
        weeklyPicker.SelectedIndex = 0;

        var months = new List<string>();
        for (int i = 1; i <= 12; i++) {
            months.Add(new DateTime(DateTime.Today.Year, i, 1).ToString("MMMM"));
        }
        monthlyPicker.ItemsSource = months;
        monthlyPicker.SelectedIndex = DateTime.Today.Month - 1;

        dailyDatePicker.Date = DateTime.Today;

        Appearing += async (s, e) => await LoadStatsData();
    }

    private async Task LoadStatsData()
    {
        try
        {
            var all = await _db.ExpenseTable.ToListAsync();
            var allExpenses = all.ToList();

            // 1. Filter by period
            DateTime startDate, endDate;
            DateTime prevStartDate, prevEndDate;

            if (_currentPeriod == "Daily")
            {
                startDate = dailyDatePicker.Date.Date;
                endDate = startDate.AddDays(1).AddTicks(-1);
                prevStartDate = startDate.AddDays(-1);
                prevEndDate = prevStartDate.AddDays(1).AddTicks(-1);
            }
            else if (_currentPeriod == "Weekly")
            {
                int weekIndex = weeklyPicker.SelectedIndex;
                if (weekIndex < 0) weekIndex = 0;
                int startDay = (weekIndex * 7) + 1;
                int endDay = weekIndex >= 4 ? DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) : startDay + 6;
                startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, startDay);
                endDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, endDay, 23, 59, 59);

                prevEndDate = startDate.AddTicks(-1);
                prevStartDate = prevEndDate.AddDays(-7);
            }
            else
            {
                int monthIndex = monthlyPicker.SelectedIndex + 1;
                if (monthIndex < 1) monthIndex = DateTime.Today.Month;
                startDate = new DateTime(DateTime.Today.Year, monthIndex, 1);
                endDate = startDate.AddMonths(1).AddTicks(-1);
                
                prevStartDate = startDate.AddMonths(-1);
                prevEndDate = startDate.AddTicks(-1);
            }

            var periodData = all.Where(x => x.Date >= startDate && x.Date <= endDate).ToList();
            var prevPeriodData = all.Where(x => x.Date >= prevStartDate && x.Date <= prevEndDate).ToList();

            // 2. Summary Cards
            decimal income = 0; // Income not implemented in db yet
            decimal spent = periodData.Sum(x => x.Expenses);
            
            summaryIncome.Text = $"₹{income:N0}";
            summarySpent.Text = $"₹{spent:N0}";
            summaryNet.Text = $"₹{(income - spent):N0}";
            summaryNet.TextColor = (income - spent) >= 0 ? Color.FromArgb("#10CFC9") : Color.FromArgb("#E53935");

            // 3. Comparison
            decimal prevSpent = prevPeriodData.Sum(x => x.Expenses);
            decimal diff = spent - prevSpent;
            if (diff > 0)
            {
                comparisonIcon.Text = "↑";
                comparisonIcon.TextColor = Color.FromArgb("#E53935");
                comparisonText.Text = $"You spent ₹{Math.Abs(diff):N0} more than last period";
            }
            else
            {
                comparisonIcon.Text = "↓";
                comparisonIcon.TextColor = Color.FromArgb("#10CFC9");
                comparisonText.Text = $"You spent ₹{Math.Abs(diff):N0} less than last period";
            }

            // 4. Insights
            var expensesOnly = periodData.ToList();
            
            var topCategoryGroup = expensesOnly.GroupBy(x => x.Category).OrderByDescending(g => g.Sum(x => x.Expenses)).FirstOrDefault();
            insightTopCategory.Text = topCategoryGroup != null ? topCategoryGroup.Key : "N/A";

            var biggest = expensesOnly.OrderByDescending(x => x.Expenses).FirstOrDefault();
            insightBiggest.Text = biggest != null ? $"₹{biggest.Expenses:N0}" : "₹0";

            var maxDayGroup = expensesOnly.GroupBy(x => x.Date.Date).OrderByDescending(g => g.Sum(x => x.Expenses)).FirstOrDefault();
            insightMaxDay.Text = maxDayGroup != null ? maxDayGroup.Key.ToString("MMM d") : "N/A";

            int days = (endDate - startDate).Days + 1;
            insightAvg.Text = $"₹{(spent / Math.Max(1, days)):N0}";

            // 5. Visuals
            BuildBarChart(expensesOnly, startDate, endDate);
            BuildCategoryRanking(expensesOnly);
            
            if (_currentPeriod == "Daily")
            {
                donutChartContainer.IsVisible = false;
                heatmapContainer.IsVisible = false;
            }
            else if (_currentPeriod == "Weekly")
            {
                donutChartContainer.IsVisible = true;
                heatmapContainer.IsVisible = false;
                BuildDonutChart(expensesOnly);
            }
            else
            {
                donutChartContainer.IsVisible = true;
                heatmapContainer.IsVisible = true;
                BuildDonutChart(expensesOnly);
                BuildHeatmap(all.ToList(), startDate);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void BuildBarChart(List<ExpenseTable> expenses, DateTime start, DateTime end)
    {
        barChartGrid.Children.Clear();
        barChartGrid.ColumnDefinitions.Clear();
        
        List<Tuple<string, decimal>> dataPoints = new();

        if (_currentPeriod == "Daily")
        {
            barChartTitle.Text = "Hourly Spending";
            int[] hours = { 6, 9, 12, 15, 18, 21 };
            string[] labels = { "6 AM", "9 AM", "12 PM", "3 PM", "6 PM", "9 PM" };
            
            for (int i = 0; i < hours.Length; i++)
            {
                int hStart = hours[i];
                int hEnd = i == hours.Length - 1 ? 24 : hours[i + 1];
                decimal sum = expenses.Where(x => x.Date.Hour >= hStart && x.Date.Hour < hEnd).Sum(x => x.Expenses);
                dataPoints.Add(new Tuple<string, decimal>(labels[i], sum));
            }
        }
        else if (_currentPeriod == "Weekly")
        {
            barChartTitle.Text = "Daily Spending";
            for (int i = 0; i < 7; i++)
            {
                var day = start.AddDays(i);
                if (day > end) break;
                decimal sum = expenses.Where(x => x.Date.Date == day.Date).Sum(x => x.Expenses);
                dataPoints.Add(new Tuple<string, decimal>(day.ToString("ddd"), sum));
            }
        }
        else
        {
            barChartTitle.Text = "Weekly Spending";
            for (int w = 0; w < 5; w++)
            {
                int sDay = (w * 7) + 1;
                int eDay = w == 4 ? DateTime.DaysInMonth(start.Year, start.Month) : sDay + 6;
                decimal sum = expenses.Where(x => x.Date.Day >= sDay && x.Date.Day <= eDay).Sum(x => x.Expenses);
                dataPoints.Add(new Tuple<string, decimal>($"W{w + 1}", sum));
            }
        }

        decimal max = dataPoints.Any() ? dataPoints.Max(d => d.Item2) : 0;
        if (max == 0) max = 1;

        for (int i = 0; i < dataPoints.Count; i++)
        {
            barChartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            var point = dataPoints[i];
            
            var stack = new VerticalStackLayout { VerticalOptions = LayoutOptions.End };
            
            double height = Math.Max(4, (double)(point.Item2 / max) * 90);
            
            var bar = new Border 
            { 
                BackgroundColor = Color.FromArgb("#10CFC9"), 
                HeightRequest = height, 
                StrokeThickness = 0,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 4)
            };
            bar.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 };
            
            var label = new Label 
            { 
                Text = point.Item1, 
                FontSize = 10, 
                TextColor = Color.FromArgb("#8A94A6"), 
                HorizontalOptions = LayoutOptions.Center 
            };

            stack.Children.Add(bar);
            stack.Children.Add(label);
            
            Grid.SetColumn(stack, i);
            barChartGrid.Children.Add(stack);
        }
    }

    private void BuildDonutChart(List<ExpenseTable> expenses)
    {
        var categoryGrouped = expenses.GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key ?? "Other", Amount = g.Sum(x => x.Expenses) })
            .OrderByDescending(g => g.Amount)
            .ToList();

        var entries = new List<ChartEntry>();
        var colors = new[] { "#10CFC9", "#3D4A6B", "#FFC107", "#9C27B0", "#2196F3", "#FF9800", "#E53935" };

        for (int i = 0; i < categoryGrouped.Count; i++)
        {
            entries.Add(new ChartEntry((float)categoryGrouped[i].Amount)
            {
                Label = categoryGrouped[i].Category,
                ValueLabel = $"₹{categoryGrouped[i].Amount:N0}",
                Color = SKColor.Parse(colors[i % colors.Length])
            });
        }

        donutChart.Chart = new DonutChart
        {
            Entries = entries,
            LabelTextSize = 26f,
            BackgroundColor = SKColors.Transparent,
            HoleRadius = 0.55f
        };
    }

    private void BuildCategoryRanking(List<ExpenseTable> expenses)
    {
        categoryRankingList.Children.Clear();
        var categoryGrouped = expenses.GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key ?? "Other", Amount = g.Sum(x => x.Expenses) })
            .OrderByDescending(g => g.Amount)
            .ToList();

        decimal total = categoryGrouped.Sum(c => c.Amount);
        var colors = new[] { "#10CFC9", "#3D4A6B", "#FFC107", "#9C27B0", "#2196F3", "#FF9800", "#E53935" };

        for (int i = 0; i < categoryGrouped.Count; i++)
        {
            var item = categoryGrouped[i];
            double pct = total > 0 ? (double)(item.Amount / total) : 0;
            
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } } };
            
            var hStack = new HorizontalStackLayout { Spacing = 8 };
            hStack.Children.Add(new BoxView { WidthRequest = 12, HeightRequest = 12, CornerRadius = 6, Color = Color.FromArgb(colors[i % colors.Length]), VerticalOptions = LayoutOptions.Center });
            hStack.Children.Add(new Label { Text = item.Category, FontSize = 13, TextColor = Color.FromArgb("#1C2340"), VerticalOptions = LayoutOptions.Center });
            
            grid.Children.Add(hStack);
            Grid.SetColumn(hStack, 0);

            var amountLabel = new Label { Text = $"₹{item.Amount:N0}", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1C2340"), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
            grid.Children.Add(amountLabel);
            Grid.SetColumn(amountLabel, 2);

            categoryRankingList.Children.Add(grid);
            
            if (i < categoryGrouped.Count - 1)
            {
                categoryRankingList.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#F3F4F6") });
            }
        }
    }

    private void BuildHeatmap(List<ExpenseTable> allExpenses, DateTime start)
    {
        heatmapGrid.Children.Clear();
        int daysInMonth = DateTime.DaysInMonth(start.Year, start.Month);
        
        var dailyTotals = allExpenses
            .Where(x => x.Date.Year == start.Year && x.Date.Month == start.Month)
            .GroupBy(x => x.Date.Day)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Expenses));

        decimal maxDaily = dailyTotals.Values.Any() ? dailyTotals.Values.Max() : 0;
        
        for (int d = 1; d <= daysInMonth; d++)
        {
            decimal amt = dailyTotals.ContainsKey(d) ? dailyTotals[d] : 0;
            Color dotColor = Color.FromArgb("#E5E7EB"); // Zero
            
            if (amt > 0 && maxDaily > 0)
            {
                double ratio = (double)(amt / maxDaily);
                if (ratio < 0.33) dotColor = Color.FromArgb("#4CAF50"); // Low
                else if (ratio < 0.66) dotColor = Color.FromArgb("#FFC107"); // Med
                else dotColor = Color.FromArgb("#F44336"); // High
            }

            var dot = new Border
            {
                WidthRequest = 30,
                HeightRequest = 30,
                BackgroundColor = dotColor,
                StrokeThickness = 0,
                Margin = new Thickness(4)
            };
            dot.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 15 };
            
            var lbl = new Label
            {
                Text = d.ToString(),
                TextColor = amt > 0 ? Colors.White : Color.FromArgb("#8A94A6"),
                FontSize = 11,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontAttributes = FontAttributes.Bold
            };
            dot.Content = lbl;
            
            heatmapGrid.Children.Add(dot);
        }
    }

    private async void OnDateSelected(object sender, DateChangedEventArgs e) => await LoadStatsData();
    private async void OnPeriodChanged(object sender, EventArgs e) => await LoadStatsData();

    private async void OnPillDaily(object sender, EventArgs e)
    {
        if (_currentPeriod == "Daily") return;
        _currentPeriod = "Daily";
        SetActivePill("Daily");
        await LoadStatsData();
    }

    private async void OnPillWeekly(object sender, EventArgs e)
    {
        if (_currentPeriod == "Weekly") return;
        _currentPeriod = "Weekly";
        SetActivePill("Weekly");
        await LoadStatsData();
    }

    private async void OnPillMonthly(object sender, EventArgs e)
    {
        if (_currentPeriod == "Monthly") return;
        _currentPeriod = "Monthly";
        SetActivePill("Monthly");
        await LoadStatsData();
    }

    private void SetActivePill(string active)
    {
        pillDaily.BackgroundColor = Color.FromArgb("#0D9C96");
        pillWeekly.BackgroundColor = Color.FromArgb("#0D9C96");
        pillMonthly.BackgroundColor = Color.FromArgb("#0D9C96");
        
        lblDaily.TextColor = Colors.White; lblDaily.FontAttributes = FontAttributes.None;
        lblWeekly.TextColor = Colors.White; lblWeekly.FontAttributes = FontAttributes.None;
        lblMonthly.TextColor = Colors.White; lblMonthly.FontAttributes = FontAttributes.None;

        dailySelector.IsVisible = false;
        weeklySelector.IsVisible = false;
        monthlySelector.IsVisible = false;

        if (active == "Daily") 
        {
            pillDaily.BackgroundColor = Colors.White;
            lblDaily.TextColor = Color.FromArgb("#10CFC9"); lblDaily.FontAttributes = FontAttributes.Bold;
            dailySelector.IsVisible = true;
        } 
        else if (active == "Weekly") 
        {
            pillWeekly.BackgroundColor = Colors.White;
            lblWeekly.TextColor = Color.FromArgb("#10CFC9"); lblWeekly.FontAttributes = FontAttributes.Bold;
            weeklySelector.IsVisible = true;
        } 
        else 
        {
            pillMonthly.BackgroundColor = Colors.White;
            lblMonthly.TextColor = Color.FromArgb("#10CFC9"); lblMonthly.FontAttributes = FontAttributes.Bold;
            monthlySelector.IsVisible = true;
        }
    }

    private async void OnDashboardTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new DashboardPage());

    private async void OnHistoryTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new HistoryPage());

    private async void OnProfileTapped(object sender, EventArgs e)
        => await AppNavigation.NavigateToTabAsync(Navigation, new ProfilePage());

    private async void addButton_Clicked(object sender, EventArgs e)
    {
        try { await Navigation.PushAsync(new MenuPage()); }
        catch (Exception ex) { await UIHelper.HandleException(ex); }
    }
}
