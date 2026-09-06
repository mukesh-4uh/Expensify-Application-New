using System;
using System.ComponentModel.DataAnnotations;

namespace ExpensifyApp.DataBase
{
    public class MonthlyCommitment
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Amount { get; set; }
        public int DueDay { get; set; }
        public bool IsActive { get; set; } = true;
        public bool AutoAdd { get; set; } = true;
        public string Category { get; set; } = "";
    }
}
