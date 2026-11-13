using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using BookStoreManagementSystem.Models;
using BookStoreManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace BookStoreManagementSystem
{
    public partial class CustomerOrdersWindow : Window
    {
        private List<OrderViewModel> _allOrders = new();
        private OrderViewModel? _selectedOrder;
        private bool _isLoaded = false;

        public CustomerOrdersWindow()
        {
            InitializeComponent();
            Loaded += CustomerOrdersWindow_Loaded;
        }

        private void CustomerOrdersWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureUserSession();
            LoadOrdersFromDatabase();
            _isLoaded = true;
        }

        private void EnsureUserSession()
        {
            if (SessionManager.CurrentUser == null)
            {
                try
                {
                    using var context = new BookStoreDbContext();
                    var customer = context.Users.Include(u => u.Role).FirstOrDefault(u => u.RoleId == 1)
                                   ?? context.Users.Include(u => u.Role).FirstOrDefault();
                    if (customer != null)
                    {
                        SessionManager.CurrentUser = customer;
                    }
                }
                catch
                {
                }
            }

            headerControl?.RefreshAuthAndCart();
        }

        private void LoadOrdersFromDatabase()
        {
            if (SessionManager.CurrentUser == null) return;

            try
            {
                using var context = new BookStoreDbContext();
                int currentUserId = SessionManager.CurrentUser.UserId;

                var query = context.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Book)
                    .Where(o => o.UserId == currentUserId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                _allOrders = query.Select(o => new OrderViewModel
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    CustomerName = o.User?.FullName ?? SessionManager.DisplayName,
                    CustomerEmail = o.User?.Email ?? "",
                    RecipientPhone = o.RecipientPhone,
                    ShippingAddress = o.ShippingAddress,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    CancelReason = o.CancelReason,
                    CreatedAt = o.CreatedAt,
                    TotalQuantity = o.OrderItems.Sum(i => i.Quantity),
                    ItemsSummary = string.Join(", ", o.OrderItems.Select(i => $"{i.Book?.Title} (x{i.Quantity})")),
                    Items = o.OrderItems.Select(i => new OrderItemDetailViewModel
                    {
                        OrderItemId = i.OrderItemId,
                        BookId = i.BookId,
                        BookTitle = i.Book?.Title ?? $"Sách #{i.BookId}",
                        Author = i.Book?.Author ?? "",
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList()
                }).ToList();

                ApplyFilters();

                if (dgCustomerOrders.Items.Count > 0)
                {
                    dgCustomerOrders.SelectedIndex = 0;
                }
                else
                {
                    BindSelectedOrder(null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải danh sách đơn hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<OrderViewModel> query = _allOrders;

            var keyword = txtOrderSearch?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(o =>
                    o.OrderCode.ToLower().Contains(keyword) ||
                    o.ItemsSummary.ToLower().Contains(keyword) ||
                    o.ShippingAddress.ToLower().Contains(keyword) ||
                    o.RecipientPhone.ToLower().Contains(keyword));
            }

            if (cbOrderStatusFilter?.SelectedItem is ComboBoxItem selected)
            {
                var filter = selected.Content?.ToString();
                switch (filter)
                {
                    case "Đơn Mua Sách":
                        query = query.Where(o => !o.IsRentalOrder);
                        break;
                    case "Đơn Thuê Sách":
                        query = query.Where(o => o.IsRentalOrder);
                        break;
                    case "Đang thuê sách":
                        query = query.Where(o => o.CanReturnBook);
                        break;
                    case "Chờ xác nhận":
                        query = query.Where(o => o.CanCancel);
                        break;
                    case "Đã trả sách":
                        query = query.Where(o => string.Equals(o.OrderStatus, "Returned", StringComparison.OrdinalIgnoreCase) || string.Equals(o.OrderStatus, "Đã trả", StringComparison.OrdinalIgnoreCase));
                        break;
                    case "Hoàn thành":
                        query = query.Where(o => string.Equals(o.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase));
                        break;
                    case "Đã hủy":
                        query = query.Where(o => string.Equals(o.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase));
                        break;
                }
            }

            dgCustomerOrders.ItemsSource = query.ToList();
        }

        private void txtOrderSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            ApplyFilters();
        }

        private void cbOrderStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            ApplyFilters();
        }

        private void btnRefreshOrders_Click(object sender, RoutedEventArgs e)
        {
            LoadOrdersFromDatabase();
        }

        private void dgCustomerOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BindSelectedOrder(dgCustomerOrders.SelectedItem as OrderViewModel);
        }

        private void BindSelectedOrder(OrderViewModel? order)
        {
            _selectedOrder = order;
            if (order == null)
            {
                txtDetailOrderHeader.Text = "🧾 Chi Tiết Đơn Hàng";
                txtDetailStatusText.Text = "Trạng thái: Chưa chọn đơn";
                txtDetailCreatedAt.Text = "Ngày tạo: -";
                txtDetailPhone.Text = "SĐT nhận: -";
                txtDetailAddress.Text = "Địa chỉ / Kỳ hạn thuê: -";
                txtDetailPayment.Text = "Thanh toán: -";
                txtDetailTotalAmount.Text = "0 đ";
                dgDetailOrderItems.ItemsSource = null;

                btnDetailExtend7Days.IsEnabled = false;
                btnDetailReturnBook.IsEnabled = false;
                btnDetailReorder.IsEnabled = false;
                btnDetailExportInvoice.IsEnabled = false;
                btnDetailCancelOrder.IsEnabled = false;
                return;
            }

            txtDetailOrderHeader.Text = $"🧾 Đơn Hàng {order.OrderCode}";
            txtDetailStatusText.Text = string.IsNullOrWhiteSpace(order.CancelReason)
                ? $"{order.OrderTypeDisplay} • Trạng thái: {order.StatusDisplay}"
                : $"{order.OrderTypeDisplay} • Trạng thái: {order.StatusDisplay}\nLý do hủy: {order.CancelReason}";
            txtDetailCreatedAt.Text = $"Ngày đặt: {order.FormattedCreatedAt}";
            txtDetailPhone.Text = $"SĐT nhận: {order.RecipientPhone}";
            txtDetailAddress.Text = $"Địa chỉ / Kỳ hạn: {order.ShippingAddress}";
            var cleanPaymentMethod = (order.PaymentMethod ?? string.Empty)
                .Replace(" (Có sách thuê)", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            txtDetailPayment.Text = $"Thanh toán: {cleanPaymentMethod} - {order.PaymentStatusDisplay}";
            txtDetailTotalAmount.Text = order.FormattedTotalAmount;
            dgDetailOrderItems.ItemsSource = order.Items;

            btnDetailExtend7Days.IsEnabled = order.CanReturnBook;
            btnDetailReturnBook.IsEnabled = order.CanReturnBook;
            btnDetailReorder.IsEnabled = true;
            btnDetailExportInvoice.IsEnabled = true;
            btnDetailCancelOrder.IsEnabled = order.CanCancel;
        }

        // ==================== ORDER ACTIONS ====================
        private void btnDetailExtend7Days_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null || !_selectedOrder.CanReturnBook) return;

            decimal fee = 20000m;
            if (MessageBox.Show(
                $"Bạn muốn gia hạn đơn thuê {_selectedOrder.OrderCode} thêm 7 ngày (Phí gia hạn: {fee:N0} đ)?",
                "Gia hạn thuê sách",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var dbOrder = context.Orders.FirstOrDefault(o => o.OrderId == _selectedOrder.OrderId);
                if (dbOrder == null) return;

                dbOrder.TotalAmount += fee;
                dbOrder.ShippingAddress = $"{dbOrder.ShippingAddress} [Gia hạn +7 ngày đến {DateTime.Now.AddDays(7):dd/MM/yyyy}]";
                context.SaveChanges();

                LoadOrdersFromDatabase();
                MessageBox.Show($"⏳ Đã gia hạn đơn thuê {_selectedOrder.OrderCode} thêm 7 ngày thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gia hạn thuê:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDetailReturnBook_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null || !_selectedOrder.CanReturnBook) return;

            if (MessageBox.Show(
                $"Xác nhận hoàn trả sách cho đơn thuê {_selectedOrder.OrderCode}?",
                "Hoàn trả sách thuê",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var dbOrder = context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefault(o => o.OrderId == _selectedOrder.OrderId);

                if (dbOrder == null) return;

                dbOrder.OrderStatus = "ReturnPending";
                context.SaveChanges();
                LoadOrdersFromDatabase();
                MessageBox.Show($"⏳ Đã gửi yêu cầu trả sách cho đơn {_selectedOrder.OrderCode}.\nTrạng thái hiện tại: Chờ xác nhận trả. Admin sẽ kiểm tra và xác nhận hoàn kho!", "Chờ xác nhận trả sách", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi hoàn trả sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDetailReorder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null || SessionManager.CurrentUser == null) return;

            try
            {
                using var context = new BookStoreDbContext();
                int userId = SessionManager.CurrentUser.UserId;

                var cart = context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefault(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new Cart
                    {
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Carts.Add(cart);
                    context.SaveChanges();
                }

                int addedCount = 0;
                foreach (var item in _selectedOrder.Items)
                {
                    var dbBook = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                    if (dbBook == null || dbBook.StockQuantity <= 0) continue;

                    var existing = cart.CartItems.FirstOrDefault(ci => ci.BookId == item.BookId);
                    if (existing != null)
                    {
                        existing.Quantity = Math.Min(dbBook.StockQuantity, existing.Quantity + item.Quantity);
                    }
                    else
                    {
                        context.CartItems.Add(new CartItem
                        {
                            CartId = cart.CartId,
                            BookId = item.BookId,
                            Quantity = Math.Min(dbBook.StockQuantity, item.Quantity),
                            AddedAt = DateTime.UtcNow
                        });
                    }
                    addedCount++;
                }

                cart.UpdatedAt = DateTime.UtcNow;
                context.SaveChanges();

                if (addedCount > 0)
                {
                    var checkoutWin = new CartCheckoutWindow();
                    checkoutWin.Show();
                    Close();
                }
                else
                {
                    MessageBox.Show("Các sách trong đơn hàng này hiện đang tạm hết hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm lại đơn hàng vào giỏ:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDetailExportInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null) return;

            var cleanPaymentMethod = (_selectedOrder.PaymentMethod ?? string.Empty)
                .Replace(" (Có sách thuê)", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            var sb = new StringBuilder();
            sb.AppendLine("================================================");
            sb.AppendLine($"      HÓA ĐƠN G07 BOOKSTORE ({_selectedOrder.OrderCode})");
            sb.AppendLine("================================================");
            sb.AppendLine($"Khách hàng:     {_selectedOrder.CustomerName}");
            sb.AppendLine($"Số điện thoại:  {_selectedOrder.RecipientPhone}");
            sb.AppendLine($"Địa chỉ nhận:   {_selectedOrder.ShippingAddress}");
            sb.AppendLine($"Ngày đặt:       {_selectedOrder.FormattedCreatedAt}");
            sb.AppendLine($"Hình thức:      {_selectedOrder.OrderTypeDisplay} ({_selectedOrder.StatusDisplay})");
            if (!string.IsNullOrWhiteSpace(_selectedOrder.CancelReason))
            {
                sb.AppendLine($"Lý do hủy:      {_selectedOrder.CancelReason}");
            }
            sb.AppendLine($"Thanh toán:     {cleanPaymentMethod} - {_selectedOrder.PaymentStatusDisplay}");
            sb.AppendLine("------------------------------------------------");
            foreach (var item in _selectedOrder.Items)
            {
                sb.AppendLine($"• {item.BookTitle}");
                sb.AppendLine($"  SL: {item.Quantity} x {item.FormattedUnitPrice} = {item.FormattedSubTotal}");
            }
            sb.AppendLine("------------------------------------------------");
            sb.AppendLine($"TỔNG CỘNG:      {_selectedOrder.FormattedTotalAmount}");
            sb.AppendLine("================================================");

            var dlg = new SaveFileDialog
            {
                Title = "Lưu hóa đơn đơn hàng BookStore",
                Filter = "Text Invoice (*.txt)|*.txt",
                FileName = $"HoaDon_ORD{_selectedOrder.OrderId:D4}_{DateTime.Now:yyyyMMdd}.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"💾 Đã xuất hóa đơn đơn hàng {_selectedOrder.OrderCode} ra file:\n{dlg.FileName}", "Xuất hóa đơn thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnDetailCancelOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null || !_selectedOrder.CanCancel) return;

            if (MessageBox.Show(
                $"Bạn có chắc chắn muốn hủy đơn hàng {_selectedOrder.OrderCode} không?",
                "Xác nhận hủy đơn",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            string? reason = PromptCancelReason(_selectedOrder.OrderCode);
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var dbOrder = context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefault(o => o.OrderId == _selectedOrder.OrderId);

                if (dbOrder == null) return;

                dbOrder.OrderStatus = "Cancelled";
                dbOrder.CancelReason = reason.Trim();
                foreach (var item in dbOrder.OrderItems)
                {
                    var book = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                    if (book != null)
                    {
                        book.StockQuantity += item.Quantity;
                    }
                }

                context.SaveChanges();
                LoadOrdersFromDatabase();
                MessageBox.Show($"Đã hủy đơn hàng {_selectedOrder.OrderCode}!\nLý do: {reason.Trim()}", "Đã hủy đơn", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi hủy đơn hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? PromptCancelReason(string orderCode)
        {
            var dlg = new Window
            {
                Title = $"Hủy đơn hàng {orderCode}",
                Width = 420,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = $"Nhập lý do hủy đơn hàng {orderCode} *",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var txtReason = new TextBox
            {
                Height = 84,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            root.Children.Add(txtReason);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            string? result = null;
            var btnCancel = new Button { Content = "Đóng", Width = 85, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
            btnCancel.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };

            var btnConfirm = new Button
            {
                Content = "Xác nhận hủy",
                Width = 120,
                Height = 34,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626")),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0)
            };
            btnConfirm.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(txtReason.Text))
                {
                    MessageBox.Show("Vui lòng nhập lý do hủy đơn hàng!", "Thiếu lý do", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtReason.Focus();
                    return;
                }
                result = txtReason.Text.Trim();
                dlg.DialogResult = true;
                dlg.Close();
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnConfirm);
            root.Children.Add(btnRow);

            dlg.Content = root;
            txtReason.Focus();
            return dlg.ShowDialog() == true ? result : null;
        }

        // ==================== NAVIGATION ====================
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

        private void btnGoCartCheckout_Click(object sender, RoutedEventArgs e)
        {
            var cartWin = new CartCheckoutWindow();
            cartWin.Show();
            Close();
        }

        private void btnGoProfile_Click(object sender, RoutedEventArgs e)
        {
            var profileWin = new UserProfileWindow("Home");
            profileWin.Show();
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

