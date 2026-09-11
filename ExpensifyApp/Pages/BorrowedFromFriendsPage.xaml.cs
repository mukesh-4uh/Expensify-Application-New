using CommunityToolkit.Maui.Views;
using ExpensifyApp.DataBase;
using ExpensifyApp.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ExpensifyApp.Pages;

// ─────────────────────────────────────────────────────────────────────────────
// VIEW MODEL / DISPLAY ITEM
// ─────────────────────────────────────────────────────────────────────────────
public class BorrowedDisplayItem
{
    public int Id { get; set; }
    public string PersonName { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Relationship { get; set; } = "";
    public int Amount { get; set; }
    public string AmountDisplay { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Notes { get; set; } = "";
    public string DateBorrowedStr { get; set; } = "";
    public string DueDateStr { get; set; } = "";
    public string DateRepaidStr { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusText { get; set; } = "";

    // Colours
    public Color StatusBg { get; set; } = Colors.LightGray;
    public Color StatusColor { get; set; } = Colors.DarkGray;
    public Color DueColor { get; set; } = Color.FromArgb("#1C2340");
    public Color AvatarBg { get; set; } = Color.FromArgb("#EDE7FF");

    // Display helpers
    public string AvatarEmoji { get; set; } = "👤";
    public string RelationshipDisplay { get; set; } = "";
    public string PurposeDisplay { get; set; } = "";

    // Visibility
    public bool IsUnpaid => Status != "Paid";
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    // Raw
    public BorrowedTransaction RawRecord { get; set; } = null!;
}

// ─────────────────────────────────────────────────────────────────────────────
// PAGE
// ─────────────────────────────────────────────────────────────────────────────
public partial class BorrowedFromFriendsPage : ContentPage
{
    private readonly ExpenseContext _db;
    public ObservableCollection<BorrowedDisplayItem> BorrowedItems { get; set; } = new();
    private string _currentFilter = "All";

    public BorrowedFromFriendsPage()
    {
        InitializeComponent();
        _db = new ExpenseContext();
        borrowedCollectionView.ItemsSource = BorrowedItems;
        Appearing += async (s, e) => await LoadDataAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LOAD & REFRESH
    // ─────────────────────────────────────────────────────────────────────────
    private async Task LoadDataAsync()
    {
        try
        {
            // Ensure table exists
            await _db.Database.ExecuteSqlRawAsync(
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

            var raw = await _db.BorrowedTransaction.ToListAsync();
            var today = DateTime.Today;

            // Sync statuses dynamically
            foreach (var item in raw)
            {
                if (item.Status == "Paid") continue;

                if (item.DueDate.Date < today)
                    item.Status = "Overdue";
                else if (item.DueDate.Date == today)
                    item.Status = "Due Today";
                else if (item.DueDate.Date <= today.AddDays(7))
                    item.Status = "Due soon";
                else
                    item.Status = "Pending";
            }
            _db.BorrowedTransaction.UpdateRange(raw);
            await _db.SaveChangesAsync();

            // Summary stats
            int totalOwed = raw.Where(b => b.Status != "Paid").Sum(b => b.Amount);
            int totalPaid = raw.Where(b => b.Status == "Paid").Sum(b => b.Amount);

            totalOwedLabel.Text = $"₹{totalOwed:N0}";
            totalPaidLabel.Text = $"₹{totalPaid:N0}";

            RenderList(raw);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void RenderList(List<BorrowedTransaction> raw)
    {
        var filtered = _currentFilter switch
        {
            "Pending" => raw.Where(b => b.Status != "Paid").ToList(),
            "Paid"    => raw.Where(b => b.Status == "Paid").ToList(),
            _         => raw
        };

        var sorted = filtered
            .OrderBy(b => b.Status == "Paid" ? 1 : 0)
            .ThenBy(b => b.DueDate)
            .ToList();

        BorrowedItems.Clear();
        foreach (var b in sorted)
        {
            BorrowedItems.Add(MapToDisplay(b));
        }

        listTitleLabel.Text = _currentFilter switch
        {
            "Pending" => $"Unpaid Records ({sorted.Count})",
            "Paid"    => $"Paid Records ({sorted.Count})",
            _         => $"All Records ({sorted.Count})"
        };
    }

    private static BorrowedDisplayItem MapToDisplay(BorrowedTransaction b)
    {
        // Relationship → avatar emoji
        string emoji = b.Relationship?.ToLower() switch
        {
            "family"    => "👨‍👩‍👧",
            "friend"    => "🧑‍🤝‍🧑",
            "colleague" => "💼",
            "neighbour" => "🏘️",
            _           => "👤"
        };

        // AvatarBg based on status
        var avatarBg = b.Status switch
        {
            "Paid"     => Color.FromArgb("#E8F5E9"),
            "Overdue"  => Color.FromArgb("#FFEBEE"),
            "Due Today"=> Color.FromArgb("#E3F2FD"),
            _          => Color.FromArgb("#EDE7FF")
        };

        var statusBg = b.Status switch
        {
            "Paid"     => Color.FromArgb("#E8F5E9"),
            "Overdue"  => Color.FromArgb("#FFEBEE"),
            "Due Today"=> Color.FromArgb("#E3F2FD"),
            "Due soon" => Color.FromArgb("#E1F5FE"),
            _          => Color.FromArgb("#FFF3E0")
        };

        var statusColor = b.Status switch
        {
            "Paid"     => Color.FromArgb("#2E7D32"),
            "Overdue"  => Color.FromArgb("#C62828"),
            "Due Today"=> Color.FromArgb("#1565C0"),
            "Due soon" => Color.FromArgb("#0288D1"),
            _          => Color.FromArgb("#EF6C00")
        };

        var dueColor = b.Status switch
        {
            "Overdue"  => Color.FromArgb("#C62828"),
            "Due Today"=> Color.FromArgb("#1565C0"),
            _          => Color.FromArgb("#1C2340")
        };

        string statusText = b.Status switch
        {
            "Paid"     => "Paid ✅",
            "Overdue"  => "Overdue 🔴",
            "Due Today"=> "Due Today 🔵",
            "Due soon" => "Due Soon 🔵",
            _          => "Pending 🟠"
        };

        string rel = string.IsNullOrWhiteSpace(b.Relationship) ? "Personal" : b.Relationship;
        string purpose = string.IsNullOrWhiteSpace(b.Purpose) ? "General" : b.Purpose;

        return new BorrowedDisplayItem
        {
            Id               = b.Id,
            PersonName       = b.PersonName,
            MobileNumber     = b.MobileNumber,
            Relationship     = rel,
            Amount           = b.Amount,
            AmountDisplay    = $"₹{b.Amount:N0}",
            Purpose          = b.Purpose,
            Notes            = b.Notes,
            DateBorrowedStr  = b.DateBorrowed.ToString("dd MMM yyyy"),
            DueDateStr       = b.Status == "Paid"
                                   ? (b.DateRepaid?.ToString("Paid on dd MMM yyyy") ?? "—")
                                   : b.DueDate.ToString("dd MMM yyyy"),
            DateRepaidStr    = b.DateRepaid?.ToString("dd MMM yyyy") ?? "",
            Status           = b.Status,
            StatusText       = statusText,
            StatusBg         = statusBg,
            StatusColor      = statusColor,
            DueColor         = dueColor,
            AvatarBg         = avatarBg,
            AvatarEmoji      = emoji,
            RelationshipDisplay = $"{emoji}  {rel}",
            PurposeDisplay   = $"📌 {purpose}",
            RawRecord        = b
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FILTER
    // ─────────────────────────────────────────────────────────────────────────
    private async void OnFilterTapped(object sender, EventArgs e)
    {
        var border = sender as Border;
        var param  = border?.GestureRecognizers.OfType<TapGestureRecognizer>().FirstOrDefault()?.CommandParameter as string;
        if (string.IsNullOrEmpty(param)) return;

        _currentFilter = param;

        // Reset chips
        var inactive = Colors.White;
        var inactiveText = Color.FromArgb("#8A94A6");
        chipAll.BackgroundColor = inactive;     lblAll.TextColor = inactiveText;
        chipPending.BackgroundColor = inactive; lblPending.TextColor = inactiveText;
        chipPaid.BackgroundColor = inactive;    lblPaid.TextColor = inactiveText;

        // Activate
        Color activeBg = _currentFilter switch
        {
            "Pending" => Color.FromArgb("#EF6C00"),
            "Paid"    => Color.FromArgb("#2E7D32"),
            _         => Color.FromArgb("#0F9F99")
        };
        if (_currentFilter == "All")     { chipAll.BackgroundColor = activeBg;     lblAll.TextColor = Colors.White; }
        else if (_currentFilter == "Pending") { chipPending.BackgroundColor = activeBg; lblPending.TextColor = Colors.White; }
        else if (_currentFilter == "Paid")    { chipPaid.BackgroundColor = activeBg;    lblPaid.TextColor = Colors.White; }

        await LoadDataAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADD BORROWED
    // ─────────────────────────────────────────────────────────────────────────
    private async void OnAddBorrowedTapped(object sender, EventArgs e)
    {
        try
        {
            var popup = new AddBorrowedPopup();
            var result = await this.ShowPopupAsync(popup);

            if (result is AddBorrowedResult res)
            {
                var tx = new BorrowedTransaction
                {
                    PersonName    = res.PersonName,
                    MobileNumber  = res.MobileNumber,
                    Relationship  = res.Relationship,
                    Amount        = res.Amount,
                    Purpose       = res.Purpose,
                    DateBorrowed  = res.DateBorrowed,
                    DueDate       = res.DueDate,
                    Status        = "Pending",
                    Notes         = res.Notes
                };
                _db.BorrowedTransaction.Add(tx);
                await _db.SaveChangesAsync();
                await UIHelper.ShowToastMessage("Borrowed record added!");
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MARK PAID
    // ─────────────────────────────────────────────────────────────────────────
    private async void OnMarkPaidClicked(object sender, EventArgs e)
    {
        var btn  = sender as Button;
        var item = btn?.CommandParameter as BorrowedDisplayItem;
        if (item != null) await MarkAsPaidAsync(item);
    }

    private async void OnSwipeMarkPaidInvoked(object sender, EventArgs e)
    {
        var sw   = sender as SwipeItem;
        var item = sw?.CommandParameter as BorrowedDisplayItem;
        if (item != null) await MarkAsPaidAsync(item);
    }

    private async Task MarkAsPaidAsync(BorrowedDisplayItem item)
    {
        var confirmPopup = new ConfirmationPopup(
            "Mark as Paid ✅",
            $"Have you returned ₹{item.Amount:N0} to {item.PersonName}?",
            "This will mark the debt as cleared.",
            "Yes, Paid",
            "Cancel",
            isDestructive: false);

        var confirmResult = await this.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirm || !confirm) return;

        try
        {
            var tx = await _db.BorrowedTransaction.FirstOrDefaultAsync(b => b.Id == item.Id);
            if (tx != null)
            {
                tx.Status = "Paid";
                tx.DateRepaid = DateTime.Today;
                _db.BorrowedTransaction.Update(tx);
                await _db.SaveChangesAsync();
                await UIHelper.ShowToastMessage("Marked as paid! 🎉");
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE
    // ─────────────────────────────────────────────────────────────────────────
    private async void OnSwipeDeleteInvoked(object sender, EventArgs e)
    {
        var sw   = sender as SwipeItem;
        var item = sw?.CommandParameter as BorrowedDisplayItem;
        if (item != null) await DeleteRecordAsync(item);
    }

    private async Task DeleteRecordAsync(BorrowedDisplayItem item)
    {
        var confirmPopup = new ConfirmationPopup(
            "Delete Record ✕",
            $"Remove borrowed record of ₹{item.Amount:N0} from {item.PersonName}?",
            "This action cannot be undone.",
            "Delete",
            "Cancel",
            isDestructive: true);

        var confirmResult = await this.ShowPopupAsync(confirmPopup);
        if (confirmResult is not bool confirm || !confirm) return;

        try
        {
            var tx = await _db.BorrowedTransaction.FirstOrDefaultAsync(b => b.Id == item.Id);
            if (tx != null)
            {
                _db.BorrowedTransaction.Remove(tx);
                await _db.SaveChangesAsync();
                await UIHelper.ShowToastMessage("Record deleted.");
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NAVIGATION
    // ─────────────────────────────────────────────────────────────────────────
    private async void OnBackTapped(object sender, EventArgs e)
        => await Navigation.PopAsync();
}
