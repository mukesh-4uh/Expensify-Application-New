using System;
using System.ComponentModel.DataAnnotations;

namespace ExpensifyApp.DataBase
{
    public class UserFinancialProfile
    {
        [Key]
        public int Id { get; set; }
        public string DashboardMode { get; set; } = "Monthly budget"; // "Monthly budget" or "Monthly commitments"
        public string ProfileType { get; set; } = ""; // "Salaried employee", "Business owner", etc.
        public int MonthlyIncome { get; set; }
        public int CommitmentPercentage { get; set; } = 50;
        public int SavingsPercentage { get; set; } = 30;
        public int ExpensePercentage { get; set; } = 20;
        public string SavingsMode { get; set; } = "50/30/20"; // "50/30/20", "60/20/20", "Custom", etc.
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Auto Save Options
        public bool AutoSaveSalary { get; set; } = false;
        public int AutoSaveFixedAmount { get; set; } = 0;
        public bool AutoSaveRoundUp { get; set; } = false;
    }
}
