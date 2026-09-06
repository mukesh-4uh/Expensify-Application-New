using System;
using System.ComponentModel.DataAnnotations;

namespace ExpensifyApp.DataBase
{
    public class LendingTransaction
    {
        [Key]
        public int Id { get; set; }
        public string PersonName { get; set; } = "";
        public string MobileNumber { get; set; } = "";
        public string Relationship { get; set; } = ""; // Friend, Family, Colleague, etc.
        public int Amount { get; set; }
        public string Purpose { get; set; } = ""; // Food, Medical, Rent, Personal, etc.
        public DateTime DateGiven { get; set; } = DateTime.Now;
        public DateTime DueDate { get; set; } = DateTime.Now;
        public DateTime? DateReturned { get; set; }
        public string Status { get; set; } = "Pending"; // "Pending", "Due soon", "Overdue", "Recovered"
        public bool ReminderEnabled { get; set; } = true;
        public int ReminderDays { get; set; } = 3; // 0, 1, 3, 7 days before
        public string Notes { get; set; } = "";
    }
}
