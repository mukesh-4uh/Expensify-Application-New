using ExpensifyApp.DataBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ExpensifyApp.Helpers
{
    public static class AppLoadActivityHelper
    {
        public static async Task DoAppInitWork()
        {
            try
            {
                using var _db = new ExpenseContext();
                await _db.DoInitWork();

                await PermissionHelper.CheckAndRequestCameraPermission();
            }
            catch (Exception ex)
            {
                await UIHelper.HandleException(ex);
            }
        }
    }
}
