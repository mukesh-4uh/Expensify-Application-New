using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages;

public class LendingDisplayItem
{
    public int Id { get; set; }
    public string PersonName { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Relationship { get; set; } = "";
    public int Amount { get; set; }
    public string AmountDisplay { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Notes { get; set; } = "";
    public string DateGivenStr { get; set; } = "";
    public string DueDateStr { get; set; } = "";
    public string DateReturnedStr { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string Status { get; set; } = ""; // Raw Status (Pending, Overdue, Recovered, etc.)

    public Color StatusBg { get; set; } = Colors.LightGray;
    public Color StatusColor { get; set; } = Colors.DarkGray;
    public Color DueColor { get; set; } = Colors.Black;
    public string OverdueDaysStr { get; set; } = "";

    // Visibility Flags
    public bool IsActiveCard => Status == "Pending" || Status == "Due soon" || Status == "Due Today";
    public bool IsRecoveredCard => Status == "Recovered";
    public bool IsOverdueCard => Status == "Overdue";
    public bool IsNotActiveCard => !IsActiveCard;
    public bool HasPurpose => !string.IsNullOrWhiteSpace(Purpose);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public DateTime DateGivenRaw { get; set; }
    public DateTime DueDateRaw { get; set; }
    public LendingTransaction RawRecord { get; set; }
}

public partial class LendingRecoveryPage : ContentPage
{
    private readonly ExpenseContext _db;
    public ObservableCollection<LendingDisplayItem> LendingItems { get; set; } = new();
    private string _currentFilter = "All";

    public LendingRecoveryPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        lendingCollectionView.ItemsSource = LendingItems;

        Appearing += async (s, e) => await LoadLendingData();
    }

    private async Task LoadLendingData()
    {
        try
        {
            var raw = await _db.LendingTransaction.ToListAsync();
            var today = DateTime.Today;

            // 1. Sync status dynamically based on current date
            foreach (var item in raw)
            {
                if (item.Status != "Recovered")
                {
                    if (item.DueDate.Date < today.Date)
                    {
                        item.Status = "Overdue";
                    }
                    else if (item.DueDate.Date == today.Date)
                    {
                        item.Status = "Due Today";
                    }
                    else if (item.DueDate.Date <= today.AddDays(7).Date)
                    {
                        item.Status = "Due soon";
                    }
                    else
                    {
                        item.Status = "Pending";
                    }
                }
            }
            _db.LendingTransaction.UpdateRange(raw);
            await _db.SaveChangesAsync();

            // 2. Calculate Stats
            int outstanding = raw.Where(l => l.Status != "Recovered").Sum(l => l.Amount);
            int received = raw.Where(l => l.Status == "Recovered").Sum(l => l.Amount);

            outstandingLabel.Text = $"₹{outstanding:N0}";
            receivedLabel.Text = $"₹{received:N0}";

            // 3. Render Filtered List
            FilterAndRenderList(raw);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void FilterAndRenderList(List<LendingTransaction> raw)
    {
        var filtered = _currentFilter switch
        {
            "Pending" => raw.Where(l => l.Status != "Recovered").ToList(),
            "Recovered" => raw.Where(l => l.Status == "Recovered").ToList(),
            _ => raw
        };

        // Sort: Pending/Overdue first, then by DueDate
        var sorted = filtered
            .OrderBy(l => l.Status == "Recovered" ? 1 : 0)
            .ThenBy(l => l.DueDate)
            .ToList();

        LendingItems.Clear();
        foreach (var item in sorted)
        {
            int overdueDays = (DateTime.Today - item.DueDate).Days;
            string overdueStr = overdueDays > 0 ? $"{overdueDays} Days" : "0 Days";

            LendingItems.Add(new LendingDisplayItem
            {
                Id = item.Id,
                PersonName = item.PersonName,
                MobileNumber = item.MobileNumber,
                Relationship = string.IsNullOrWhiteSpace(item.Relationship) ? "Personal" : item.Relationship,
                Amount = item.Amount,
                AmountDisplay = $"₹{item.Amount:N0}",
                Purpose = item.Purpose,
                Notes = item.Notes,
                DateGivenStr = item.DateGiven.ToString("dd MMM yyyy"),
                DueDateStr = item.DueDate.ToString("dd MMM yyyy"),
                DateReturnedStr = item.DateReturned?.ToString("dd MMM yyyy") ?? "",
                Status = item.Status,
                StatusText = item.Status switch
                {
                    "Recovered" => "Recovered ✅",
                    "Overdue" => "Overdue 🔴",
                    "Due Today" => "Due Today 🔵",
                    "Due soon" => "Due Soon 🔵",
                    _ => "Pending 🟠"
                },
                StatusBg = item.Status switch
                {
                    "Recovered" => Color.FromArgb("#E8F5E9"),
                    "Overdue" => Color.FromArgb("#FFEBEE"),
                    "Due Today" => Color.FromArgb("#E3F2FD"),
                    "Due soon" => Color.FromArgb("#E1F5FE"),
                    _ => Color.FromArgb("#FFF3E0")
                },
                StatusColor = item.Status switch
                {
                    "Recovered" => Color.FromArgb("#2E7D32"),
                    "Overdue" => Color.FromArgb("#C62828"),
                    "Due Today" => Color.FromArgb("#1565C0"),
                    "Due soon" => Color.FromArgb("#0288D1"),
                    _ => Color.FromArgb("#EF6C00")
                },
                DueColor = item.Status switch
                {
                    "Overdue" => Color.FromArgb("#C62828"),
                    "Due Today" => Color.FromArgb("#1565C0"),
                    _ => Color.FromArgb("#1C2340")
                },
                OverdueDaysStr = overdueStr,
                DateGivenRaw = item.DateGiven,
                DueDateRaw = item.DueDate,
                RawRecord = item
            });
        }

        // Update Header Label
        listTitleLabel.Text = _currentFilter == "All" 
            ? $"All Loans ({sorted.Count})" 
            : $"{_currentFilter} Loans ({sorted.Count})";
    }

    private async void OnFilterTapped(object sender, EventArgs e)
    {
        var border = sender as Border;
        var commandParam = border?.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault()?.CommandParameter as string;
        if (string.IsNullOrEmpty(commandParam)) return;

        _currentFilter = commandParam;

        // Reset all chip visuals
        chipAll.BackgroundColor = Colors.White;
        lblAll.TextColor = Color.FromArgb("#8A94A6");
        chipPending.BackgroundColor = Colors.White;
        lblPending.TextColor = Color.FromArgb("#8A94A6");
        chipRecovered.BackgroundColor = Colors.White;
        lblRecovered.TextColor = Color.FromArgb("#8A94A6");

        // Set active chip highlight
        if (_currentFilter == "All")
        {
            chipAll.BackgroundColor = Color.FromArgb("#0D8C87");
            lblAll.TextColor = Colors.White;
        }
        else if (_currentFilter == "Pending")
        {
            chipPending.BackgroundColor = Color.FromArgb("#EF6C00");
            lblPending.TextColor = Colors.White;
        }
        else if (_currentFilter == "Recovered")
        {
            chipRecovered.BackgroundColor = Color.FromArgb("#2E7D32");
            lblRecovered.TextColor = Colors.White;
        }

        // Reload data with filter applied
        await LoadLendingData();
    }

    private async void OnAddLentTapped(object sender, EventArgs e)
    {
        try
        {
            var popup = new AddLentPopup();
            var result = await this.ShowPopupAsync(popup);

            if (result is AddLentResult lentResult)
            {
                var tx = new LendingTransaction
                {
                    PersonName = lentResult.PersonName,
                    MobileNumber = lentResult.MobileNumber,
                    Relationship = lentResult.Relationship,
                    Amount = lentResult.Amount,
                    Purpose = lentResult.Purpose,
                    DateGiven = lentResult.DateGiven,
                    DueDate = lentResult.DueDate,
                    Status = lentResult.Status,
                    ReminderEnabled = true,
                    ReminderDays = lentResult.ReminderDays,
                    Notes = lentResult.Notes,
                    DateReturned = lentResult.Status == "Recovered" ? DateTime.Today : null
                };

                _db.LendingTransaction.Add(tx);
                await _db.SaveChangesAsync();

                await UIHelper.ShowToastMessage("Loan logged successfully!");
                await LoadLendingData();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task EditLentRecordAsync(LendingDisplayItem item)
    {
        try
        {
            var popup = new AddLentPopup(item.RawRecord);
            var result = await this.ShowPopupAsync(popup);

            if (result is AddLentResult lentResult)
            {
                var tx = await _db.LendingTransaction.FirstOrDefaultAsync(l => l.Id == item.Id);
                if (tx != null)
                {
                    tx.PersonName = lentResult.PersonName;
                    tx.MobileNumber = lentResult.MobileNumber;
                    tx.Relationship = lentResult.Relationship;
                    tx.Amount = lentResult.Amount;
                    tx.Purpose = lentResult.Purpose;
                    tx.DateGiven = lentResult.DateGiven;
                    tx.DueDate = lentResult.DueDate;
                    tx.Status = lentResult.Status;
                    tx.ReminderDays = lentResult.ReminderDays;
                    tx.Notes = lentResult.Notes;
                    tx.DateReturned = lentResult.Status == "Recovered" ? DateTime.Today : null;

                    _db.LendingTransaction.Update(tx);
                    await _db.SaveChangesAsync();

                    await UIHelper.ShowToastMessage("Loan updated successfully!");
                    await LoadLendingData();
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task RecoverLentRecordAsync(LendingDisplayItem item)
    {
        var confirmPopup = new ConfirmationPopup(
            "Mark as Recovered 🤝",
            $"Are you sure you have recovered ₹{item.Amount:N0} from {item.PersonName}?",
            "This will clear the outstanding balance.",
            "Yes, Recovered",
            "Cancel",
            isDestructive: false);

        var confirmResult = await this.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirm || !confirm) return;

        try
        {
            var tx = await _db.LendingTransaction.FirstOrDefaultAsync(l => l.Id == item.Id);
            if (tx != null)
            {
                tx.Status = "Recovered";
                tx.DateReturned = DateTime.Today;
                _db.LendingTransaction.Update(tx);
                await _db.SaveChangesAsync();

                await UIHelper.ShowToastMessage("Marked as recovered!");
                await LoadLendingData();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task DeleteLentRecordAsync(LendingDisplayItem item)
    {
        var confirmPopup = new ConfirmationPopup(
            "Delete Loan Record ✕",
            $"Are you sure you want to delete this loan record for {item.PersonName}?",
            "This will remove the transaction history completely.",
            "Delete",
            "Cancel",
            isDestructive: true);

        var confirmResult = await this.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirm || !confirm) return;

        try
        {
            var tx = await _db.LendingTransaction.FirstOrDefaultAsync(l => l.Id == item.Id);
            if (tx != null)
            {
                _db.LendingTransaction.Remove(tx);
                await _db.SaveChangesAsync();

                await UIHelper.ShowToastMessage("Loan record deleted.");
                await LoadLendingData();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnEditButtonClicked(object sender, EventArgs e)
    {
        var btn = sender as Button;
        var item = btn?.CommandParameter as LendingDisplayItem;
        if (item != null) await EditLentRecordAsync(item);
    }

    private async void OnMarkPaidButtonClicked(object sender, EventArgs e)
    {
        var btn = sender as Button;
        var item = btn?.CommandParameter as LendingDisplayItem;
        if (item != null) await RecoverLentRecordAsync(item);
    }

    private async void OnCallButtonClicked(object sender, EventArgs e)
    {
        var btn = sender as Button;
        var item = btn?.CommandParameter as LendingDisplayItem;
        if (item != null && !string.IsNullOrWhiteSpace(item.MobileNumber))
        {
            try
            {
                PhoneDialer.Default.Open(item.MobileNumber);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Dialer Error", "Unable to open phone dialer: " + ex.Message, "OK");
            }
        }
    }

    private async void OnViewDetailsButtonClicked(object sender, EventArgs e)
    {
        var btn = sender as Button;
        var item = btn?.CommandParameter as LendingDisplayItem;
        if (item != null)
        {
            string msg = $"Borrower: {item.PersonName}\n" +
                         $"Relationship: {item.Relationship}\n" +
                         $"Amount: {item.AmountDisplay}\n" +
                         $"Purpose: {item.Purpose}\n" +
                         $"Given on: {item.DateGivenStr}\n" +
                         $"Status: {item.StatusText}\n";
            if (item.IsRecoveredCard)
                msg += $"Returned on: {item.DateReturnedStr}\n";
            else
                msg += $"Due on: {item.DueDateStr}\n";

            if (!string.IsNullOrWhiteSpace(item.Notes))
                msg += $"\nNotes: {item.Notes}";

            await DisplayAlert("Loan Details", msg, "OK");
        }
    }

    // SWIPE INVOKED EVENT HANDLERS
    private async void OnSwipeEditInvoked(object sender, EventArgs e)
    {
        var sw = sender as SwipeItem;
        var item = sw?.CommandParameter as LendingDisplayItem;
        if (item != null) await EditLentRecordAsync(item);
    }

    private async void OnSwipeRecoverInvoked(object sender, EventArgs e)
    {
        var sw = sender as SwipeItem;
        var item = sw?.CommandParameter as LendingDisplayItem;
        if (item != null) await RecoverLentRecordAsync(item);
    }

    private async void OnSwipeDeleteInvoked(object sender, EventArgs e)
    {
        var sw = sender as SwipeItem;
        var item = sw?.CommandParameter as LendingDisplayItem;
        if (item != null) await DeleteLentRecordAsync(item);
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnMarkPaidClicked(object sender, EventArgs e)
    {
        var btn = sender as Button;
        var item = btn?.CommandParameter as LendingDisplayItem;
        if (item != null) await RecoverLentRecordAsync(item);
    }
}
