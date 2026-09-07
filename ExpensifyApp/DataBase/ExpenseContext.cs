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


        public DbSet<ExpenseTable> ExpenseTable { get; set; }
        public DbSet<BudgetTable> BudgetTable { get; set; }
        public DbSet<RecurringExpenseTable> RecurringExpenseTable { get; set; }
        public DbSet<UserFinancialProfile> UserFinancialProfile { get; set; }
        public DbSet<MonthlyCommitment> MonthlyCommitment { get; set; }
        public DbSet<LendingTransaction> LendingTransaction { get; set; }
        public DbSet<SavingsTransaction> SavingsTransaction { get; set; }
        public DbSet<SavingsGoal> SavingsGoal { get; set; }
        public DbSet<BorrowedTransaction> BorrowedTransaction { get; set; }



        public ExpenseContext()
        {            
            DbPath = GetDatabasePath();
        }

        public static string GetDatabasePath()
        {
            try
            {
                if (!System.IO.Directory.Exists(_DBPath))
                {
                    System.IO.Directory.CreateDirectory(_DBPath);
                }
                return System.IO.Path.Combine(_DBPath, dbFileName);
            }
            catch
            {
                // Fallback to internal app data directory if external storage permission fails
                string fallbackDir = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
                if (!System.IO.Directory.Exists(fallbackDir))
                {
                    System.IO.Directory.CreateDirectory(fallbackDir);
                }
                return System.IO.Path.Combine(fallbackDir, dbFileName);
            }
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

                // Create BudgetTable if it doesn't exist (since EnsureCreated doesn't run if the DB already exists)
                await base.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS BudgetTable (Id INTEGER PRIMARY KEY AUTOINCREMENT, Amount INTEGER NOT NULL);");

                // Create RecurringExpenseTable if it doesn't exist
                await base.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS RecurringExpenseTable (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Category TEXT NOT NULL DEFAULT '',
                        SubCategory TEXT NOT NULL DEFAULT '',
                        PayMode TEXT NOT NULL DEFAULT '',
                        Amount INTEGER NOT NULL DEFAULT 0,
                        DayOfMonth INTEGER NOT NULL DEFAULT 1,
                        IsActive INTEGER NOT NULL DEFAULT 1
                    );");

                // Create UserFinancialProfile if it doesn't exist
                await base.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS UserFinancialProfile (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DashboardMode TEXT NOT NULL DEFAULT 'Monthly budget',
                        ProfileType TEXT NOT NULL DEFAULT '',
                        MonthlyIncome INTEGER NOT NULL DEFAULT 0,
                        CommitmentPercentage INTEGER NOT NULL DEFAULT 50,
                        SavingsPercentage INTEGER NOT NULL DEFAULT 30,
                        ExpensePercentage INTEGER NOT NULL DEFAULT 20,
                        SavingsMode TEXT NOT NULL DEFAULT '50/30/20',
                        CreatedDate TEXT NOT NULL
                    );");

                // Create MonthlyCommitment if it doesn't exist
                await base.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS MonthlyCommitment (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL DEFAULT '',
                        Amount INTEGER NOT NULL DEFAULT 0,
                        DueDay INTEGER NOT NULL DEFAULT 1,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        AutoAdd INTEGER NOT NULL DEFAULT 1,
                        Category TEXT NOT NULL DEFAULT ''
                    );");

                // Create LendingTransaction if it doesn't exist
                await base.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS LendingTransaction (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PersonName TEXT NOT NULL DEFAULT '',
                        MobileNumber TEXT NOT NULL DEFAULT '',
                        Relationship TEXT NOT NULL DEFAULT '',
                        Amount INTEGER NOT NULL DEFAULT 0,
                        Purpose TEXT NOT NULL DEFAULT '',
                        DateGiven TEXT NOT NULL,
                        DueDate TEXT NOT NULL,
                        DateReturned TEXT,
                        Status TEXT NOT NULL DEFAULT 'Pending',
                        ReminderEnabled INTEGER NOT NULL DEFAULT 1,
                        ReminderDays INTEGER NOT NULL DEFAULT 3,
                        Notes TEXT NOT NULL DEFAULT ''
                    );");

                try
                {
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE LendingTransaction ADD COLUMN MobileNumber TEXT NOT NULL DEFAULT '';");
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE LendingTransaction ADD COLUMN Relationship TEXT NOT NULL DEFAULT '';");
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE LendingTransaction ADD COLUMN Purpose TEXT NOT NULL DEFAULT '';");
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE LendingTransaction ADD COLUMN DateReturned TEXT;");
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE LendingTransaction ADD COLUMN ReminderDays INTEGER NOT NULL DEFAULT 3;");
                }
                catch
                {
                    // Columns might already exist, ignore error
                }

                // Create SavingsTransaction if it doesn't exist
                await base.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS SavingsTransaction (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Amount INTEGER NOT NULL DEFAULT 0,
                        Type TEXT NOT NULL DEFAULT 'Add',
                        Date TEXT NOT NULL,
                        Notes TEXT NOT NULL DEFAULT '',
                        GoalId INTEGER,
                        GoalName TEXT NOT NULL DEFAULT ''
                    );");

                // Create SavingsGoal if it doesn't exist
                await base.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS SavingsGoal (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        TargetAmount INTEGER NOT NULL DEFAULT 0,
                        CurrentAmount INTEGER NOT NULL DEFAULT 0,
                        AllocationPercentage INTEGER NOT NULL DEFAULT 0,
                        Deadline TEXT NOT NULL,
                        CategoryIcon TEXT NOT NULL DEFAULT '💰',
                        IsCompleted INTEGER NOT NULL DEFAULT 0
                    );");

                // Create BorrowedTransaction if it doesn't exist
                await base.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS BorrowedTransaction (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        PersonName TEXT NOT NULL DEFAULT '',
                        MobileNumber TEXT NOT NULL DEFAULT '',
                        Relationship TEXT NOT NULL DEFAULT '',
                        Amount INTEGER NOT NULL DEFAULT 0,
                        Purpose TEXT NOT NULL DEFAULT '',
                        DateBorrowed TEXT NOT NULL,
                        DueDate TEXT NOT NULL,
                        DateRepaid TEXT,
                        Status TEXT NOT NULL DEFAULT 'Pending',
                        Notes TEXT NOT NULL DEFAULT ''
                    );");

                // Perform Schema migrations (try-catch for already migrated databases)
                try
                {
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE SavingsTransaction ADD COLUMN GoalId INTEGER;");
                } catch { }

                try
                {
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE SavingsTransaction ADD COLUMN GoalName TEXT NOT NULL DEFAULT '';");
                } catch { }

                try
                {
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE UserFinancialProfile ADD COLUMN AutoSaveSalary INTEGER NOT NULL DEFAULT 0;");
                } catch { }

                try
                {
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE UserFinancialProfile ADD COLUMN AutoSaveFixedAmount INTEGER NOT NULL DEFAULT 0;");
                } catch { }

                try
                {
                    await base.Database.ExecuteSqlRawAsync("ALTER TABLE UserFinancialProfile ADD COLUMN AutoSaveRoundUp INTEGER NOT NULL DEFAULT 0;");
                } catch { }
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
