namespace ExpensifyApp.DataBase
{
    using System.ComponentModel.DataAnnotations;

    public class RecurringExpenseTable
    {
        [Key]
        public int Id { get; set; }

        public string Category { get; set; } = "";

        public string SubCategory { get; set; } = "";

        public string PayMode { get; set; } = "";

        public int Amount { get; set; }

        /// <summary>
        /// The day of the month (1-31) on which this recurring expense should be created.
        /// </summary>
        public int DayOfMonth { get; set; }

        /// <summary>
        /// Whether this recurring expense is currently active.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
