using CommunityToolkit.Maui.Views;
using System;
using Microsoft.Maui.ApplicationModel;

namespace ExpensifyApp.Pages
{
    public partial class GoalCompletionPopup : Popup
    {
        private string _goalName;
        private int _amountSaved;

        public GoalCompletionPopup(string goalName, int amountSaved)
        {
            InitializeComponent();
            _goalName = goalName;
            _amountSaved = amountSaved;

            goalNameLabel.Text = goalName;
            amountSavedLabel.Text = $"₹{amountSaved:N0} Saved";
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            try
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = "Goal Completed! 🎉",
                    Text = $"I just reached my savings goal for '{_goalName}' and saved ₹{_amountSaved:N0} on Expensify! 🏆💰"
                });
            }
            catch { }
        }

        private void OnDismissClicked(object sender, EventArgs e)
        {
            Close();
        }
    }
}
