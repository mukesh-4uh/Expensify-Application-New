namespace ExpensifyApp.DataBase
{
    using System.ComponentModel.DataAnnotations;
    
    public class BudgetTable
    {
        [Key]
        public int Id { get; set; }
        public int Amount { get; set; }
    }
}
