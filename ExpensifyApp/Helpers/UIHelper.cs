using CommunityToolkit.Maui.Alerts;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ExpensifyApp.Helpers;

public static class UIHelper
{
    public async static Task HandleException(Exception ex)
    {
        await ShowErrorMessage(ex.Message + Environment.NewLine + ex.StackTrace);
    }

    public async static Task ShowMessage(string message)
    {
        await Application.Current.MainPage.DisplayAlert("AMS", message, "Ok");
    }

    public async static Task ShowErrorMessage(string message, string title = "Error")
    {
        await Application.Current.MainPage.DisplayAlert(title, message, "Ok");
    }

    public async static Task<bool> ConfirmAction(string title, string message)
    {
        return await Application.Current.MainPage.DisplayAlert(title, message, "Yes", "No");
    }



    public async static Task<string> DisplayActionSheet(string message, params string[] buttons)
    {
        return await Application.Current.MainPage.DisplayActionSheet(message, "Cancel", null, buttons);
    }

    public async static Task<string> ShowMessage(string message, params string[] buttons)
    {
        return await Application.Current.MainPage.DisplayActionSheet(message, "Cancel", null, buttons);
    }


    public async static Task ShowToastMessage(string message)
    {
        Toast.Make(message, CommunityToolkit.Maui.Core.ToastDuration.Long, 16).Show();
    }

    public async static Task ShowDebugToastMessage(string message)
    {
        await ShowToastMessage(message);
    }

    //public async static Task HandleServiceError(BaseResponse se)
    //{
    //    await ShowErrorMessage(se.Message);
    //}



    //public async static Task HandleServiceError(ACS.Smartidfy.WebApp.Models.Mobile.BaseResponse se)
    //{
    //    await ShowErrorMessage(se.Message);
    //}
}