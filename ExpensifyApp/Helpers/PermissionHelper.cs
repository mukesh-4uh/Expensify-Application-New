using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Maui.ApplicationModel.Permissions;


namespace ExpensifyApp.Helpers;
public static class PermissionHelper
{
    static PermissionHelper()
    {

    }

    public static async Task<PermissionStatus> CheckAndRequestBluetoothPermission()
    {
        try
        {
            PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();

            if (status == PermissionStatus.Granted)
                return status;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // Prompt the user to turn on in settings
                // On iOS once a permission has been denied it may not be requested again from the application
                return status;
            }

            if (Permissions.ShouldShowRationale<Permissions.Bluetooth>())
            {
                // Prompt the user with additional information as to why the permission is needed
            }

            status = await Permissions.RequestAsync<Permissions.Bluetooth>();

            return status;
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }

        return PermissionStatus.Unknown;
    }

    public static async Task<PermissionStatus> CheckAndRequestLocationPermission()
    {
        try
        {
            PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status == PermissionStatus.Granted)
                return status;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // Prompt the user to turn on in settings
                // On iOS once a permission has been denied it may not be requested again from the application
                return status;
            }

            if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
            {
                // Prompt the user with additional information as to why the permission is needed
            }

            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            return status;

        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }

        return PermissionStatus.Unknown;
    }

    public static async Task<PermissionStatus> CheckAndRequestFileWritePermission()
    {
        try
        {
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R) // Android 11+
        {
            if (Android.OS.Environment.IsExternalStorageManager)
            {
                return PermissionStatus.Granted;
            }
            else
            {
                return PermissionStatus.Denied; // Manage All Files not granted
            }
        }
        else
        {
            // Android 10 and below
            var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();

            if (status == PermissionStatus.Granted)
                return status;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                return status; // iOS cannot request again if denied
            }

            if (Permissions.ShouldShowRationale<Permissions.StorageWrite>())
            {
                // Optional: show rationale to the user
            }

            status = await Permissions.RequestAsync<Permissions.StorageWrite>();
            return status;
        }
#else
            // iOS / Windows / Mac
            return await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
#endif
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
            return PermissionStatus.Unknown;
        }
    }

    public static async Task<PermissionStatus> CheckAndRequestCameraPermission()
    {
        try
        {
            PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Camera>();

            if (status == PermissionStatus.Granted)
                return status;

            if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // Prompt the user to turn on in settings
                // On iOS once a permission has been denied it may not be requested again from the application
                return status;
            }

            if (Permissions.ShouldShowRationale<Permissions.Camera>())
            {
                // Prompt the user with additional information as to why the permission is needed
            }

            status = await Permissions.RequestAsync<Permissions.Camera>();

            return status;
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }

        return PermissionStatus.Unknown;
    }
    public static async Task<FileResult> PickImageAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Pick an Image",
                FileTypes = FilePickerFileType.Images // Restrict to image files
            });

            if (result != null)
            {
                // Process the selected file
                return result;
            }
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }

        return null;
    }

    public static async Task<FileResult> CaptureImageAsync()
    {
        try
        {
            var photo = await MediaPicker.CapturePhotoAsync();

            if (photo != null)
            {
                // Save the photo locally or upload
                var filePath = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);
                using var stream = await photo.OpenReadAsync();
                using var fileStream = File.OpenWrite(filePath);
                await stream.CopyToAsync(fileStream);

                return photo;
            }
        }
        catch (Exception ex)
        {
            await UIHelper.HandleException(ex);
        }

        return null;
    }

}