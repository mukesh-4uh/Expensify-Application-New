using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel; 

namespace ExpensifyApp.Pages
{
    public partial class CustomReportsPage : ContentPage
    {
        private readonly ExpenseContext _dbContext;
        private ObservableCollection<ExpenseTable> CustomExpenses { get; set; } = new ObservableCollection<ExpenseTable>();

        public CustomReportsPage()
        {
            InitializeComponent();
            _dbContext = new ExpenseContext();
            expenses.ItemsSource = CustomExpenses;
        }

        private async void OnDateChanged(object sender, DateChangedEventArgs e)
        {
            await LoadExpenses();
        }

        private async Task LoadExpenses()
        {
            try
            {
                if (StartDatePicker.Date == null || EndDatePicker.Date == null)
                    return;

                DateTime startDate = StartDatePicker.Date;
                DateTime endDate = EndDatePicker.Date;

                if (startDate > endDate)
                {
                    await DisplayAlert("Invalid Date", "Start Date cannot be after End Date.", "OK");
                    return;
                }

                CustomExpenses.Clear();
                totalAmountLabel.Text = string.Empty;
                totalAmountLabel.IsVisible = false;

                var expenseList = await Task.Run(() => _dbContext.ExpenseTable
                    .Where(x => x.Date.Date >= startDate.Date && x.Date.Date <= endDate.Date)
                    .ToList());

                if (expenseList.Any())
                {
                    foreach (var item in expenseList)
                    {
                        CustomExpenses.Add(item);
                    }
                    totalAmountLabel.IsVisible = true;
                    decimal totalExpense = expenseList.Sum(x => x.Expenses);
                    totalAmountLabel.Text = $"Total: {totalExpense.ToString("C", new CultureInfo("en-IN"))}";
                }
                else
                {
                    await UIHelper.ShowToastMessage("No expenses found for the selected dates.");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error loading expenses: {ex.Message}", "OK");
            }
        }

        private async void EditButton_Clicked(object sender, EventArgs e)
        {
            if (sender is ImageButton button && button.BindingContext is ExpenseTable selectedExpense)
            {
                string action = await DisplayActionSheet("Select field to edit", "Cancel", null,
                    "Date", "Category", "SubCategory", "Expense", "Payment Mode");

                if (action == "Cancel" || string.IsNullOrEmpty(action))
                    return;

                try
                {
                    bool isEdited = false;

                    switch (action)
                    {
                        case "Date":
                            string newDateStr = await DisplayPromptAsync("Edit Date", "Enter new date (dd/MM/yyyy):", initialValue: selectedExpense.Date.ToString("dd/MM/yyyy"));
                            if (DateTime.TryParse(newDateStr, out DateTime newDate))
                            {
                                selectedExpense.Date = newDate;
                                isEdited = true;
                            }
                            else
                            {
                                await DisplayAlert("Invalid Input", "Please enter a valid date.", "OK");
                            }
                            break;

                        case "Category":
                            string newCategory = await DisplayPromptAsync("Edit Category", "Enter new category:", initialValue: selectedExpense.Category);
                            if (!string.IsNullOrEmpty(newCategory))
                            {
                                selectedExpense.Category = newCategory;
                                isEdited = true;
                            }
                            break;

                        case "SubCategory":
                            string newSubCategory = await DisplayPromptAsync("Edit SubCategory", "Enter new subcategory:", initialValue: selectedExpense.SubCategory);
                            if (!string.IsNullOrEmpty(newSubCategory))
                            {
                                selectedExpense.SubCategory = newSubCategory;
                                isEdited = true;
                            }
                            break;

                        case "Expense":
                            string newExpenseStr = await DisplayPromptAsync("Edit Expense", "Enter new expense amount:", initialValue: selectedExpense.Expenses.ToString());
                            if (decimal.TryParse(newExpenseStr, out decimal newExpense))
                            {
                                selectedExpense.Expenses = (int)newExpense;
                                isEdited = true;
                            }
                            else
                            {
                                await DisplayAlert("Invalid Input", "Expense must be a valid number.", "OK");
                            }
                            break;

                        case "Payment Mode":
                            string newPayMode = await DisplayPromptAsync("Edit Payment Mode", "Enter new payment mode:", initialValue: selectedExpense.PayMode);
                            if (!string.IsNullOrEmpty(newPayMode))
                            {
                                selectedExpense.PayMode = newPayMode;
                                isEdited = true;
                            }
                            break;
                    }

                    if (isEdited)
                    {
                        _dbContext.ExpenseTable.Update(selectedExpense);
                        await _dbContext.SaveChangesAsync();

                        await UIHelper.ShowToastMessage("Expense updated successfully!");

                        
                        var index = CustomExpenses.IndexOf(selectedExpense);
                        if (index >= 0)
                        {
                            CustomExpenses.RemoveAt(index);
                            CustomExpenses.Insert(index, selectedExpense);
                        }
                        UpdateTotalAmount(); 
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Error updating expense: {ex.Message}", "OK");
                }
            }
        }

        private async void DeleteButton_Clicked(object sender, EventArgs e)
        {
            if (sender is ImageButton button && button.BindingContext is ExpenseTable selectedExpense)
            {
                bool confirm = await DisplayAlert("Confirm Delete", "Are you sure you want to delete this expense?", "Yes", "No");

                if (confirm)
                {
                    try
                    {
                        _dbContext.ExpenseTable.Remove(selectedExpense);
                        await _dbContext.SaveChangesAsync();

                        CustomExpenses.Remove(selectedExpense);

                        await UIHelper.ShowToastMessage("Expense deleted successfully!");

                        UpdateTotalAmount(); // Update total after delete
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", $"Error deleting expense: {ex.Message}", "OK");
                    }
                }
            }
        }

        private void UpdateTotalAmount()
        {
            decimal totalExpense = CustomExpenses.Sum(x => x.Expenses);
            totalAmountLabel.Text = $"Total: {totalExpense.ToString("C", new CultureInfo("en-IN"))}";
        }
    }
}
