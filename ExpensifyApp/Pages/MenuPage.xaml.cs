using System;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ExpensifyApp.Pages;

public partial class MenuPage : ContentPage
{
    private readonly ExpenseContext _dbContext;
   
    public MenuPage()
    {
        InitializeComponent();
        _dbContext = new ExpenseContext();
    }

   

    private async void SaveEntry(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expenseEntry.Text) || !int.TryParse(expenseEntry.Text, out int amount))
            {
                await DisplayAlert("Validation Error", "Please enter a valid expense amount.", "OK");
                return;
            }

            if (payModePicker.SelectedIndex == -1 || payModePicker.SelectedItem == null)
            {
                await DisplayAlert("Validation Error", "Please select a payment mode.", "OK");
                return;
            }

            var newExpense = new ExpenseTable
            {
                Category = categoryEntry.Text,
                SubCategory = subCategoryEntry.Text,
                PayMode = payModePicker.SelectedItem.ToString(),
                Expenses = amount,
                Date = datePicker.Date.Date.Add(DateTime.Now.TimeOfDay)
            };
            
            _dbContext.ExpenseTable.Add(newExpense);
            await _dbContext.SaveChangesAsync();

            // Run round-up check
            await AutoSaveHelper.HandleRoundUp(_dbContext, amount);

            await DisplayAlert("Success", "Expense saved!", "OK");

            ExpenseFormPanel.IsVisible = false;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void HidePanel(object sender, EventArgs e)
    {
        ExpenseFormPanel.IsVisible = false;
    }

    private async void OnCategoryClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is string category)
        {
            // Show form
            ExpenseFormPanel.IsVisible = true;

            // Fill category
            categoryEntry.Text = category;
            subCategoryEntry.Text = string.Empty;
            expenseEntry.Text = string.Empty;
            payModePicker.SelectedIndex = -1;
            datePicker.Date = DateTime.Today;

            // Smooth scroll to the form
            await Task.Delay(100); // Small delay to ensure layout is updated
            await (this.Content as ScrollView)?.ScrollToAsync(ExpenseFormPanel, ScrollToPosition.MakeVisible, true);
        }
    }

    private void HideExpenseForm(object sender, EventArgs e)
    {
        ExpenseFormPanel.IsVisible = false;
    }
}