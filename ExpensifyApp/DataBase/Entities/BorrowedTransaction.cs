using System;
using System.ComponentModel.DataAnnotations;

namespace ExpensifyApp.DataBase
{
    public class BorrowedTransaction
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Name of the person the user borrowed from.</summary>
        public string PersonName { get; set; } = "";

        /// <summary>Contact number (optional).</summary>
        public string MobileNumber { get; set; } = "";

        /// <summary>Relationship: Friend, Family, Colleague, etc.</summary>
        public string Relationship { get; set; } = "";

        /// <summary>Amount borrowed in ₹.</summary>
        public int Amount { get; set; }

        /// <summary>Reason / purpose of borrowing.</summary>
        public string Purpose { get; set; } = "";

        /// <summary>Date the money was borrowed.</summary>
        public DateTime DateBorrowed { get; set; } = DateTime.Now;

        /// <summary>Expected date to repay.</summary>
        public DateTime DueDate { get; set; } = DateTime.Now;

        /// <summary>Date the money was actually repaid (null if not yet paid).</summary>
        public DateTime? DateRepaid { get; set; }

        /// <summary>Status: Pending | Due soon | Due Today | Overdue | Paid</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Optional notes.</summary>
        public string Notes { get; set; } = "";
    }
}
