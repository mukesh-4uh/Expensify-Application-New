using System;
using System.Threading.Tasks;

namespace ExpensifyApp.Services;

public static class GoogleAccountPickerService
{
    /// <summary>
    /// Opens the native Android Google Account Chooser showing all Gmail accounts on the device.
    /// Returns the selected Gmail email address, or null if cancelled.
    /// </summary>
    public static async Task<string?> PickGoogleAccountAsync()
    {
#if ANDROID
        return await MainActivity.StartGoogleAccountPickerAsync();
#else
        await Task.CompletedTask;
        return null;
#endif
    }
}
