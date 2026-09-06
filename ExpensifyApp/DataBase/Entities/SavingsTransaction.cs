using System;
using System.ComponentModel.DataAnnotations;

namespace ExpensifyApp.DataBase
{
    public class SavingsTransaction
    {
        [Key]
        public int Id { get; set; }
        public int Amount { get; set; }
        public string Type { get; set; } = "Add"; // "Add", "Withdraw", "Transfer"
        public DateTime Date { get; set; } = DateTime.Now;
        public string Notes { get; set; } = "";
        public int? GoalId { get; set; } // Nullable if general savings, or target goal
        public string GoalName { get; set; } = ""; // Helper goal title cache
    }
}
