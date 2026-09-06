using System.Linq;
using System.Threading.Tasks;
using ExpensifyApp.Pages;
using Microsoft.Maui.Controls;

namespace ExpensifyApp.Helpers;

public static class AppNavigation
{
    public static async Task NavigateToTabAsync(INavigation navigation, Page targetPage)
    {     
        var current = navigation.NavigationStack.LastOrDefault();
        if (current == null) return;
     
        if (current.GetType() == targetPage.GetType()) return;
       
        await navigation.PushAsync(targetPage, false);
     
        if (current is not LoginPage)
        {
            navigation.RemovePage(current);
        }
    }
}
