using ExpensifyApp.DataBase;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ExpensifyApp.Helpers
{
    public static class AutoSaveHelper
    {
        public static async Task HandleRoundUp(ExpenseContext db, int expenseAmount)
        {
            try
            {
                var profile = await db.UserFinancialProfile.FirstOrDefaultAsync();
                if (profile == null || !profile.AutoSaveRoundUp) return;

                int targetRounded = 0;
                if (expenseAmount <= 0) return;

                if (expenseAmount < 100)
                {
                    // Round up to next multiple of 10 (e.g. 82 -> 90)
                    targetRounded = ((expenseAmount + 9) / 10) * 10;
                }
                else
                {
                    // Round up to next multiple of 100 (e.g. 182 -> 200)
                    targetRounded = ((expenseAmount + 99) / 100) * 100;
                }

                int diff = targetRounded - expenseAmount;
                if (diff <= 0) return;

                // Default to Emergency Fund or the first available savings goal
                var targetGoal = await db.SavingsGoal.FirstOrDefaultAsync(g => g.Name.Contains("Emergency"));
                if (targetGoal == null)
                {
                    targetGoal = await db.SavingsGoal.FirstOrDefaultAsync();
                }

                if (targetGoal != null)
                {
                    targetGoal.CurrentAmount += diff;
                    db.SavingsGoal.Update(targetGoal);

                    var tx = new SavingsTransaction
                    {
                        Amount = diff,
                        Type = "Transfer",
                        Date = DateTime.Now,
                        Notes = $"Round-up from purchase (₹{expenseAmount})",
                        GoalId = targetGoal.Id,
                        GoalName = targetGoal.Name
                    };
                    db.SavingsTransaction.Add(tx);
                    await db.SaveChangesAsync();
                }
            }
            catch { }
        }
    }
}
