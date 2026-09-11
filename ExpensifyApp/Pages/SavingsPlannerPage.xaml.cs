using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace ExpensifyApp.Pages
{
    public class SavingsDisplayItem
    {
        public int Id { get; set; }
        public string TypeEmoji { get; set; } = "💰";
        public string Description { get; set; } = "";
        public string DateStr { get; set; } = "";
        public string AmountDisplay { get; set; } = "";
        public Color AmountColor { get; set; } = Colors.Black;
    }

    public partial class SavingsPlannerPage : ContentPage
    {
        private readonly ExpenseContext _db;
        public ObservableCollection<SavingsDisplayItem> SavingsHistory { get; set; } = new();

        private int _totalSavings = 0;
        private int _monthlyTarget = 0;
        private int _savedThisMonth = 0;

        public SavingsPlannerPage()
        {
            InitializeComponent();
            _db = new ExpenseContext();
            savingsHistoryList.ItemsSource = SavingsHistory;

            Appearing += async (s, e) => await LoadSavingsData();
        }

        private async Task LoadSavingsData()
        {
            try
            {
                var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
                if (profile == null) return;

                var budgetRecord = await _db.BudgetTable.FirstOrDefaultAsync();
                decimal budget = budgetRecord != null ? budgetRecord.Amount : 0;
                int baseIncome = profile.MonthlyIncome > 0 ? profile.MonthlyIncome : (int)budget;

                // Set Strategy Info
                _monthlyTarget = (int)(baseIncome * (profile.SavingsPercentage / 100.0));
                targetSavingsLabel.Text = $"₹{_monthlyTarget:N0}";
                currentPlanLabel.Text = $"{profile.SavingsMode} Strategy ({profile.SavingsPercentage}% Savings)";
                strategySavingsPctLabel.Text = $"{profile.SavingsPercentage}%";
                strategyCommitPctLabel.Text = $"{profile.CommitmentPercentage}%";
                strategyExpensePctLabel.Text = $"{profile.ExpensePercentage}%";

                // Pre-populate salary card
                if (profile.MonthlyIncome > 0)
                    monthlySalaryEntry.Text = profile.MonthlyIncome.ToString();
                string planKey = profile.SavingsMode?.ToLower() ?? "";
                int planIdx = planKey switch
                {
                    "50/30/20" => 0,
                    "60/20/20" => 1,
                    "70/20/10" => 2,
                    "aggressive" => 3,
                    "student" => 4,
                    "custom" => 5,
                    _ => -1
                };
                savingsPlanPicker.SelectedIndex = planIdx;
                UpdateSalaryPreview();

                // Load Goals
                var goals = await _db.SavingsGoal.ToListAsync();
                _totalSavings = goals.Sum(g => g.CurrentAmount);
                totalSavingsLabel.Text = $"₹{_totalSavings:N0}";

                // Load Saved this Month
                var today = DateTime.Today;
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var txs = await _db.SavingsTransaction.OrderByDescending(t => t.Date).ToListAsync();
                _savedThisMonth = txs
                    .Where(t => t.Date >= monthStart && (t.Type == "Add" || t.Type == "Transfer"))
                    .Sum(t => t.Amount);
                savedThisMonthLabel.Text = $"₹{_savedThisMonth:N0}";

                // Remaining monthly target
                int remainingTarget = Math.Max(_monthlyTarget - _savedThisMonth, 0);
                remainingTargetLabel.Text = $"₹{remainingTarget:N0}";

                // Overall progress bar
                double progressPct = _monthlyTarget > 0 ? ((double)_savedThisMonth / _monthlyTarget) * 100.0 : 0.0;
                progressPctLabel.Text = $"{progressPct:F0}%";
                double maxBarWidth = 280.0;
                double targetWidth = Math.Min(maxBarWidth * (progressPct / 100.0), maxBarWidth);
                savingsProgressBar.WidthRequest = Math.Max(targetWidth, 4.0);

                // Build Goals UI Card Stack
                BuildGoalsUI(goals);

                // Load History List
                SavingsHistory.Clear();
                int withdrawalCountThisMonth = 0;

                foreach (var tx in txs)
                {
                    bool isAdd = tx.Type == "Add" || tx.Type == "Transfer";
                    if (tx.Type == "Withdraw" && tx.Date >= monthStart)
                    {
                        withdrawalCountThisMonth++;
                    }

                    string labelDesc = tx.Type switch
                    {
                        "Add" => string.IsNullOrWhiteSpace(tx.GoalName) ? "Added to Savings" : $"Added to {tx.GoalName}",
                        "Transfer" => string.IsNullOrWhiteSpace(tx.GoalName) ? "Salary Allocation" : $"Transferred to {tx.GoalName}",
                        "Withdraw" => string.IsNullOrWhiteSpace(tx.GoalName) ? "Withdrawn from Savings" : $"Withdrawn from {tx.GoalName}",
                        _ => "Savings Transaction"
                    };

                    SavingsHistory.Add(new SavingsDisplayItem
                    {
                        Id = tx.Id,
                        TypeEmoji = tx.Type == "Withdraw" ? "📤" : (tx.Type == "Transfer" ? "⚡" : "📥"),
                        Description = labelDesc,
                        DateStr = tx.Date.ToString("dd MMM yyyy • hh:mm tt"),
                        AmountDisplay = isAdd ? $"+₹{tx.Amount:N0}" : $"-₹{tx.Amount:N0}",
                        AmountColor = isAdd ? Color.FromArgb("#0D8C87") : Color.FromArgb("#D32F2F")
                    });
                }

                // Dynamic Smart Insights
                insightLabel1.Text = $"💡 You saved ₹{_savedThisMonth:N0} this month. " +
                    (profile.SavingsPercentage >= 30 ? "Aggressive saving strategy in action!" : "Consider upgrading strategy preset to save more.");

                if (withdrawalCountThisMonth > 0)
                {
                    insightLabel2.Text = $"⚠️ Notice: You made {withdrawalCountThisMonth} withdrawal{(withdrawalCountThisMonth > 1 ? "s" : "")} from savings this month.";
                }
                else
                {
                    var pendingGoal = goals.FirstOrDefault(g => !g.IsCompleted);
                    if (pendingGoal != null)
                    {
                        int goalDiff = pendingGoal.TargetAmount - pendingGoal.CurrentAmount;
                        insightLabel2.Text = $"🎯 Tip: Deposit to your '{pendingGoal.Name}' goal to reach your target faster!";
                    }
                    else if (goals.Any())
                    {
                        insightLabel2.Text = "🎉 Fantastic job! All your savings goals are fully completed.";
                    }
                    else
                    {
                        insightLabel2.Text = "💡 Create a new savings goal to start building dedicated wealth.";
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private void BuildGoalsUI(List<SavingsGoal> goals)
        {
            goalsStackLayout.Children.Clear();

            if (!goals.Any())
            {
                var emptyCard = new Border
                {
                    BackgroundColor = Colors.White,
                    StrokeThickness = 0,
                    Padding = new Thickness(24),
                    StrokeShape = new RoundRectangle { CornerRadius = 18 }
                };
                emptyCard.Shadow = new Shadow { Brush = Color.FromArgb("#0D000000"), Offset = new Point(0, 2), Radius = 8 };

                var emptyStack = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
                emptyStack.Children.Add(new Label { Text = "🎯", FontSize = 36, HorizontalOptions = LayoutOptions.Center });
                emptyStack.Children.Add(new Label { Text = "No Active Savings Goals", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1C2340"), HorizontalOptions = LayoutOptions.Center });
                emptyStack.Children.Add(new Label { Text = "Tap '+ Goal' in top header to add a new goal!", FontSize = 12, TextColor = Color.FromArgb("#8A94A6"), HorizontalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center });
                
                emptyCard.Content = emptyStack;
                goalsStackLayout.Children.Add(emptyCard);
                return;
            }

            foreach (var goal in goals)
            {
                var card = new Border
                {
                    BackgroundColor = Colors.White,
                    StrokeThickness = 0,
                    Padding = new Thickness(16),
                    StrokeShape = new RoundRectangle { CornerRadius = 18 }
                };
                card.Shadow = new Shadow { Brush = Color.FromArgb("#0D000000"), Offset = new Point(0, 2), Radius = 8 };

                var grid = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    },
                    RowSpacing = 12
                };

                // Row 0: Goal Icon + Title & Progress Badge
                var headerGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    ColumnSpacing = 12
                };

                var iconBorder = new Border
                {
                    WidthRequest = 42,
                    HeightRequest = 42,
                    BackgroundColor = goal.IsCompleted ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#E0F2F1"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 }
                };
                iconBorder.Content = new Label { Text = goal.CategoryIcon, FontSize = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

                var titleStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
                titleStack.Children.Add(new Label { Text = goal.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1C2340") });
                
                string deadlineStr = goal.Deadline != default ? $"Due {goal.Deadline:dd MMM yyyy}" : "Ongoing Goal";
                titleStack.Children.Add(new Label { Text = deadlineStr, FontSize = 11, TextColor = Color.FromArgb("#8A94A6") });

                double progress = goal.TargetAmount > 0 ? ((double)goal.CurrentAmount / goal.TargetAmount) * 100.0 : 0;
                
                var pctBorder = new Border
                {
                    BackgroundColor = goal.IsCompleted ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#E0F2F1"),
                    StrokeThickness = 0,
                    Padding = new Thickness(8, 4),
                    VerticalOptions = LayoutOptions.Center,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 }
                };
                pctBorder.Content = new Label
                {
                    Text = goal.IsCompleted ? "Completed 🎉" : $"{progress:F0}%",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = goal.IsCompleted ? Color.FromArgb("#2E7D32") : Color.FromArgb("#0D8C87")
                };

                headerGrid.Children.Add(iconBorder);
                headerGrid.Children.Add(titleStack);
                Grid.SetColumn(titleStack, 1);
                headerGrid.Children.Add(pctBorder);
                Grid.SetColumn(pctBorder, 2);
                grid.Children.Add(headerGrid);

                // Row 1: Amounts & Progress Bar
                var amountBarStack = new VerticalStackLayout { Spacing = 6 };
                var amountGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                };

                amountGrid.Children.Add(new Label { Text = $"₹{goal.CurrentAmount:N0}", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1C2340") });
                
                var targetLabel = new Label { Text = $"Target: ₹{goal.TargetAmount:N0}", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#8A94A6"), HorizontalOptions = LayoutOptions.End };
                amountGrid.Children.Add(targetLabel);
                Grid.SetColumn(targetLabel, 1);

                var pBarBorder = new Border { HeightRequest = 8, BackgroundColor = Color.FromArgb("#E5E7EB"), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 4 } };
                var pBarGrid = new Grid { HorizontalOptions = LayoutOptions.Start };
                double barWidth = Math.Max(260.0 * (Math.Min(progress, 100.0) / 100.0), 4.0);
                pBarGrid.Children.Add(new BoxView { Color = goal.IsCompleted ? Color.FromArgb("#2E7D32") : Color.FromArgb("#0D8C87"), WidthRequest = barWidth, CornerRadius = new CornerRadius(4) });
                pBarBorder.Content = pBarGrid;

                amountBarStack.Children.Add(amountGrid);
                amountBarStack.Children.Add(pBarBorder);
                grid.Children.Add(amountBarStack);
                Grid.SetRow(amountBarStack, 1);

                // Row 2: Action Buttons
                var actionGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    ColumnSpacing = 8
                };
                
                var depositBtn = new Button
                {
                    Text = "+ Deposit to Goal",
                    HeightRequest = 36,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 10,
                    BackgroundColor = Color.FromArgb("#0D8C87"),
                    TextColor = Colors.White
                };
                depositBtn.Clicked += async (s, e) => await DepositToGoal(goal);
                
                var withdrawBtn = new Button
                {
                    Text = "Withdraw",
                    HeightRequest = 36,
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 10,
                    BackgroundColor = Color.FromArgb("#FFEBEE"),
                    TextColor = Color.FromArgb("#D32F2F")
                };
                withdrawBtn.Clicked += async (s, e) => await WithdrawFromGoal(goal);

                var deleteBtn = new Button
                {
                    Text = "✕",
                    HeightRequest = 36,
                    WidthRequest = 36,
                    Padding = 0,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 10,
                    BackgroundColor = Color.FromArgb("#F0F3F8"),
                    TextColor = Color.FromArgb("#8A94A6")
                };
                deleteBtn.Clicked += async (s, e) => await DeleteGoal(goal);

                actionGrid.Children.Add(depositBtn);
                actionGrid.Children.Add(withdrawBtn);
                Grid.SetColumn(withdrawBtn, 1);
                actionGrid.Children.Add(deleteBtn);
                Grid.SetColumn(deleteBtn, 2);

                grid.Children.Add(actionGrid);
                Grid.SetRow(actionGrid, 2);

                card.Content = grid;
                goalsStackLayout.Children.Add(card);
            }
        }

        private async Task DepositToGoal(SavingsGoal goal)
        {
            var popup = new DepositGoalPopup(goal.Name, goal.CategoryIcon, goal.CurrentAmount, goal.TargetAmount, isWithdrawal: false);
            var res = await this.ShowPopupAsync(popup);
            if (res is int amt && amt > 0)
            {
                try
                {
                    goal.CurrentAmount += amt;
                    bool completedNow = false;

                    if (goal.CurrentAmount >= goal.TargetAmount && !goal.IsCompleted)
                    {
                        goal.IsCompleted = true;
                        completedNow = true;
                    }

                    _db.SavingsGoal.Update(goal);

                    var tx = new SavingsTransaction
                    {
                        Amount = amt,
                        Type = "Add",
                        Date = DateTime.Now,
                        Notes = $"Deposited into {goal.Name}",
                        GoalId = goal.Id,
                        GoalName = goal.Name
                    };
                    _db.SavingsTransaction.Add(tx);

                    await _db.SaveChangesAsync();

                    await UIHelper.ShowToastMessage($"Saved ₹{amt:N0} to '{goal.Name}'!");

                    if (completedNow)
                    {
                        var celebration = new GoalCompletionPopup(goal.Name, goal.TargetAmount);
                        await this.ShowPopupAsync(celebration);
                    }

                    await LoadSavingsData();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.Message, "OK");
                }
            }
        }

        private async Task WithdrawFromGoal(SavingsGoal goal)
        {
            var popup = new DepositGoalPopup(goal.Name, goal.CategoryIcon, goal.CurrentAmount, goal.TargetAmount, isWithdrawal: true);
            var res = await this.ShowPopupAsync(popup);
            if (res is int amt && amt > 0)
            {
                try
                {
                    goal.CurrentAmount -= amt;
                    if (goal.CurrentAmount < goal.TargetAmount && goal.IsCompleted)
                    {
                        goal.IsCompleted = false; // Reset completed status if withdrawn below target
                    }

                    _db.SavingsGoal.Update(goal);

                    var tx = new SavingsTransaction
                    {
                        Amount = amt,
                        Type = "Withdraw",
                        Date = DateTime.Now,
                        Notes = $"Withdrawn from {goal.Name}",
                        GoalId = goal.Id,
                        GoalName = goal.Name
                    };
                    _db.SavingsTransaction.Add(tx);

                    await _db.SaveChangesAsync();

                    await UIHelper.ShowToastMessage($"Withdrawn ₹{amt:N0} successfully!");
                    await LoadSavingsData();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.Message, "OK");
                }
            }
        }

        private async Task DeleteGoal(SavingsGoal goal)
        {
            bool confirm = await DisplayAlert("Delete Goal", $"Are you sure you want to delete goal '{goal.Name}'? This will delete the goal profile. Saved money will remain in general savings.", "Delete", "Cancel");
            if (!confirm) return;

            try
            {
                _db.SavingsGoal.Remove(goal);
                await _db.SaveChangesAsync();

                await UIHelper.ShowToastMessage("Goal deleted.");
                await LoadSavingsData();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task CreateGoalAsync()
        {
            var popup = new AddGoalPopup();
            var res = await this.ShowPopupAsync(popup);
            if (res is AddGoalResult goalRes)
            {
                try
                {
                    var newGoal = new SavingsGoal
                    {
                        Name = goalRes.Name,
                        TargetAmount = goalRes.TargetAmount,
                        CurrentAmount = 0,
                        AllocationPercentage = goalRes.AllocationPercentage,
                        Deadline = goalRes.Deadline,
                        CategoryIcon = goalRes.CategoryIcon,
                        IsCompleted = false
                    };

                    _db.SavingsGoal.Add(newGoal);
                    await _db.SaveChangesAsync();

                    await UIHelper.ShowToastMessage($"Goal '{goalRes.Name}' created successfully!");
                    await LoadSavingsData();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.Message, "OK");
                }
            }
        }

        // ─── Monthly Salary & Plan Card Handlers ──────────────────────────────

        private void UpdateSalaryPreview()
        {
            if (!int.TryParse(monthlySalaryEntry?.Text?.Trim(), out int salary) || salary <= 0)
            {
                if (salaryPreviewBorder != null) salaryPreviewBorder.IsVisible = false;
                return;
            }

            int savingsPct = savingsPlanPicker?.SelectedIndex switch
            {
                0 => 30,
                1 => 20,
                2 => 10,
                3 => 40,
                4 => 25,
                _ => 0
            };

            if (savingsPct == 0)
            {
                if (salaryPreviewBorder != null) salaryPreviewBorder.IsVisible = false;
                return;
            }

            int savingsAmt = (int)(salary * savingsPct / 100.0);
            int spendAmt   = salary - savingsAmt;

            previewSavingsLabel.Text  = $"₹{savingsAmt:N0}";
            previewSpendLabel.Text    = $"₹{spendAmt:N0}";
            salaryPreviewBorder.IsVisible = true;
        }

        private void OnSalaryEntryCompleted(object sender, EventArgs e) => UpdateSalaryPreview();

        private void OnSavingsPlanChanged(object sender, EventArgs e) => UpdateSalaryPreview();

        private async void OnApplySalaryPlanClicked(object sender, EventArgs e)
        {
            if (!int.TryParse(monthlySalaryEntry.Text?.Trim(), out int salary) || salary <= 0)
            {
                await DisplayAlert("Validation", "Please enter a valid monthly salary.", "OK");
                return;
            }

            int planIdx = savingsPlanPicker.SelectedIndex;
            if (planIdx < 0)
            {
                await DisplayAlert("Validation", "Please choose a savings plan.", "OK");
                return;
            }

            // Map selection to strategy values
            (string modeName, int savPct, int comPct, int expPct) = planIdx switch
            {
                0 => ("50/30/20", 30, 50, 20),
                1 => ("60/20/20", 20, 60, 20),
                2 => ("70/20/10", 10, 70, 20),
                3 => ("Aggressive", 40, 40, 20),
                4 => ("Student", 25, 55, 20),
                _ => ("Custom", 20, 50, 30)
            };

            try
            {
                var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
                if (profile != null)
                {
                    profile.MonthlyIncome         = salary;
                    profile.SavingsMode           = modeName;
                    profile.SavingsPercentage     = savPct;
                    profile.CommitmentPercentage  = comPct;
                    profile.ExpensePercentage     = expPct;
                    _db.UserFinancialProfile.Update(profile);
                }
                else
                {
                    _db.UserFinancialProfile.Add(new UserFinancialProfile
                    {
                        MonthlyIncome        = salary,
                        SavingsMode          = modeName,
                        SavingsPercentage    = savPct,
                        CommitmentPercentage = comPct,
                        ExpensePercentage    = expPct
                    });
                }

                // Also sync the BudgetTable amount
                var budgetRecord = await _db.BudgetTable.FirstOrDefaultAsync();
                if (budgetRecord != null)
                {
                    budgetRecord.Amount = salary;
                    _db.BudgetTable.Update(budgetRecord);
                }

                await _db.SaveChangesAsync();
                await UIHelper.ShowToastMessage($"✅ Plan applied! Saving ₹{(int)(salary * savPct / 100.0):N0}/month");
                await LoadSavingsData();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnAddGoalClicked(object? sender, EventArgs e)
        {
            await CreateGoalAsync();
        }

        private async void OnChangePlanClicked(object sender, EventArgs e)
        {
            var popup = new ChangeStrategyPopup();
            var res = await this.ShowPopupAsync(popup);
            if (res is bool updated && updated)
            {
                await LoadSavingsData();
            }
        }

        private async void OnAddSavingsTapped(object sender, EventArgs e)
        {
            var goals = await _db.SavingsGoal.ToListAsync();
            if (!goals.Any())
            {
                await CreateGoalAsync();
                return;
            }

            string[] goalOptions = goals.Select(g => g.Name).Concat(new[] { "Auto-Allocate among Goals" }).ToArray();
            string selected = await DisplayActionSheet("Select Savings Goal to Save Into:", "Cancel", null, goalOptions);
            if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

            if (selected == "Auto-Allocate among Goals")
            {
                string res = await DisplayPromptAsync("Auto-Allocate Deposit", "Enter total savings amount to allocate across goals:", "Deposit", "Cancel", "Amount (₹)", -1, Keyboard.Numeric);
                if (string.IsNullOrWhiteSpace(res) || !int.TryParse(res, out int amt) || amt <= 0) return;

                await AutoAllocateSavings(amt);
            }
            else
            {
                var targetGoal = goals.FirstOrDefault(g => g.Name == selected);
                if (targetGoal != null) await DepositToGoal(targetGoal);
            }
        }

        private async Task AutoAllocateSavings(int totalAmount)
        {
            try
            {
                var goals = await _db.SavingsGoal.ToListAsync();
                if (!goals.Any()) return;

                int allocatedSum = goals.Sum(g => g.AllocationPercentage);
                if (allocatedSum == 0)
                {
                    int equalAmt = totalAmount / goals.Count;
                    foreach (var goal in goals)
                    {
                        goal.CurrentAmount += equalAmt;
                        _db.SavingsGoal.Update(goal);

                        var tx = new SavingsTransaction
                        {
                            Amount = equalAmt,
                            Type = "Add",
                            Date = DateTime.Now,
                            Notes = $"Auto-allocated to {goal.Name}",
                            GoalId = goal.Id,
                            GoalName = goal.Name
                        };
                        _db.SavingsTransaction.Add(tx);
                    }
                }
                else
                {
                    int remainingAmt = totalAmount;
                    foreach (var goal in goals)
                    {
                        int portion = (int)(totalAmount * (goal.AllocationPercentage / (double)allocatedSum));
                        if (portion > 0)
                        {
                            goal.CurrentAmount += portion;
                            _db.SavingsGoal.Update(goal);
                            remainingAmt -= portion;

                            var tx = new SavingsTransaction
                            {
                                Amount = portion,
                                Type = "Add",
                                Date = DateTime.Now,
                                Notes = $"Auto-allocated to {goal.Name}",
                                GoalId = goal.Id,
                                GoalName = goal.Name
                            };
                            _db.SavingsTransaction.Add(tx);
                        }
                    }

                    if (remainingAmt > 0 && goals.Any())
                    {
                        var first = goals.First();
                        first.CurrentAmount += remainingAmt;
                        _db.SavingsGoal.Update(first);

                        var tx = new SavingsTransaction
                        {
                            Amount = remainingAmt,
                            Type = "Add",
                            Date = DateTime.Now,
                            Notes = $"Auto-allocated remainder to {first.Name}",
                            GoalId = first.Id,
                            GoalName = first.Name
                        };
                        _db.SavingsTransaction.Add(tx);
                    }
                }

                await _db.SaveChangesAsync();
                await UIHelper.ShowToastMessage($"Allocated ₹{totalAmount:N0} successfully!");
                await LoadSavingsData();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnWithdrawSavingsTapped(object sender, EventArgs e)
        {
            var goals = await _db.SavingsGoal.ToListAsync();
            if (!goals.Any()) return;

            string[] goalOptions = goals.Select(g => g.Name).ToArray();
            string selected = await DisplayActionSheet("Withdraw from which Goal?", "Cancel", null, goalOptions);
            if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

            var targetGoal = goals.FirstOrDefault(g => g.Name == selected);
            if (targetGoal != null) await WithdrawFromGoal(targetGoal);
        }

        private async void OnTransferSavingsTapped(object sender, EventArgs e)
        {
            var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
            if (profile == null) return;

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var expenses = await _db.ExpenseTable.Where(x => x.Date >= monthStart && x.Date <= monthEnd).ToListAsync();
            decimal monthTotal = expenses.Sum(x => x.Expenses);

            var rawLendingTxs = await _db.LendingTransaction.ToListAsync();
            int outstandingLentVal = rawLendingTxs.Where(l => l.Status != "Recovered").Sum(l => l.Amount);

            var rawCommitments = await _db.MonthlyCommitment.ToListAsync();
            int commitmentsTotalVal = rawCommitments.Where(c => c.IsActive).Sum(c => c.Amount);

            int savingsTargetVal = (int)(profile.MonthlyIncome * (profile.SavingsPercentage / 100.0));
            int availableBalanceVal = (int)(profile.MonthlyIncome - commitmentsTotalVal - savingsTargetVal - monthTotal - outstandingLentVal);
            availableBalanceVal = Math.Max(availableBalanceVal, 0);

            string res = await DisplayPromptAsync("Transfer from Available", $"Enter amount to transfer to savings:\n(Available safe-to-spend: ₹{availableBalanceVal:N0})", "Transfer", "Cancel", "Amount (₹)", -1, Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(res) || !int.TryParse(res, out int amt) || amt <= 0) return;

            var goals = await _db.SavingsGoal.ToListAsync();
            if (!goals.Any()) return;

            string[] goalOptions = goals.Select(g => g.Name).Concat(new[] { "Auto-Allocate among Goals" }).ToArray();
            string selected = await DisplayActionSheet("Where to transfer funds?", "Cancel", null, goalOptions);
            if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

            if (selected == "Auto-Allocate among Goals")
            {
                await AutoAllocateSavings(amt);
            }
            else
            {
                var targetGoal = goals.FirstOrDefault(g => g.Name == selected);
                if (targetGoal != null)
                {
                    try
                    {
                        targetGoal.CurrentAmount += amt;
                        _db.SavingsGoal.Update(targetGoal);

                        var tx = new SavingsTransaction
                        {
                            Amount = amt,
                            Type = "Transfer",
                            Date = DateTime.Now,
                            Notes = $"Transferred from Available to {targetGoal.Name}",
                            GoalId = targetGoal.Id,
                            GoalName = targetGoal.Name
                        };
                        _db.SavingsTransaction.Add(tx);

                        await _db.SaveChangesAsync();
                        await UIHelper.ShowToastMessage("Transfer completed successfully!");
                        await LoadSavingsData();
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", ex.Message, "OK");
                    }
                }
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
