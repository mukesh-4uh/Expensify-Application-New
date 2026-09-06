using CommunityToolkit.Maui.Views;

namespace ExpensifyApp.Pages;

/// <summary>
/// Result returned from the RecurringPopup when the user confirms.
/// </summary>
public class RecurringResult
{
    public int DayOfMonth { get; set; }
}

public partial class RecurringPopup : Popup
{
    private readonly int _originalDay;
    private bool _useCustomDay = false;

    public RecurringPopup(string category, int amount, int originalDayOfMonth)
    {
        InitializeComponent();

        _originalDay = originalDayOfMonth;

        // Display expense info
        infoLabel.Text = category;
        amountLabel.Text = $"₹{amount:N0}";
        sameDateLabel.Text = $"Repeats on the {GetDaySuffix(originalDayOfMonth)} of every month";

        // Populate day picker (1–31)
        for (int i = 1; i <= 31; i++)
            dayPicker.Items.Add(i.ToString());
        dayPicker.SelectedIndex = originalDayOfMonth - 1;

        // Default: same date selected
        SelectSameDate();
    }

    private void OnSameDateTapped(object sender, EventArgs e) => SelectSameDate();
    private void OnCustomDateTapped(object sender, EventArgs e) => SelectCustomDate();

    private void SelectSameDate()
    {
        _useCustomDay = false;

        // Option 1 — active
        optionSameDate.BackgroundColor = Color.FromArgb("#E0F2F1");
        optionSameDate.Stroke = Color.FromArgb("#0D8C87");
        optionSameDate.StrokeThickness = 2;
        dotSame.Color = Color.FromArgb("#0D8C87");

        // Option 2 — inactive
        optionCustomDate.BackgroundColor = Colors.White;
        optionCustomDate.Stroke = Color.FromArgb("#E5E7EB");
        optionCustomDate.StrokeThickness = 1;
        dotCustom.Color = Colors.Transparent;

        // Hide day picker
        customDaySection.IsVisible = false;
    }

    private void SelectCustomDate()
    {
        _useCustomDay = true;

        // Option 1 — inactive
        optionSameDate.BackgroundColor = Colors.White;
        optionSameDate.Stroke = Color.FromArgb("#E5E7EB");
        optionSameDate.StrokeThickness = 1;
        dotSame.Color = Colors.Transparent;

        // Option 2 — active
        optionCustomDate.BackgroundColor = Color.FromArgb("#FFF3E0");
        optionCustomDate.Stroke = Color.FromArgb("#FF9800");
        optionCustomDate.StrokeThickness = 2;
        dotCustom.Color = Color.FromArgb("#FF9800");

        // Show day picker
        customDaySection.IsVisible = true;
    }

    private void OnConfirmClicked(object sender, EventArgs e)
    {
        int day;

        if (_useCustomDay)
        {
            if (dayPicker.SelectedIndex < 0)
            {
                // Default to original day if nothing selected
                day = _originalDay;
            }
            else
            {
                day = dayPicker.SelectedIndex + 1;
            }
        }
        else
        {
            day = _originalDay;
        }

        Close(new RecurringResult { DayOfMonth = day });
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(null);
    }

    private static string GetDaySuffix(int day)
    {
        if (day >= 11 && day <= 13) return $"{day}th";
        return (day % 10) switch
        {
            1 => $"{day}st",
            2 => $"{day}nd",
            3 => $"{day}rd",
            _ => $"{day}th"
        };
    }
}
