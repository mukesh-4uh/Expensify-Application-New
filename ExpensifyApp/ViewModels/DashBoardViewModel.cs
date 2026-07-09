//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using ExpensifyApp.DataBase;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Maui.Graphics;
//using System;
//using System.Collections.ObjectModel;
//using System.Linq;

//namespace ExpensifyApp.ViewModels
//{
//    public partial class DashBoardViewModel : ObservableObject
//    {
//        private readonly ExpenseContext _db;

//        // CHART DATA
//        public ObservableCollection<CategoryExpenseModel> ChartItems { get; }
//            = new ObservableCollection<CategoryExpenseModel>();

//        // GRID DATA
//        public ObservableCollection<ExpenseDisplay> Expenses { get; set; }
//            = new ObservableCollection<ExpenseDisplay>();


//        // DATE PICKER BOUND PROPERTY
//        [ObservableProperty]
//        private DateTime selectedDate = DateTime.Today;


//        [ObservableProperty]
//        private decimal totalAmount;


//        partial void OnSelectedDateChanged(DateTime value)
//        {
//            LoadAllData();
//        }

//        private const double MaxBarWidth = 260.0;

//        // COLOR PALETTE
//        private readonly List<Color> ColorPalette = new()
//        {
//            Color.FromArgb("#4CAF50"),
//            Color.FromArgb("#2196F3"),
//            Color.FromArgb("#FF9800"),
//            Color.FromArgb("#9C27B0"),
//            Color.FromArgb("#F44336"),
//            Color.FromArgb("#009688"),
//            Color.FromArgb("#3F51B5"),
//            Color.FromArgb("#795548"),
//            Color.FromArgb("#607D8B"),
//        };

//        private readonly Random _rand = new Random();

//        private Color GetRandomColor()
//        {
//            return ColorPalette[_rand.Next(ColorPalette.Count)];
//        }

//        public DashBoardViewModel(ExpenseContext db)
//        {
//            _db = db;
//            LoadAllData(); // Load for current date
//        }

//        // LOAD BOTH GRID + CHART
//        private async Task LoadAllData()
//        {
//            LoadChart();
//            //LoadGrid();
//        }

//        // LOAD BAR CHART (DATE BASED)


       
//        private async Task LoadChart()
//        {
//            ChartItems.Clear();

//            var data = _db.ExpenseTable
//                .Where(x => x.Date.Date == SelectedDate.Date)
//                .GroupBy(x => x.Category)
//                .Select(g => new CategoryExpenseModel
//                {
//                    Category = g.Key,
//                    Amount = g.Sum(x => x.Expenses)
//                })
//                .OrderByDescending(x => x.Amount)
//                .ToList();

//            if (data.Count == 0)
//            {
//                ChartItems.Add(new CategoryExpenseModel
//                {
//                    Category = "No Data",
//                    Amount = 0,
//                    BarWidth = 4,
//                    BarColor = Color.FromArgb("#BDBDBD")
//                });
//                return;
//            }

//            var maxAmount = data.Max(x => x.Amount);
//            if (maxAmount <= 0) maxAmount = 1;

//            foreach (var item in data)
//            {
//                double width = (item.Amount / maxAmount) * MaxBarWidth;

//                if (width < 4)
//                    width = 4;

//                ChartItems.Add(new CategoryExpenseModel
//                {
//                    Category = item.Category,
//                    Amount = item.Amount,
//                    BarWidth = width,
//                    BarColor = GetRandomColor()
//                });
//            }
//        }


//        //private async Task LoadGrid()
//        //{
//        //    Expenses.Clear();

//        //    var list = _db.ExpenseTable
//        //        .Where(e => e.Date.Date == SelectedDate.Date)
//        //        .ToList();

//        //    int sno = 1;
//        //    decimal sum = 0;

//        //    foreach (var item in list)
//        //    {
//        //        sum += item.Expenses;

//        //        Expenses.Add(new ExpenseDisplay
//        //        {
//        //            Index = sno++,
//        //            SNo = item.SNo,
//        //            Category = item.Category,
//        //            SubCategory = item.SubCategory,
//        //            Expenses = item.Expenses
//        //        });
//        //    }

//        //    TotalAmount = sum;
//        //}




//        [RelayCommand]
//        public async Task DeleteAsync(ExpenseDisplay item)
//        {
//            if (item == null)
//                return;

//            bool confirm = await App.Current.MainPage
//                .DisplayAlert("Confirm Delete",
//                              $"Delete {item.Category} ({item.SubCategory})?",
//                              "Yes", "No");

//            if (!confirm)
//                return;

//            var dbItem = await _db.ExpenseTable
//    .FirstOrDefaultAsync(x => x.SNo == item.SNo);

//            if (dbItem != null)
//            {
//                _db.ExpenseTable.Remove(dbItem);
//                await _db.SaveChangesAsync();
//            }

//            Expenses.Remove(item);
//            await LoadAllData();
//        }


//    }
//    public class CategoryExpenseModel
//    {
//        public string Category { get; set; }
//        public double Amount { get; set; }
//        public double BarWidth { get; set; }
//        public Color BarColor { get; set; }
//    }

//    public class ExpenseDisplay
//    {
//        public int Index { get; set; }
//        public int SNo { get; set; }
//        public string Category { get; set; }
//        public string SubCategory { get; set; }
//        public decimal Expenses { get; set; }
//        public DateTime Date { get; set; }
//    }


//    public class ChartItemModel
//    {
//        public string Category { get; set; }
//        public decimal Amount { get; set; }
//        public Color BarColor { get; set; }
//        public double BarWidth { get; set; }
//    }
//}
