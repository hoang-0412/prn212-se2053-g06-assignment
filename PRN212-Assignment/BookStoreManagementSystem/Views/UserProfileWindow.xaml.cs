using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BookStoreManagementSystem.Models;
using BookStoreManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BookStoreManagementSystem
{
    public partial class UserProfileWindow : Window
    {
        private readonly string _returnScreen;
        private List<OrderViewModel> _myOrders = new();
        private OrderViewModel? _selectedProfileOrder;
        private bool _isLoaded = false;

        public UserProfileWindow() : this("Home")
        {
        }

        public UserProfileWindow(string returnScreen)
        {
            InitializeComponent();
            _returnScreen = returnScreen;
            Loaded += UserProfileWindow_Loaded;
        }

        private void UserProfileWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureLoggedInUser();
            LoadProfileAndOrders();
            _isLoaded = true;
        }

        private void EnsureLoggedInUser()
        {
            if (SessionManager.CurrentUser != null) return;

            try
            {
                using var context = new BookStoreDbContext();
                var defaultUser = context.Users
                    .Include(u => u.Role)
                    .FirstOrDefault(u => u.RoleId == 1)
                    ?? context.Users.Include(u => u.Role).FirstOrDefault();

                if (defaultUser != null)
                {
                    SessionManager.CurrentUser = defaultUser;
                }
            }
            catch
            {
            }
        }

        private void LoadProfileAndOrders()
        {
            headerControl?.RefreshAuthAndCart();

            if (SessionManager.CurrentUser == null) return;

            try
            {
                using var context = new BookStoreDbContext();
                var dbUser = context.Users
                    .Include(u => u.Role)
                    .FirstOrDefault(u => u.UserId == SessionManager.CurrentUser.UserId);

                if (dbUser == null) return;

                SessionManager.CurrentUser = dbUser;

                txtProfileHeaderName.Text = dbUser.FullName;
                txtProfileHeaderEmail.Text = dbUser.Email;
                txtProfileHeaderRole.Text = SessionManager.RoleDisplay;

                txtProfileFullName.Text = dbUser.FullName;
                txtProfileEmail.Text = dbUser.Email;
                txtProfilePhone.Text = dbUser.PhoneNumber ?? string.Empty;
                txtProfileAddress.Text = dbUser.Address ?? string.Empty;

                var orders = context.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Book)
                    .Where(o => o.UserId == dbUser.UserId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                _myOrders = orders.Select(o => new OrderViewModel
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    CustomerName = dbUser.FullName,
                    CustomerEmail = dbUser.Email,
                    RecipientPhone = o.RecipientPhone,
                    ShippingAddress = o.ShippingAddress,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt,
                    TotalQuantity = o.OrderItems.Sum(i => i.Quantity),
                    ItemsSummary = string.Join(", ", o.OrderItems.Select(i => $"{i.Book?.Title} (x{i.Quantity})")),
                    Items = o.OrderItems.Select(i => new OrderItemDetailViewModel
                    {
                        OrderItemId = i.OrderItemId,
                        BookId = i.BookId,
                        BookTitle = i.Book?.Title ?? $"Sách #{i.BookId}",
                        Author = i.Book?.Author ?? string.Empty,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList()
                }).ToList();

                ApplyOrderFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải thông tin hồ sơ:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyOrderFilter()
        {
            IEnumerable<OrderViewModel> query = _myOrders;

            if (cbProfileOrderFilter?.SelectedItem is ComboBoxItem selected)
            {
                var mode = selected.Content?.ToString();
                if (mode == "Đơn đang thuê")
                {
                    query = query.Where(o => o.CanReturnBook);
                }
                else if (mode == "Đơn mua sở hữu")
                {
                    query = query.Where(o => !o.IsRentalOrder);
                }
                else if (mode == "Chờ xác nhận")
                {
                    query = query.Where(o => o.CanCancel);
                }
            }

            dgProfileOrders.ItemsSource = query.ToList();

            if (_selectedProfileOrder != null && !query.Contains(_selectedProfileOrder))
            {
                BindSelectedProfileOrder(null);
            }
        }

        private void cbProfileOrderFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            ApplyOrderFilter();
        }

        private void dgProfileOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BindSelectedProfileOrder(dgProfileOrders.SelectedItem as OrderViewModel);
        }

        private void BindSelectedProfileOrder(OrderViewModel? order)
        {
            _selectedProfileOrder = order;
            if (order == null)
            {
                panelProfileOrderDetail.Visibility = Visibility.Collapsed;
                return;
            }

            txtProfileDetailOrderHeader.Text = $"🧾 Chi Tiết Đơn {order.OrderCode}";
            txtProfileDetailStatus.Text = $"{order.OrderTypeDisplay} • Trạng thái: {order.StatusDisplay}";
            txtProfileDetailCreatedAt.Text = $"Ngày đặt: {order.FormattedCreatedAt}";
            txtProfileDetailPhone.Text = $"SĐT nhận: {order.RecipientPhone}";
            txtProfileDetailAddress.Text = $"Địa chỉ: {order.ShippingAddress}";
            txtProfileDetailPayment.Text = $"Thanh toán: {order.PaymentMethod} ({order.PaymentStatus})";
            txtProfileDetailTotal.Text = order.FormattedTotalAmount;
            dgProfileOrderItems.ItemsSource = order.Items;
            panelProfileOrderDetail.Visibility = Visibility.Visible;
        }

        // ==================== SAVE PROFILE & CHANGE PASSWORD ====================
        private void btnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (SessionManager.CurrentUser == null) return;

            var fullName = txtProfileFullName.Text.Trim();
            var email = txtProfileEmail.Text.Trim();
            var phone = txtProfilePhone.Text.Trim();
            var address = txtProfileAddress.Text.Trim();

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Họ tên và Email đăng nhập không được để trống!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                MessageBox.Show("Định dạng Email không hợp lệ!", "Lỗi định dạng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                int currentUserId = SessionManager.CurrentUser.UserId;

                if (context.Users.Any(u => u.Email.ToLower() == email.ToLower() && u.UserId != currentUserId))
                {
                    MessageBox.Show("Địa chỉ Email này đã được sử dụng bởi một tài khoản khác!", "Trùng Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var user = context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == currentUserId);
                if (user == null) return;

                user.FullName = fullName;
                user.Email = email;
                user.PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone;
                user.Address = string.IsNullOrEmpty(address) ? null : address;

                context.SaveChanges();
                SessionManager.CurrentUser = user;

                LoadProfileAndOrders();
                MessageBox.Show("✅ Đã cập nhật thông tin hồ sơ cá nhân thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu thông tin hồ sơ:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (SessionManager.CurrentUser == null) return;

            var currentPwd = txtCurrentPassword.Password.Trim();
            var newPwd = txtNewPassword.Password.Trim();
            var confirmPwd = txtConfirmNewPassword.Password.Trim();

            if (string.IsNullOrEmpty(currentPwd) || string.IsNullOrEmpty(newPwd) || string.IsNullOrEmpty(confirmPwd))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Mật khẩu hiện tại, Mật khẩu mới và Xác nhận mật khẩu mới!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPwd.Length < 4)
            {
                MessageBox.Show("Mật khẩu mới phải có ít nhất 4 ký tự!", "Lỗi mật khẩu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPwd != confirmPwd)
            {
                MessageBox.Show("Mật khẩu xác nhận không khớp với mật khẩu mới!", "Lỗi xác nhận", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var user = context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == SessionManager.CurrentUser.UserId);
                if (user == null) return;

                var hashedCurrent = LoginWindow.ComputeSha256(currentPwd);
                bool validOldPassword =
                    user.PasswordHash == currentPwd ||
                    user.PasswordHash == hashedCurrent ||
                    (currentPwd == "123456" && user.PasswordHash.StartsWith("hashed_pwd_"));

                if (!validOldPassword)
                {
                    MessageBox.Show("Mật khẩu hiện tại không chính xác! Vui lòng kiểm tra lại.", "Sai mật khẩu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                user.PasswordHash = newPwd;
                context.SaveChanges();
                SessionManager.CurrentUser = user;

                txtCurrentPassword.Password = string.Empty;
                txtNewPassword.Password = string.Empty;
                txtConfirmNewPassword.Password = string.Empty;

                MessageBox.Show("🔑 Đã đổi mật khẩu đăng nhập thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đổi mật khẩu:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== RENTAL EXTENSION, RETURN & ORDER CANCEL ====================
        private void btnExtendRental7Days_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderViewModel orderVm)
            {
                decimal extensionFee = 20000m;
                if (MessageBox.Show(
                    $"Bạn muốn gia hạn đơn thuê {orderVm.OrderCode} thêm 7 ngày (Phí gia hạn: {extensionFee:N0} đ)?",
                    "Gia hạn thuê sách (+7 ngày)",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                try
                {
                    using var context = new BookStoreDbContext();
                    var order = context.Orders.FirstOrDefault(o => o.OrderId == orderVm.OrderId);
                    if (order == null) return;

                    order.TotalAmount += extensionFee;
                    order.OrderStatus = "Renting";
                    order.ShippingAddress = $"{order.ShippingAddress} [Đã gia hạn +7 ngày đến {DateTime.Now.AddDays(7):dd/MM/yyyy}]";
                    context.SaveChanges();

                    LoadProfileAndOrders();
                    MessageBox.Show($"⏳ Đã gia hạn đơn thuê {orderVm.OrderCode} thêm 7 ngày thành công!", "Gia hạn thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi gia hạn thuê sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnReturnRentalBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderViewModel orderVm)
            {
                if (MessageBox.Show(
                    $"Xác nhận hoàn trả sách cho đơn thuê {orderVm.OrderCode}?",
                    "Hoàn trả sách thuê",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                try
                {
                    using var context = new BookStoreDbContext();
                    var order = context.Orders
                        .Include(o => o.OrderItems)
                        .FirstOrDefault(o => o.OrderId == orderVm.OrderId);

                    if (order == null) return;

                    order.OrderStatus = "ReturnPending";
                    context.SaveChanges();
                    LoadProfileAndOrders();
                    MessageBox.Show($"⏳ Đã gửi yêu cầu trả sách cho đơn {orderVm.OrderCode}.\nTrạng thái: Chờ xác nhận trả. Admin sẽ kiểm tra và xác nhận hoàn kho!", "Chờ xác nhận trả sách", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi hoàn trả sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnCancelOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderViewModel orderVm)
            {
                if (MessageBox.Show(
                    $"Bạn có chắc chắn muốn hủy đơn hàng {orderVm.OrderCode} không?",
                    "Xác nhận hủy đơn",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                try
                {
                    using var context = new BookStoreDbContext();
                    var order = context.Orders
                        .Include(o => o.OrderItems)
                        .FirstOrDefault(o => o.OrderId == orderVm.OrderId);

                    if (order == null) return;

                    order.OrderStatus = "Cancelled";
                    foreach (var item in order.OrderItems)
                    {
                        var book = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                        if (book != null)
                        {
                            book.StockQuantity += item.Quantity;
                        }
                    }

                    context.SaveChanges();
                    LoadProfileAndOrders();
                    MessageBox.Show($"Đã hủy đơn hàng {orderVm.OrderCode} và hoàn lại số lượng tồn kho!", "Đã hủy đơn", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi hủy đơn hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ==================== NAVIGATION ====================
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (string.Equals(_returnScreen, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var adminWin = new AdminDashboardWindow();
                adminWin.Show();
                Close();
            }
            else if (string.Equals(_returnScreen, "BookList", StringComparison.OrdinalIgnoreCase))
            {
                var listWin = new BookListWindow();
                listWin.Show();
                Close();
            }
            else
            {
                var homeWin = new HomePage();
                homeWin.Show();
                Close();
            }
        }

        private void btnGoHome_Click(object sender, RoutedEventArgs e)
        {
            var homeWin = new HomePage();
            homeWin.Show();
            Close();
        }

        private void btnGoBookList_Click(object sender, RoutedEventArgs e)
        {
            var listWin = new BookListWindow();
            listWin.Show();
            Close();
        }

        private void btnOpenVouchers_Click(object sender, RoutedEventArgs e)
        {
            var voucherWin = new UserVouchersWindow
            {
                Owner = this
            };
            voucherWin.ShowDialog();
        }

        private void btnOpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin) return;
            var adminWin = new AdminDashboardWindow();
            adminWin.Show();
            Close();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
            var loginWin = new LoginWindow();
            loginWin.Show();
            Close();
        }
    }
}

