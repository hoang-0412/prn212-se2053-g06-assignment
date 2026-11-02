using BookStoreManagementSystem.Models;

namespace BookStoreManagementSystem
{
    public static class SessionManager
    {
        public static User? CurrentUser { get; set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static bool IsAdmin =>
            CurrentUser != null &&
            (CurrentUser.RoleId == 2 ||
             string.Equals(CurrentUser.Role?.RoleName, "Admin", System.StringComparison.OrdinalIgnoreCase));

        public static string DisplayName =>
            CurrentUser != null ? CurrentUser.FullName : "Khách vãng lai";

        public static string RoleDisplay =>
            CurrentUser == null ? "Khách (Guest)" : (IsAdmin ? "Quản trị viên (Admin)" : "Khách hàng (Customer)");

        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}

