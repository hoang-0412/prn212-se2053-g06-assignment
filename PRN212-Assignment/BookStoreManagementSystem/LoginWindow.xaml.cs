using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using BookStoreManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreManagementSystem
{
    public partial class LoginWindow : Window
    {
        public bool IsModalLogin { get; set; } = false;

        public LoginWindow()
        {
            InitializeComponent();
            txtLoginPassword.Password = "hashed_pwd_user_456";
        }

        private void btnTabLogin_Click(object sender, RoutedEventArgs e)
        {
            panelLogin.Visibility = Visibility.Visible;
            panelRegister.Visibility = Visibility.Collapsed;

            btnTabLogin.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
            btnTabLogin.Foreground = Brushes.White;
            btnTabRegister.Background = Brushes.Transparent;
            btnTabRegister.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
        }

        private void btnTabRegister_Click(object sender, RoutedEventArgs e)
        {
            panelLogin.Visibility = Visibility.Collapsed;
            panelRegister.Visibility = Visibility.Visible;

            btnTabRegister.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
            btnTabRegister.Foreground = Brushes.White;
            btnTabLogin.Background = Brushes.Transparent;
            btnTabLogin.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
        }

        private void btnQuickAdmin_Click(object sender, RoutedEventArgs e)
        {
            txtLoginEmail.Text = "admin@bookstore.com";
            txtLoginPassword.Password = "hashed_pwd_admin_123";
            PerformLogin("admin@bookstore.com", "hashed_pwd_admin_123", openAdminDirectly: true);
        }

        private void btnQuickCustomer_Click(object sender, RoutedEventArgs e)
        {
            txtLoginEmail.Text = "nguyenvana@gmail.com";
            txtLoginPassword.Password = "hashed_pwd_user_456";
            PerformLogin("nguyenvana@gmail.com", "hashed_pwd_user_456", openAdminDirectly: false);
        }

        private void btnSubmitLogin_Click(object sender, RoutedEventArgs e)
        {
            var email = txtLoginEmail.Text.Trim();
            var password = txtLoginPassword.Password.Trim();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Email và Mật khẩu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PerformLogin(email, password, openAdminDirectly: false);
        }

        private void PerformLogin(string email, string password, bool openAdminDirectly)
        {
            try
            {
                using var context = new BookStoreDbContext();
                var user = context.Users
                    .Include(u => u.Role)
                    .FirstOrDefault(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    MessageBox.Show("Không tìm thấy tài khoản với địa chỉ Email này!", "Đăng nhập thất bại", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var hashedInput = ComputeSha256(password);
                bool passwordValid =
                    user.PasswordHash == password ||
                    user.PasswordHash == hashedInput ||
                    (password == "123456" && (user.PasswordHash.StartsWith("hashed_pwd_")));

                if (!passwordValid)
                {
                    MessageBox.Show("Mật khẩu không chính xác! Vui lòng kiểm tra lại.", "Đăng nhập thất bại", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SessionManager.CurrentUser = user;

                if (IsModalLogin)
                {
                    DialogResult = true;
                    Close();
                    return;
                }

                if (SessionManager.IsAdmin && openAdminDirectly)
                {
                    var adminWin = new AdminDashboardWindow();
                    adminWin.Show();
                    Close();
                }
                else if (SessionManager.IsAdmin)
                {
                    var choice = MessageBox.Show(
                        $"Xin chào Quản trị viên {user.FullName}!\n\nBạn muốn mở thẳng Admin Dashboard (Yes) hay vào Trang Chủ Cửa Hàng (No)?",
                        "Đăng nhập Admin thành công",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (choice == MessageBoxResult.Yes)
                    {
                        var adminWin = new AdminDashboardWindow();
                        adminWin.Show();
                    }
                    else
                    {
                        var home = new HomePage();
                        home.Show();
                    }
                    Close();
                }
                else
                {
                    var home = new HomePage();
                    home.Show();
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối CSDL khi đăng nhập:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSubmitRegister_Click(object sender, RoutedEventArgs e)
        {
            var fullName = txtRegFullName.Text.Trim();
            var phone = txtRegPhone.Text.Trim();
            var email = txtRegEmail.Text.Trim();
            var address = txtRegAddress.Text.Trim();
            var pwd = txtRegPassword.Password.Trim();
            var confirmPwd = txtRegConfirmPassword.Password.Trim();

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("Vui lòng điền đầy đủ Họ tên, Email và Mật khẩu!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                MessageBox.Show("Địa chỉ Email không hợp lệ!", "Lỗi định dạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pwd.Length < 4)
            {
                MessageBox.Show("Mật khẩu phải có ít nhất 4 ký tự!", "Lỗi mật khẩu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pwd != confirmPwd)
            {
                MessageBox.Show("Mật khẩu xác nhận không khớp!", "Lỗi mật khẩu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                if (context.Users.Any(u => u.Email.ToLower() == email.ToLower()))
                {
                    MessageBox.Show("Địa chỉ Email này đã được đăng ký! Vui lòng dùng Email khác.", "Trùng Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var customerRole = context.Roles.FirstOrDefault(r => r.RoleName == "Customer")
                                   ?? context.Roles.OrderBy(r => r.RoleId).FirstOrDefault();

                if (customerRole == null)
                {
                    customerRole = new Role { RoleName = "Customer" };
                    context.Roles.Add(customerRole);
                    context.SaveChanges();
                }

                var newUser = new User
                {
                    RoleId = customerRole.RoleId,
                    FullName = fullName,
                    Email = email,
                    PasswordHash = pwd,
                    PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone,
                    Address = string.IsNullOrEmpty(address) ? null : address,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(newUser);
                context.SaveChanges();

                // Tạo giỏ hàng mặc định cho User
                var cart = new Cart
                {
                    UserId = newUser.UserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Carts.Add(cart);
                context.SaveChanges();

                // Nạp lại kèm Role
                newUser.Role = customerRole;
                SessionManager.CurrentUser = newUser;

                MessageBox.Show($"🎉 Đăng ký tài khoản thành công! Chào mừng {newUser.FullName} đến với BookStore.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                if (IsModalLogin)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    var home = new HomePage();
                    home.Show();
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể đăng ký tài khoản:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnContinueAsGuest_Click(object sender, RoutedEventArgs e)
        {
            if (IsModalLogin)
            {
                DialogResult = false;
                Close();
            }
            else
            {
                var home = new HomePage();
                home.Show();
                Close();
            }
        }

        public static string ComputeSha256(string rawData)
        {
            using var sha256Hash = SHA256.Create();
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}

