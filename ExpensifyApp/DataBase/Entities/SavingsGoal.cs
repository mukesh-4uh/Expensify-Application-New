using System;
using System.ComponentModel.DataAnnotations;

namespace ExpensifyApp.DataBase
{
    public class SavingsGoal
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int TargetAmount { get; set; }
        public int CurrentAmount { get; set; }
        public int AllocationPercentage { get; set; } = 0; // Divider for monthly auto allocation
        public DateTime Deadline { get; set; } = DateTime.Today.AddMonths(6);
        public string CategoryIcon { get; set; } = "💰"; // Emoji icon
        public bool IsCompleted { get; set; } = false;
    }
}
