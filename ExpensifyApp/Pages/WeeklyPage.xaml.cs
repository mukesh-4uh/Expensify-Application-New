namespace ExpensifyApp.Pages;

public partial class WeeklyPage : ContentPage
{
   
        public WeeklyPage()
        {
            InitializeComponent();
        }

        private async void OnSwipeUp(object sender, SwipedEventArgs e)
        {
            ReportPanel.IsVisible = true;
            await ReportPanel.TranslateTo(0, 0, 300, Easing.SinOut);
            await ReportPanel.FadeTo(1, 200);
        }

        private async void OnSwipeDown(object sender, SwipedEventArgs e)
        {
            await ReportPanel.TranslateTo(0, 1000, 300, Easing.SinIn);
            ReportPanel.IsVisible = false;
        }
}
