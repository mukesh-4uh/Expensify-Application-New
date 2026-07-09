using ExpensifyApp.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages
{
    public class ACSBasePage : ContentPage
    {
        public static ImageSource sharedImageSource = null;

        public ACSBasePage()
        {

        }

        protected async Task LoadPage(string pageName)
        {
            await Shell.Current.GoToAsync(pageName);
        }


        //protected override bool OnBackButtonPressed()
        //{
        //    if (this is Dashboard) 
        //    {
        //        UIHelper.ConfirmAction("Logout Confirmation", "Do you want to log out?").ContinueWith(t =>
        //        {
        //            Device.BeginInvokeOnMainThread(async () =>
        //            {
        //                try
        //                {
        //                    if (t.Result) // User clicked "Yes"
        //                    {
        //                        await LogoutAsync();
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Debug.WriteLine($"Error during logout: {ex.Message}");
        //                }
        //            });
        //        });

        //        return true; // Prevent default navigation
        //    }

        //    // Ensure navigation happens properly
        //    Device.BeginInvokeOnMainThread(async () =>
        //    {
        //        try
        //        {
        //            ShowWaitCursor();
        //            await Task.Delay(150);
        //            await Navigation.PopAsync();
        //            //await Shell.Current.GoToAsync(".."); // Await navigation
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"Navigation error: {ex.Message}");
        //        }
        //        finally
        //        {
        //            HideWaitCursor();
        //        }
        //    });

        //    return true; // Prevent base method execution
        //}

        private async Task LogoutAsync()
        {
            try
            {
                var login = new LoginPage();
                var root = Navigation.NavigationStack[0];
                Navigation.InsertPageBefore(login, root);
                NavigationPage.SetHasNavigationBar(login, false);
                await Navigation.PopToRootAsync(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error during logout: " + ex.Message);
            }
        }

        private bool pageAlreadyLoaded = false;
        protected async override void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                if (pageAlreadyLoaded)
                    OnPageReappearing();
                else
                    OnPageAppearing();
            }
            catch (Exception ex)
            {
                await UIHelper.HandleException(ex);
            }
            finally
            {
                pageAlreadyLoaded = true;
            }
        }


        protected async override void OnDisappearing()
        {
            base.OnDisappearing();
            OnPageDisappearing();
        }



        protected virtual void OnPageAppearing()
        {

        }

        protected virtual void OnPageReappearing()
        {

        }


        protected virtual void OnPageDisappearing()
        {

        }

        //protected void ShowWaitCursor()
        //{
        //    PageViewModel.Current.IsBusy = true;
        //}

        //protected void HideWaitCursor()
        //{
        //    PageViewModel.Current.IsBusy = false;
        //}


        internal void SetFocusToControl(VisualElement ctrl)
        {
            Task.Run(async () =>
            {
                await Task.Delay(250);
                ctrl.Focus();
            });
        }


       
    }

}









