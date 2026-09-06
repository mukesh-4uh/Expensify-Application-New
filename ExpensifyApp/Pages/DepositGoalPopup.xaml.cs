using CommunityToolkit.Maui.Views;
using System;
using Microsoft.Maui.Controls;

namespace ExpensifyApp.Pages
{
    public partial class DepositGoalPopup : Popup
    {
        private readonly bool _isWithdrawal;
        private readonly int _currentAmount;

        public DepositGoalPopup(string goalName, string goalIcon, int currentAmount, int targetAmount, bool isWithdrawal = false)
        {
            InitializeComponent();

            _isWithdrawal = isWithdrawal;
            _currentAmount = currentAmount;

            headerGoalNameLabel.Text = goalName;
            headerIconLabel.Text = string.IsNullOrWhiteSpace(goalIcon) ? (isWithdrawal ? "📤" : "📥") : goalIcon;
            currentAmountLabel.Text = $"₹{currentAmount:N0}";
            targetAmountLabel.Text = $"₹{targetAmount:N0}";

            if (isWithdrawal)
            {
                headerTitleLabel.Text = "Withdraw from Goal";
                inputFieldHeader.Text = "WITHDRAW AMOUNT";
                actionButton.Text = "Withdraw 📤";
                actionButton.BackgroundColor = Color.FromArgb("#D32F2F");
            }
        }

        private void OnQuickPresetTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter != null && int.TryParse(e.Parameter.ToString(), out int val))
            {
                amountEntry.Text = val.ToString();
            }
        }

        private void OnActionClicked(object sender, EventArgs e)
        {
            errorLabel.IsVisible = false;

            string txt = amountEntry.Text?.Trim() ?? "";
            if (!int.TryParse(txt, out int amt) || amt <= 0)
            {
                errorLabel.Text = "❌ Please enter a valid amount greater than 0";
                errorLabel.IsVisible = true;
                return;
            }

            if (_isWithdrawal && amt > _currentAmount)
            {
                errorLabel.Text = $"❌ Cannot withdraw ₹{amt:N0}. Max balance: ₹{_currentAmount:N0}";
                errorLabel.IsVisible = true;
                return;
            }

            Close(amt);
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }
    }
}
