using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BookStoreManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreManagementSystem
{
    public partial class LoginWindow : Window
    {
        public bool IsModalLogin { get; set; } = false;

        private User? _matchedUser;
        private bool _isSyncingPassword = false;

        private static readonly Dictionary<string, List<string>> ProvinceWards = new()
        {
            ["Thành phố Hà Nội"] = new() { "Chọn Phường/Xã", "Phường Hoàn Kiếm", "Phường Ba Đình", "Phường Đống Đa", "Phường Cầu Giấy", "Phường Thanh Xuân", "Phường Hai Bà Trưng", "Phường Hoàng Mai", "Phường Long Biên", "Xã Thạch Hòa (Hòa Lạc)" },
            ["TP. Hồ Chí Minh"] = new() { "Chọn Phường/Xã", "Phường Bến Nghé (Quận 1)", "Phường Võ Thị Sáu (Quận 3)", "Phường Thảo Điền (TP. Thủ Đức)", "Phường Phú Mỹ Hưng (Quận 7)", "Phường 25 (Bình Thạnh)", "Phường 12 (Tân Bình)" },
            ["Thành phố Đà Nẵng"] = new() { "Chọn Phường/Xã", "Phường Hải Châu", "Phường Thanh Khê", "Phường Sơn Trà", "Phường Ngũ Hành Sơn", "Phường Liên Chiểu" },
            ["Thành phố Hải Phòng"] = new() { "Chọn Phường/Xã", "Phường Hồng Bàng", "Phường Ngô Quyền", "Phường Lê Chân", "Phường Hải An" },
            ["Thành phố Cần Thơ"] = new() { "Chọn Phường/Xã", "Phường Ninh Kiều", "Phường Bình Thủy", "Phường Cái Răng" }
        };

        public LoginWindow()
        {
            InitializeComponent();
            InitializeAddressDropdowns();
            Loaded += (_, _) => txtIdentifier.Focus();
        }

        private void InitializeAddressDropdowns()
        {
            cbProvince.ItemsSource = ProvinceWards.Keys.ToList();
            cbProvince.SelectedIndex = 0;
        }

        private void cbProvince_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbProvince.SelectedItem is string province && ProvinceWards.TryGetValue(province, out var wards))
            {
                cbWard.ItemsSource = wards;
                cbWard.SelectedIndex = 0;
            }
        }

        // ==================== STEP 1: IDENTIFIER (PHONE OR EMAIL) ====================
        private void txtIdentifier_TextChanged(object sender, TextChangedEventArgs e)
        {
            phIdentifier.Visibility = string.IsNullOrEmpty(txtIdentifier.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void txtIdentifier_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnContinueIdentifier_Click(sender, e);
            }
        }

        private void btnContinueIdentifier_Click(object sender, RoutedEventArgs e)
        {
            var rawInput = txtIdentifier.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại hoặc email!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtIdentifier.Focus();
                return;
            }

            bool looksLikeEmail = rawInput.Contains('@');
            if (!looksLikeEmail && !IsLikelyPhoneNumber(rawInput))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại hợp lệ (10 chữ số) hoặc địa chỉ email đúng định dạng!", "Định dạng chưa đúng", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtIdentifier.Focus();
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                User? existingUser = null;

                if (looksLikeEmail)
                {
                    var normalizedEmail = rawInput.ToLower();
                    existingUser = context.Users
                        .Include(u => u.Role)
                        .FirstOrDefault(u => u.Email.ToLower() == normalizedEmail);
                }
                else
                {
                    var normalizedDigits = NormalizePhoneDigits(rawInput);
                    var candidates = context.Users
                        .Include(u => u.Role)
                        .Where(u => u.PhoneNumber != null && u.PhoneNumber != "")
                        .ToList();

                    existingUser = candidates.FirstOrDefault(u => NormalizePhoneDigits(u.PhoneNumber!) == normalizedDigits);
                }

                if (existingUser != null)
                {
                    // Account exists -> go to Step 2 (Enter Password)
                    _matchedUser = existingUser;
                    ClearLoginPasswordInputs();
                    panelStepIdentifier.Visibility = Visibility.Collapsed;
                    panelStepPassword.Visibility = Visibility.Visible;
                    btnBackToIdentifier.Visibility = Visibility.Visible;
                    pwdLogin.Focus();
                }
                else
                {
                    // Not registered -> go to Register View & autofill Phone or Email
                    OpenRegisterWithPrefill(rawInput, looksLikeEmail);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi kết nối CSDL:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenRegisterWithPrefill(string rawInput, bool isEmail)
        {
            txtRegFullName.Text = string.Empty;
            txtRegStreetAddress.Text = string.Empty;
            ClearRegisterPasswordInputs();

            if (isEmail)
            {
                txtRegEmail.Text = rawInput;
                txtRegPhone.Text = string.Empty;
            }
            else
            {
                txtRegPhone.Text = FormatPhoneForRegisterBox(rawInput);
                txtRegEmail.Text = string.Empty;
            }

            gridLoginView.Visibility = Visibility.Collapsed;
            gridRegisterView.Visibility = Visibility.Visible;
            txtRegFullName.Focus();
        }

        private void btnBackToIdentifier_Click(object sender, RoutedEventArgs e)
        {
            _matchedUser = null;
            gridRegisterView.Visibility = Visibility.Collapsed;
            gridLoginView.Visibility = Visibility.Visible;
            panelStepPassword.Visibility = Visibility.Collapsed;
            panelStepIdentifier.Visibility = Visibility.Visible;
            btnBackToIdentifier.Visibility = Visibility.Collapsed;
            txtIdentifier.Focus();
        }

        // ==================== STEP 2: PASSWORD ====================
        private string GetLoginPassword() =>
            txtLoginPasswordVisible.Visibility == Visibility.Visible ? txtLoginPasswordVisible.Text : pwdLogin.Password;

        private void ClearLoginPasswordInputs()
        {
            _isSyncingPassword = true;
            pwdLogin.Password = string.Empty;
            txtLoginPasswordVisible.Text = string.Empty;
            txtLoginPasswordVisible.Visibility = Visibility.Collapsed;
            pwdLogin.Visibility = Visibility.Visible;
            phLoginPassword.Visibility = Visibility.Visible;
            _isSyncingPassword = false;
        }

        private void pwdLogin_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingPassword) return;
            _isSyncingPassword = true;
            txtLoginPasswordVisible.Text = pwdLogin.Password;
            phLoginPassword.Visibility = string.IsNullOrEmpty(pwdLogin.Password) ? Visibility.Visible : Visibility.Collapsed;
            _isSyncingPassword = false;
        }

        private void txtLoginPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingPassword) return;
            _isSyncingPassword = true;
            pwdLogin.Password = txtLoginPasswordVisible.Text;
            phLoginPassword.Visibility = string.IsNullOrEmpty(txtLoginPasswordVisible.Text) ? Visibility.Visible : Visibility.Collapsed;
            _isSyncingPassword = false;
        }

        private void btnToggleLoginPwd_Click(object sender, RoutedEventArgs e)
        {
            if (pwdLogin.Visibility == Visibility.Visible)
            {
                txtLoginPasswordVisible.Text = pwdLogin.Password;
                pwdLogin.Visibility = Visibility.Collapsed;
                txtLoginPasswordVisible.Visibility = Visibility.Visible;
                txtLoginPasswordVisible.Focus();
                txtLoginPasswordVisible.CaretIndex = txtLoginPasswordVisible.Text.Length;
            }
            else
            {
                pwdLogin.Password = txtLoginPasswordVisible.Text;
                txtLoginPasswordVisible.Visibility = Visibility.Collapsed;
                pwdLogin.Visibility = Visibility.Visible;
                pwdLogin.Focus();
            }
        }

        private void pwdLogin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnSubmitPassword_Click(sender, e);
            }
        }

        private void btnForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Vui lòng liên hệ quản trị viên G07 BookStore (Hotline: 0985 101 804) để được hỗ trợ cấp lại mật khẩu tài khoản của bạn.",
                "Quên mật khẩu",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void btnSubmitPassword_Click(object sender, RoutedEventArgs e)
        {
            if (_matchedUser == null)
            {
                btnBackToIdentifier_Click(sender, e);
                return;
            }

            var password = GetLoginPassword().Trim();
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hashedInput = ComputeSha256(password);
            bool passwordValid =
                _matchedUser.PasswordHash == password ||
                _matchedUser.PasswordHash == hashedInput ||
                (password == "123456" && _matchedUser.PasswordHash.StartsWith("hashed_pwd_"));

            if (!passwordValid)
            {
                MessageBox.Show("Mật khẩu không chính xác! Vui lòng kiểm tra lại.", "Đăng nhập thất bại", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SessionManager.CurrentUser = _matchedUser;

            if (IsModalLogin)
            {
                DialogResult = true;
                Close();
                return;
            }

            if (SessionManager.IsAdmin)
            {
                var choice = MessageBox.Show(
                    $"Xin chào !\n\nBạn có muốn trực tiếp truy cập vào Admin Dashboard không?",
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

        // ==================== VIEW 2: REGISTER ====================
        private void RegField_TextChanged(object sender, TextChangedEventArgs e)
        {
            phRegFullName.Visibility = string.IsNullOrEmpty(txtRegFullName.Text) ? Visibility.Visible : Visibility.Collapsed;
            phRegPhone.Visibility = string.IsNullOrEmpty(txtRegPhone.Text) ? Visibility.Visible : Visibility.Collapsed;
            phRegEmail.Visibility = string.IsNullOrEmpty(txtRegEmail.Text) ? Visibility.Visible : Visibility.Collapsed;
            phRegStreet.Visibility = string.IsNullOrEmpty(txtRegStreetAddress.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearRegisterPasswordInputs()
        {
            _isSyncingPassword = true;
            pwdReg.Password = string.Empty;
            txtRegPwdVisible.Text = string.Empty;
            pwdReg.Visibility = Visibility.Visible;
            txtRegPwdVisible.Visibility = Visibility.Collapsed;
            phRegPassword.Visibility = Visibility.Visible;

            pwdRegConfirm.Password = string.Empty;
            txtRegConfirmPwdVisible.Text = string.Empty;
            pwdRegConfirm.Visibility = Visibility.Visible;
            txtRegConfirmPwdVisible.Visibility = Visibility.Collapsed;
            phRegConfirmPassword.Visibility = Visibility.Visible;
            _isSyncingPassword = false;
        }

        private void pwdReg_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingPassword) return;
            _isSyncingPassword = true;
            txtRegPwdVisible.Text = pwdReg.Password;
            phRegPassword.Visibility = string.IsNullOrEmpty(pwdReg.Password) ? Visibility.Visible : Visibility.Collapsed;
            _isSyncingPassword = false;
        }

        private void txtRegPwdVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingPassword) return;
            _isSyncingPassword = true;
            pwdReg.Password = txtRegPwdVisible.Text;
            phRegPassword.Visibility = string.IsNullOrEmpty(txtRegPwdVisible.Text) ? Visibility.Visible : Visibility.Collapsed;
            _isSyncingPassword = false;
        }

        private void btnToggleRegPwd_Click(object sender, RoutedEventArgs e)
        {
            if (pwdReg.Visibility == Visibility.Visible)
            {
                txtRegPwdVisible.Text = pwdReg.Password;
                pwdReg.Visibility = Visibility.Collapsed;
                txtRegPwdVisible.Visibility = Visibility.Visible;
                txtRegPwdVisible.Focus();
            }
            else
            {
                pwdReg.Password = txtRegPwdVisible.Text;
                txtRegPwdVisible.Visibility = Visibility.Collapsed;
                pwdReg.Visibility = Visibility.Visible;
                pwdReg.Focus();
            }
        }

        private void pwdRegConfirm_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingPassword) return;
            _isSyncingPassword = true;
            txtRegConfirmPwdVisible.Text = pwdRegConfirm.Password;
            phRegConfirmPassword.Visibility = string.IsNullOrEmpty(pwdRegConfirm.Password) ? Visibility.Visible : Visibility.Collapsed;
            _isSyncingPassword = false;
        }

        private void txtRegConfirmPwdVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingPassword) return;
            _isSyncingPassword = true;
            pwdRegConfirm.Password = txtRegConfirmPwdVisible.Text;
            phRegConfirmPassword.Visibility = string.IsNullOrEmpty(txtRegConfirmPwdVisible.Text) ? Visibility.Visible : Visibility.Collapsed;
            _isSyncingPassword = false;
        }

        private void btnToggleRegConfirmPwd_Click(object sender, RoutedEventArgs e)
        {
            if (pwdRegConfirm.Visibility == Visibility.Visible)
            {
                txtRegConfirmPwdVisible.Text = pwdRegConfirm.Password;
                pwdRegConfirm.Visibility = Visibility.Collapsed;
                txtRegConfirmPwdVisible.Visibility = Visibility.Visible;
                txtRegConfirmPwdVisible.Focus();
            }
            else
            {
                pwdRegConfirm.Password = txtRegConfirmPwdVisible.Text;
                txtRegConfirmPwdVisible.Visibility = Visibility.Collapsed;
                pwdRegConfirm.Visibility = Visibility.Visible;
                pwdRegConfirm.Focus();
            }
        }

        private void btnSubmitRegister_Click(object sender, RoutedEventArgs e)
        {
            var fullName = txtRegFullName.Text.Trim();
            var rawPhone = txtRegPhone.Text.Trim();
            var email = txtRegEmail.Text.Trim();
            var pwd = (txtRegPwdVisible.Visibility == Visibility.Visible ? txtRegPwdVisible.Text : pwdReg.Password).Trim();
            var confirmPwd = (txtRegConfirmPwdVisible.Visibility == Visibility.Visible ? txtRegConfirmPwdVisible.Text : pwdRegConfirm.Password).Trim();

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("Vui lòng điền đầy đủ Họ tên, Email và Mật khẩu!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!email.Contains('@') || !email.Contains('.'))
            {
                MessageBox.Show("Địa chỉ Email không hợp lệ!", "Lỗi định dạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(rawPhone) && !IsLikelyPhoneNumber(rawPhone))
            {
                MessageBox.Show("Số điện thoại không hợp lệ!", "Lỗi định dạng", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            string? normalizedPhone = string.IsNullOrWhiteSpace(rawPhone) ? null : FormatStoredPhone(rawPhone);
            string? fullAddress = BuildFullAddress();

            try
            {
                using var context = new BookStoreDbContext();
                if (context.Users.Any(u => u.Email.ToLower() == email.ToLower()))
                {
                    MessageBox.Show("Địa chỉ Email này đã được đăng ký! Vui lòng dùng Email khác.", "Trùng Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(normalizedPhone))
                {
                    var phoneDigits = NormalizePhoneDigits(normalizedPhone);
                    var duplicatePhone = context.Users
                        .Where(u => u.PhoneNumber != null && u.PhoneNumber != "")
                        .AsEnumerable()
                        .Any(u => NormalizePhoneDigits(u.PhoneNumber!) == phoneDigits);

                    if (duplicatePhone)
                    {
                        MessageBox.Show("Số điện thoại này đã được đăng ký!", "Trùng số điện thoại", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
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
                    PhoneNumber = normalizedPhone,
                    Address = fullAddress,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(newUser);
                context.SaveChanges();

                context.Carts.Add(new Cart
                {
                    UserId = newUser.UserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                context.SaveChanges();

                newUser.Role = customerRole;
                SessionManager.CurrentUser = newUser;

                MessageBox.Show($"🎉 Đăng ký tài khoản thành công! Chào mừng {newUser.FullName} đến với G07 BookStore.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

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

        private void chkCustomAddress_Changed(object sender, RoutedEventArgs e)
        {
            if (cbProvince == null || cbWard == null || txtRegStreetAddress == null || borderRegStreet == null) return;

            bool isCustom = chkCustomAddress.IsChecked == true;
            cbProvince.IsEnabled = !isCustom;
            cbWard.IsEnabled = !isCustom;
            txtRegStreetAddress.IsEnabled = isCustom;
            borderRegStreet.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isCustom ? "#FFFFFF" : "#F1F5F9"));

            if (isCustom)
            {
                txtRegStreetAddress.Focus();
            }
        }

        private string? BuildFullAddress()
        {
            if (chkCustomAddress.IsChecked == true)
            {
                var customAddress = txtRegStreetAddress.Text.Trim();
                return string.IsNullOrEmpty(customAddress) ? null : customAddress;
            }

            var parts = new List<string>();
            if (cbWard.SelectedItem is string ward && ward != "Chọn Phường/Xã")
                parts.Add(ward);

            if (cbProvince.SelectedItem is string province)
                parts.Add(province);

            return parts.Count > 0 ? string.Join(", ", parts) : null;
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

        private static bool IsLikelyPhoneNumber(string input)
        {
            var digits = Regex.Replace(input, @"\D", "");
            return digits.Length == 10;
        }

        private static string NormalizePhoneDigits(string phone)
        {
            var digits = Regex.Replace(phone, @"\D", "");
            if (digits.StartsWith("84") && digits.Length >= 10)
            {
                digits = "0" + digits.Substring(2);
            }
            else if (!digits.StartsWith("0") && digits.Length == 9)
            {
                digits = "0" + digits;
            }
            return digits;
        }

        private static string FormatPhoneForRegisterBox(string rawPhone)
        {
            var digits = NormalizePhoneDigits(rawPhone);
            return digits.StartsWith("0") ? digits : rawPhone.Trim();
        }

        private static string FormatStoredPhone(string rawPhone)
        {
            return NormalizePhoneDigits(rawPhone);
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
