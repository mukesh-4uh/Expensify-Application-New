using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExpensifyApp.Helpers;

namespace ExpensifyApp.DataBase
{
    public partial class ExpenseContext : DbContext
    {
        public string DbPath { get; }
        public static string ExportFilePath { get; private set; }
        public static string ImportFilePath { get; private set; }

        public static string _DBPath = @"/storage/emulated/0/expensifydir";
        private static string dbFileName = "expensify.db";


        public DbSet<ExpenseTable> ExpenseTable { get; set; } //;
        public ExpenseContext()
        {            
            DbPath = System.IO.Path.Join(_DBPath, dbFileName);
        }
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite($"Data Source={DbPath}");


        public async Task DoInitWork()
        {
            try
            {
                var result = await CheckAndRequestFolderPermission();

                if (result != PermissionStatus.Granted)
                {
                    await UIHelper.ShowErrorMessage(
                        "Storage permission denied. Please enable 'Manage All Files' permission for the app to work properly.");

                    await AppSettingsService.OpenManageAllFilesSettingsAsync();
                    return;
                }

               
                EnsureDirectoriesExist();

               
                base.Database.EnsureCreated();
            }
            catch (Exception ex)
            {     
                    await UIHelper.HandleException(ex);
                    await AppSettingsService.OpenManageAllFilesSettingsAsync();

            }
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(_DBPath))
                    Directory.CreateDirectory(_DBPath);
     
            }
            catch (Exception ex)
            {

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await UIHelper.ShowErrorMessage("Unable to create required folders. Please check your storage permissions.");
                });

                throw;
            }
        }

        public async Task<PermissionStatus> CheckAndRequestFolderPermission()
        {
            return await PermissionHelper.CheckAndRequestFileWritePermission();
        }
        public async Task<bool> HasAnyExpenseTodayAsync()
        {
            try
            {
                var today = DateTime.Today;
                return await ExpenseTable
                    .AnyAsync(e => e.Date.Date == today);
            }
            catch
            {
                return false;
            }
        }


        public static class AppSettingsService
        {
            public static Task OpenManageAllFilesSettingsAsync()
            {
#if ANDROID
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        // Optional: Small delay to ensure dialog UI finishes
                        await Task.Delay(500);

                        var uri = Android.Net.Uri.Parse("package:" + AppInfo.PackageName);
                        var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                        intent.SetData(uri);
                        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                        Android.App.Application.Context.StartActivity(intent);
                    }
                    catch (Exception)
                    {
                        await UIHelper.ShowErrorMessage("Could not open settings. Please enable 'Manage All Files' manually.");
                    }
                });
#endif
                return Task.CompletedTask;
            }
        }
    }
}
