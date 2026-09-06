using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages
{
    public partial class ChangeStrategyPopup : Popup
    {
        private readonly ExpenseContext _db;
        private string _activePreset = "50/30/20";
        private bool _isInitialLoading = true;

        public ChangeStrategyPopup()
        {
            InitializeComponent();
            _db = new ExpenseContext();
            
            // Execute loading asynchronously
            Task.Run(async () => await LoadCurrentStrategy());
        }

        private async Task LoadCurrentStrategy()
        {
            try
            {
                var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
                if (profile == null) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isInitialLoading = true;

                    _activePreset = profile.SavingsMode;
                    commitSlider.Value = profile.CommitmentPercentage;
                    savingsSlider.Value = profile.SavingsPercentage;
                    expensesSlider.Value = profile.ExpensePercentage;

                    UpdateLabels();
                    HighlightActivePresetCard();

                    _isInitialLoading = false;
                });
            }
            catch { }
        }

        private void HighlightActivePresetCard()
        {
            preset1Card.Stroke = _activePreset == "50/30/20" ? Color.FromArgb("#0D8C87") : Color.FromArgb("#E5E7EB");
            preset1Card.StrokeThickness = _activePreset == "50/30/20" ? 2 : 1;

            preset2Card.Stroke = _activePreset == "60/20/20" ? Color.FromArgb("#0D8C87") : Color.FromArgb("#E5E7EB");
            preset2Card.StrokeThickness = _activePreset == "60/20/20" ? 2 : 1;

            preset3Card.Stroke = _activePreset == "70/20/10" ? Color.FromArgb("#0D8C87") : Color.FromArgb("#E5E7EB");
            preset3Card.StrokeThickness = _activePreset == "70/20/10" ? 2 : 1;

            preset4Card.Stroke = _activePreset == "Aggressive" ? Color.FromArgb("#0D8C87") : Color.FromArgb("#E5E7EB");
            preset4Card.StrokeThickness = _activePreset == "Aggressive" ? 2 : 1;

            preset5Card.Stroke = _activePreset == "Student" ? Color.FromArgb("#0D8C87") : Color.FromArgb("#E5E7EB");
            preset5Card.StrokeThickness = _activePreset == "Student" ? 2 : 1;

            preset6Card.Stroke = _activePreset == "Custom" ? Color.FromArgb("#0D8C87") : Color.FromArgb("#E5E7EB");
            preset6Card.StrokeThickness = _activePreset == "Custom" ? 2 : 1;
        }

        private void OnPresetSelect(object sender, EventArgs e)
        {
            var border = sender as Border;
            var gesture = border?.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault();
            string preset = gesture?.CommandParameter as string;

            if (string.IsNullOrEmpty(preset)) return;

            _activePreset = preset;
            _isInitialLoading = true;

            switch (preset)
            {
                case "50/30/20":
                    commitSlider.Value = 50;
                    savingsSlider.Value = 30;
                    expensesSlider.Value = 20;
                    break;
                case "60/20/20":
                    commitSlider.Value = 60;
                    savingsSlider.Value = 20;
                    expensesSlider.Value = 20;
                    break;
                case "70/20/10":
                    commitSlider.Value = 70;
                    savingsSlider.Value = 20;
                    expensesSlider.Value = 10;
                    break;
                case "Aggressive":
                    commitSlider.Value = 40;
                    savingsSlider.Value = 40;
                    expensesSlider.Value = 20;
                    break;
                case "Student":
                    commitSlider.Value = 0;
                    savingsSlider.Value = 25;
                    expensesSlider.Value = 75;
                    break;
                case "Custom":
                    // Keep values as is
                    break;
            }

            HighlightActivePresetCard();
            UpdateLabels();
            _isInitialLoading = false;
        }

        private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (_isInitialLoading) return;

            // Any manual slider movement switches plan to Custom
            if (_activePreset != "Custom")
            {
                _activePreset = "Custom";
                HighlightActivePresetCard();
            }

            UpdateLabels();
        }

        private void UpdateLabels()
        {
            int c = (int)Math.Round(commitSlider.Value);
            int s = (int)Math.Round(savingsSlider.Value);
            int e = (int)Math.Round(expensesSlider.Value);

            commitValLabel.Text = $"{c}%";
            savingsValLabel.Text = $"{s}%";
            expensesValLabel.Text = $"{e}%";

            int total = c + s + e;
            statusTextLabel.Text = $"Total Allocation: {total}%";

            if (total == 100)
            {
                statusBorder.BackgroundColor = Color.FromArgb("#E8F5E9");
                statusTextLabel.TextColor = Color.FromArgb("#2E7D32");
                statusTextLabel.Text += " (Valid)";
                saveBtn.IsEnabled = true;
                saveBtn.Opacity = 1.0;
            }
            else
            {
                statusBorder.BackgroundColor = Color.FromArgb("#FFEBEE");
                statusTextLabel.TextColor = Color.FromArgb("#C62828");
                statusTextLabel.Text += " (Must equal 100%)";
                saveBtn.IsEnabled = false;
                saveBtn.Opacity = 0.5;
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            int c = (int)Math.Round(commitSlider.Value);
            int s = (int)Math.Round(savingsSlider.Value);
            int valE = (int)Math.Round(expensesSlider.Value);

            if (c + s + valE != 100)
            {
                return;
            }

            try
            {
                var profile = await _db.UserFinancialProfile.FirstOrDefaultAsync();
                if (profile != null)
                {
                    profile.CommitmentPercentage = c;
                    profile.SavingsPercentage = s;
                    profile.ExpensePercentage = valE;
                    profile.SavingsMode = _activePreset;

                    _db.UserFinancialProfile.Update(profile);
                    await _db.SaveChangesAsync();
                }

                Close(true);
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            Close(false);
        }
    }
}
