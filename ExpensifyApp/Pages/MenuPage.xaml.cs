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
  

    private async void reportsButton_Clicked(object sender, EventArgs e)
    {
        var popup = new ReportsPopup();
        this.ShowPopup(popup);
    }

    private async void SaveEntry(object sender, EventArgs e)
    {
        try
        {
            var newExpense = new ExpenseTable
            {
                Category = categoryEntry.Text,
                SubCategory = subCategoryEntry.Text,
                PayMode = payModePicker.SelectedItem?.ToString(),
                Expenses = int.Parse(expenseEntry.Text),
                Date = datePicker.Date
            };
            
            _dbContext.ExpenseTable.Add(newExpense);
            await _dbContext.SaveChangesAsync();

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
            await(this.Content as ScrollView)?.ScrollToAsync(ExpenseFormPanel, ScrollToPosition.MakeVisible, true);
        }
    }

    private void HideExpenseForm(object sender, EventArgs e)
    {
        ExpenseFormPanel.IsVisible = false;
    }
}