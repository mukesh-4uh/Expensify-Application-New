using CommunityToolkit.Maui.Views;

namespace ExpensifyApp.Pages;

public partial class ReportsPopup : Popup
{
	public ReportsPopup()
	{
		InitializeComponent();
	}

    

    private async void monthlyReportsButton_Clicked(object sender, EventArgs e)
    {
        await Application.Current.MainPage.Navigation.PushAsync(new Pages.MonthlyReportsPage());
        Close();
    }

    private async void customReportsButton_Clicked(object sender, EventArgs e)
    {
        await Application.Current.MainPage.Navigation.PushAsync(new Pages.CustomReportsPage());
        Close();
    }
}