using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System;

namespace ExpensifyApp.Pages;

public partial class ConfirmationPopup : Popup
{
    public ConfirmationPopup(string title, string message, string subtitle = "Please confirm to proceed", string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false)
    {
        InitializeComponent();

        titleLabel.Text = title;
        messageLabel.Text = message;
        subtitleLabel.Text = subtitle;
        confirmButton.Text = confirmText;
        cancelButton.Text = cancelText;

        if (isDestructive)
        {
            // Red Theme
            gradientStart.Color = Color.FromArgb("#E53935");
            gradientEnd.Color = Color.FromArgb("#B71C1C");
            confirmButton.BackgroundColor = Color.FromArgb("#E53935");
            subtitleLabel.TextColor = Color.FromArgb("#FFCDD2");
            iconLabel.Text = "⚠️";
        }
        else
        {
            // Teal Theme
            gradientStart.Color = Color.FromArgb("#0D8C87");
            gradientEnd.Color = Color.FromArgb("#0A7A75");
            confirmButton.BackgroundColor = Color.FromArgb("#0D8C87");
            subtitleLabel.TextColor = Color.FromArgb("#A8E6E2");
            iconLabel.Text = "❓";
        }
    }

    private void OnConfirmClicked(object sender, EventArgs e)
    {
        Close(true);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(false);
    }
}
