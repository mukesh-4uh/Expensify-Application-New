using System;
using System.Collections.Generic;

namespace ExpensifyApp.Services
{
    public static class CategorySuggestionService
    {
        private static readonly Dictionary<string, string> KeywordMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Food
            { "dinner", "Food" },
            { "lunch", "Food" },
            { "breakfast", "Food" },
            { "snack", "Food" },
            { "restaurant", "Food" },
            { "swiggy", "Food" },
            { "zomato", "Food" },
            { "tea", "Food" },
            { "coffee", "Food" },
            { "food", "Food" },

            // Groceries
            { "grocery", "Groceries" },
            { "vegetables", "Groceries" },
            { "fruits", "Groceries" },
            { "supermarket", "Groceries" },
            { "milk", "Groceries" },

            // Travel / Fuel
            { "petrol", "Travel" },
            { "diesel", "Travel" },
            { "fuel", "Travel" },
            { "cab", "Travel" },
            { "uber", "Travel" },
            { "ola", "Travel" },
            { "bus", "Travel" },
            { "train", "Travel" },
            { "flight", "Travel" },
            { "auto", "Travel" },
            { "toll", "Travel" },

            // Shopping
            { "amazon", "Shopping" },
            { "flipkart", "Shopping" },
            { "clothes", "Shopping" },
            { "shoes", "Shopping" },
            { "mall", "Shopping" },
            { "myntra", "Shopping" },
            { "shopping", "Shopping" },

            // Entertainment
            { "movie", "Entertainment" },
            { "cinema", "Entertainment" },
            { "netflix", "Entertainment" },
            { "prime", "Entertainment" },
            { "hotstar", "Entertainment" },
            { "game", "Entertainment" },

            // Medicine
            { "medicine", "Medicine" },
            { "doctor", "Medicine" },
            { "pharmacy", "Medicine" },
            { "hospital", "Medicine" },
            { "clinic", "Medicine" },

            // Education
            { "school", "Education" },
            { "college", "Education" },
            { "fees", "Education" },
            { "books", "Education" },
            { "course", "Education" },
            { "tuition", "Education" },

            // Rent
            { "rent", "Rent" },
            { "house rent", "Rent" },

            // Loan / Lending
            { "loan", "Loan" },
            { "emi", "Loan" },
            { "lending", "Lending" },
            { "borrow", "Lending" }
        };

        public static string SuggestCategory(string purpose)
        {
            if (string.IsNullOrWhiteSpace(purpose))
                return "Others";

            string text = purpose.Trim().ToLower();

            foreach (var kvp in KeywordMap)
            {
                if (text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return "Others";
        }
    }
}
