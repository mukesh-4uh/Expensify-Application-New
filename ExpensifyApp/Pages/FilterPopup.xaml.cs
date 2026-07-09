using CommunityToolkit.Maui.Views;

namespace ExpensifyApp.Pages;

public partial class FilterPopup : Popup
{
    public event Action<string>? OptionSelected;

    public FilterPopup()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public Command DateCommand => new(() =>
    {
        OptionSelected?.Invoke("Date");
        Close();
    });

    public Command WeeklyCommand => new(() =>
    {
        OptionSelected?.Invoke("Weekly");
        Close();
    });

    public Command MonthlyCommand => new(() =>
    {
        OptionSelected?.Invoke("Monthly");
        Close();
    });
}
