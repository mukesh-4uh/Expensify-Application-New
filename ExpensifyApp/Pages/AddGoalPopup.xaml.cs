using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using System;

namespace ExpensifyApp.Pages
{
    public class AddGoalResult
    {
        public string Name { get; set; } = "";
        public int TargetAmount { get; set; }
        public int AllocationPercentage { get; set; }
        public DateTime Deadline { get; set; }
        public string CategoryIcon { get; set; } = "🎯";
    }

    public partial class AddGoalPopup : Popup
    {
        private readonly string[] _icons = {
            "🎯 General Goal",
            "✈️ Vacation / Trip",
            "📱 Gadgets / Phone",
            "🚗 Car / Vehicle",
            "🏠 House / Flat",
            "🚨 Emergency Fund",
            "💍 Wedding",
            "🪙 Gold / Investment",
            "🎓 Education"
        };

        public AddGoalPopup()
        {
            InitializeComponent();

            foreach (var item in _icons)
            {
                iconPicker.Items.Add(item);
            }
            iconPicker.SelectedIndex = 0;
            deadlinePicker.Date = DateTime.Today.AddMonths(6);
            deadlinePicker.MinimumDate = DateTime.Today;
        }

        private void OnCreateClicked(object sender, EventArgs e)
        {
            errorLabel.IsVisible = false;

            string title = nameEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title))
            {
                errorLabel.Text = "❌ Please enter a goal title";
                errorLabel.IsVisible = true;
                return;
            }

            string targetStr = targetAmountEntry.Text?.Trim() ?? "";
            if (!int.TryParse(targetStr, out int targetAmt) || targetAmt <= 0)
            {
                errorLabel.Text = "❌ Please enter a valid target amount (greater than 0)";
                errorLabel.IsVisible = true;
                return;
            }

            int allocPct = 0;
            string allocStr = allocationEntry.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(allocStr))
            {
                if (!int.TryParse(allocStr, out allocPct) || allocPct < 0 || allocPct > 100)
                {
                    errorLabel.Text = "❌ Allocation percentage must be between 0 and 100";
                    errorLabel.IsVisible = true;
                    return;
                }
            }

            string selectedIcon = GetIconFromChoice(iconPicker.SelectedIndex, title);

            Close(new AddGoalResult
            {
                Name = title,
                TargetAmount = targetAmt,
                AllocationPercentage = allocPct,
                Deadline = deadlinePicker.Date,
                CategoryIcon = selectedIcon
            });
        }

        private string GetIconFromChoice(int index, string title)
        {
            if (index >= 0 && index < _icons.Length)
            {
                string choice = _icons[index];
                if (choice.Contains("Vacation")) return "✈️";
                if (choice.Contains("Gadgets")) return "📱";
                if (choice.Contains("Car")) return "🚗";
                if (choice.Contains("House")) return "🏠";
                if (choice.Contains("Emergency")) return "🚨";
                if (choice.Contains("Wedding")) return "💍";
                if (choice.Contains("Gold")) return "🪙";
                if (choice.Contains("Education")) return "🎓";
            }

            string lower = title.ToLower();
            if (lower.Contains("trip") || lower.Contains("vacation") || lower.Contains("goa")) return "✈️";
            if (lower.Contains("phone") || lower.Contains("iphone") || lower.Contains("device")) return "📱";
            if (lower.Contains("car") || lower.Contains("bike")) return "🚗";
            if (lower.Contains("house") || lower.Contains("rent") || lower.Contains("flat")) return "🏠";
            if (lower.Contains("emergency")) return "🚨";
            return "🎯";
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(null);
        }
    }
}
