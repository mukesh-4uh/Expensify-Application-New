using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Maui.Storage;
using ExpensifyApp.DataBase;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.Drawing;


namespace ExpensifyApp.Pages
{
    public partial class MonthlyReportsPage : ContentPage
    {
        private readonly ExpenseContext _dbContext = new(); // Replace with DI if applicable

        private ObservableCollection<ExpenseTable> ReportData = new();

        public MonthlyReportsPage()
        {
            InitializeComponent();
            InitMonthYearPickers();
            reportCollections.ItemsSource = ReportData;
        }

        private void InitMonthYearPickers()
        {
            var months = DateTimeFormatInfo.CurrentInfo.MonthNames.Where(m => !string.IsNullOrEmpty(m)).ToList();
            monthPicker.ItemsSource = months;

            var years = Enumerable.Range(DateTime.Now.Year - 10, 11).ToList(); // 10 years back
            yearPicker.ItemsSource = years;
        }

        private async void OnLoadReportClicked(object sender, EventArgs e)
        {
            if (monthPicker.SelectedIndex == -1 || yearPicker.SelectedIndex == -1)
            {
                await DisplayAlert("Selection Required", "Please select both month and year.", "OK");
                return;
            }

            int month = monthPicker.SelectedIndex + 1;
            int year = (int)yearPicker.SelectedItem;

            DateTime startDate = new(year, month, 1);
            DateTime endDate = startDate.AddMonths(1).AddDays(-1);

            try
            {
                ReportData.Clear();
                monthlyTotalLabel.IsVisible = false;

                var expenseList = await Task.Run(() => _dbContext.ExpenseTable
                    .Where(x => x.Date >= startDate && x.Date <= endDate)
                    .ToList());

                if (expenseList.Any())
                {
                    foreach (var item in expenseList)
                        ReportData.Add(item);

                    decimal total = expenseList.Sum(x => x.Expenses);
                    monthlyTotalLabel.Text = $"Total Expenses: ₹{total:F2}";
                    monthlyTotalLabel.IsVisible = true;
                }
                else
                {
                    await DisplayAlert("No Data", "No expenses found for the selected month.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load report: {ex.Message}", "OK");
            }
        }

        private async void OnDownloadReportClicked(object sender, EventArgs e)
        {
#if ANDROID
    try
    {
        if (!ReportData.Any())
        {
            await DisplayAlert("No Data", "Please load a report before downloading.", "OK");
            return;
        }

        var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Permission Denied", "Storage permission is required.", "OK");
            return;
        }

        // Create PDF
        using PdfDocument document = new();
        PdfPage page = document.Pages.Add();
        PdfGraphics graphics = page.Graphics;

        PdfFont headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold);
        graphics.DrawString("Monthly Expense Report", headerFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(0, 0));

        PdfGrid pdfGrid = new();
        pdfGrid.DataSource = ReportData.Select(e => new
        {
            Date = e.Date.ToString("dd-MM-yyyy"),
            e.Category,
            Amount = $"₹{e.Expenses:F2}"
        }).ToList();

        pdfGrid.Draw(page, new Syncfusion.Drawing.PointF(0, 30));

        decimal total = ReportData.Sum(x => x.Expenses);
        PdfFont totalFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
        graphics.DrawString($"Total: ₹{total:F2}", totalFont, PdfBrushes.DarkBlue,
            new Syncfusion.Drawing.PointF(0, page.GetClientSize().Height - 30));

        // Save to Downloads
        string fileName = $"ExpenseReport_{DateTime.Now:yyyyMMddHHmmss}.pdf";
        string downloadsPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
        string filePath = Path.Combine(downloadsPath, fileName);

        using FileStream outputStream = new(filePath, FileMode.Create, FileAccess.Write);
        document.Save(outputStream);

        await DisplayAlert("Success", $"PDF saved to Downloads:\n{filePath}", "OK");
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", $"PDF generation failed: {ex.Message}", "OK");
    }
#else
            await DisplayAlert("Unsupported", "Downloads folder saving is only implemented on Android.", "OK");
#endif
        }
    }

    }
