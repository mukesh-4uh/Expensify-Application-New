using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages
{
    public partial class ExpensePopup : Popup
    {
        private readonly ExpenseContext _dbContext;

       

        public ExpensePopup(string? category)
        {
            InitializeComponent();

            _dbContext = new ExpenseContext();
           

            if (!string.IsNullOrEmpty(category))
            {
                categoryEntry.Text = category;
            }
        }

        private async void saveButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                string category = categoryEntry.Text;
                string subCategory = subCategoryEntry.Text;
                string payMode = payModePicker.SelectedItem?.ToString();
                int expense;

               
                if (string.IsNullOrWhiteSpace(category) ||
                    string.IsNullOrWhiteSpace(subCategory) ||
                    string.IsNullOrWhiteSpace(payMode) ||
                    !int.TryParse(expenseEntry.Text, out expense) ||
                         datePicker.Date > DateTime.Today)
                {
                    await UIHelper.ShowMessage("Please enter all fields correctly");
                    return;
                }

                var newExpense = new ExpenseTable
                {
                    Category = category,
                    SubCategory = subCategory,
                    PayMode = payMode,
                    Expenses = expense,
                    Date = datePicker.Date
                    
                };

                _dbContext.ExpenseTable.Add(newExpense);
                await _dbContext.SaveChangesAsync();
               

                await UIHelper.ShowDebugToastMessage("Expense saved successfully");
                Close();

            }
            catch (DbUpdateException ex)
            {
                await UIHelper.ShowErrorMessage($"Database error: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                await UIHelper.HandleException(ex);
            }
        }

        private void cancelButton_Clicked(object sender, EventArgs e)
        {
            expensePopup.Close();
        }
    }
}
