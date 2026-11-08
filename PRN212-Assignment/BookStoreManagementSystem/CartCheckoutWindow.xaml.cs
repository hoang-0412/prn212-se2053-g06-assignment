using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class CartCheckoutWindow : Window
    {
        private readonly ObservableCollection<CartItemViewModel> _cartItems = new();
        private readonly Dictionary<int, int> _bookStockMap = new();
        private string _appliedVoucher = string.Empty;
        private string _lastInvoiceText = string.Empty;
        private string _lastOrderCode = string.Empty;

        public CartCheckoutWindow()
        {
            InitializeComponent();
            icCartItems.ItemsSource = _cartItems;
            Loaded += CartCheckoutWindow_Loaded;
        }

        private void CartCheckoutWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HomePage.EnsureRichSampleCatalogIfNeeded();
            EnsureUserSession();
            LoadCartFromDatabase();
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

            txtCurrentUserName.Text = SessionManager.DisplayName;
            txtCurrentUserRole.Text = SessionManager.RoleDisplay;

            if (SessionManager.CurrentUser != null)
            {
                txtRecipientName.Text = SessionManager.CurrentUser.FullName;
                txtRecipientPhone.Text = SessionManager.CurrentUser.PhoneNumber ?? string.Empty;
                txtShippingAddress.Text = SessionManager.CurrentUser.Address ?? string.Empty;
            }
        }

        private void LoadCartFromDatabase()
        {
            _cartItems.Clear();
            _bookStockMap.Clear();

            if (SessionManager.CurrentUser == null)
            {
                RecalculateSummary();
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                HomePage.EnsureValidBookCoversInDatabase(context);

                var cart = context.Carts
                    .AsNoTracking()
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Book)
                            .ThenInclude(b => b.BookImages)
                    .FirstOrDefault(c => c.UserId == SessionManager.CurrentUser.UserId);

                if (cart != null)
                {
                    foreach (var ci in cart.CartItems)
                    {
                        if (ci.Book == null) continue;

                        _bookStockMap[ci.BookId] = ci.Book.StockQuantity;
                        bool isRent = string.Equals(ci.Book.BookStatus, "Cho thuê", StringComparison.OrdinalIgnoreCase);
                        decimal rent7Price = Math.Max(10000m, Math.Round(ci.Book.Price * 0.15m / 1000m, 0) * 1000m);

                        var coverBytes = ci.Book.BookImages.FirstOrDefault(i => i.IsCover)?.ImageData
                                         ?? ci.Book.BookImages.FirstOrDefault()?.ImageData;

                        _cartItems.Add(new CartItemViewModel
                        {
                            BookId = ci.BookId,
                            Title = ci.Book.Title,
                            OriginalBookPrice = ci.Book.Price,
                            UnitPrice = isRent ? rent7Price : ci.Book.Price,
                            Quantity = ci.Quantity,
                            OrderType = isRent ? "Thuê" : "Mua",
                            RentalDays = isRent ? 7 : 0,
                            CoverImage = BookCardViewModel.LoadImageSafe(coverBytes)
                        });
                    }
                }

                RecalculateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải giỏ hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SyncCartToDatabase()
        {
            if (SessionManager.CurrentUser == null) return;

            try
            {
                using var context = new BookStoreDbContext();
                var cart = context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefault(c => c.UserId == SessionManager.CurrentUser.UserId);

                if (cart == null)
                {
                    cart = new Cart
                    {
                        UserId = SessionManager.CurrentUser.UserId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Carts.Add(cart);
                    context.SaveChanges();
                }

                context.CartItems.RemoveRange(cart.CartItems);
                context.SaveChanges();

                foreach (var group in _cartItems.GroupBy(i => i.BookId))
                {
                    context.CartItems.Add(new CartItem
                    {
                        CartId = cart.CartId,
                        BookId = group.Key,
                        Quantity = group.Sum(x => x.Quantity),
                        AddedAt = DateTime.UtcNow
                    });
                }

                cart.UpdatedAt = DateTime.UtcNow;
                context.SaveChanges();
            }
            catch
            {
            }
        }

        private void RecalculateSummary()
        {
            int totalQty = _cartItems.Sum(i => i.Quantity);
            txtCartHeaderCount.Text = $"🛒 Giỏ Hàng Của Bạn ({totalQty} sản phẩm)";
            panelEmptyCart.Visibility = _cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            btnPlaceOrder.IsEnabled = _cartItems.Count > 0;

            decimal subtotal = _cartItems.Sum(i => i.SubTotal);
            decimal shipping = _cartItems.Count > 0 ? 25000m : 0m;
            decimal discount = 0m;

            string code = _appliedVoucher.ToUpperInvariant();
            if (subtotal > 0 && !string.IsNullOrEmpty(code))
            {
                switch (code)
                {
                    case "BOOK10":
                        discount = Math.Round(subtotal * 0.10m / 1000m, 0) * 1000m;
                        txtSummaryDiscountLabel.Text = "Giảm giá (BOOK10 - 10%):";
                        break;
                    case "SINHVIEN":
                        discount = Math.Round(subtotal * 0.15m / 1000m, 0) * 1000m;
                        txtSummaryDiscountLabel.Text = "Giảm giá (SINHVIEN - 15%):";
                        break;
                    case "FREESHIP":
                        discount = shipping;
                        txtSummaryDiscountLabel.Text = "Giảm giá (FREESHIP):";
                        break;
                    case "VIP50K":
                        if (subtotal >= 300000m)
                        {
                            discount = 50000m;
                            txtSummaryDiscountLabel.Text = "Giảm giá (VIP50K):";
                        }
                        else
                        {
                            txtSummaryDiscountLabel.Text = "VIP50K (cần đơn ≥ 300K):";
                        }
                        break;
                    default:
                        txtSummaryDiscountLabel.Text = "Giảm giá Voucher:";
                        break;
                }
            }
            else
            {
                txtSummaryDiscountLabel.Text = "Giảm giá Voucher:";
            }

            decimal finalTotal = Math.Max(0m, subtotal + shipping - discount);

            txtSummarySubtotal.Text = $"{subtotal:N0} đ";
            txtSummaryShipping.Text = $"{shipping:N0} đ";
            txtSummaryDiscount.Text = $"-{discount:N0} đ";
            txtSummaryFinalTotal.Text = $"{finalTotal:N0} đ";

            icCartItems.Items.Refresh();
        }

        // ==================== ITEM ACTIONS & MODE SWITCHING ====================
        private void btnSwitchItemToBuy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                item.OrderType = "Mua";
                item.RentalDays = 0;
                item.UnitPrice = item.OriginalBookPrice;
                RecalculateSummary();
            }
        }

        private void btnSwitchItemToRent7_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                item.OrderType = "Thuê";
                item.RentalDays = 7;
                item.UnitPrice = Math.Max(10000m, Math.Round(item.OriginalBookPrice * 0.15m / 1000m, 0) * 1000m);
                RecalculateSummary();
            }
        }

        private void btnSwitchItemToRent14_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                item.OrderType = "Thuê";
                item.RentalDays = 14;
                item.UnitPrice = Math.Max(15000m, Math.Round(item.OriginalBookPrice * 0.25m / 1000m, 0) * 1000m);
                RecalculateSummary();
            }
        }

        private void btnSwitchItemToRent30_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                item.OrderType = "Thuê";
                item.RentalDays = 30;
                item.UnitPrice = Math.Max(25000m, Math.Round(item.OriginalBookPrice * 0.40m / 1000m, 0) * 1000m);
                RecalculateSummary();
            }
        }

        private void btnDecreaseItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    _cartItems.Remove(item);
                }
                SyncCartToDatabase();
                RecalculateSummary();
            }
        }

        private void btnIncreaseItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                int maxStock = _bookStockMap.GetValueOrDefault(item.BookId, 99);
                if (item.Quantity < maxStock)
                {
                    item.Quantity++;
                    SyncCartToDatabase();
                    RecalculateSummary();
                }
                else
                {
                    MessageBox.Show($"Số lượng sách '{item.Title}' đã đạt giới hạn tồn kho ({maxStock} cuốn)!", "Giới hạn tồn kho", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void btnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                _cartItems.Remove(item);
                SyncCartToDatabase();
                RecalculateSummary();
            }
        }

        private void btnClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0) return;
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ sản phẩm trong giỏ hàng?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _cartItems.Clear();
                SyncCartToDatabase();
                RecalculateSummary();
            }
        }

        // ==================== VOUCHER & PAYMENT ====================
        private void btnQuickVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string code)
            {
                txtVoucherCode.Text = code;
                _appliedVoucher = code;
                RecalculateSummary();
            }
        }

        private void btnApplyVoucher_Click(object sender, RoutedEventArgs e)
        {
            var code = txtVoucherCode.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                _appliedVoucher = string.Empty;
                RecalculateSummary();
                return;
            }

            if (code is "BOOK10" or "SINHVIEN" or "FREESHIP" or "VIP50K")
            {
                _appliedVoucher = code;
                RecalculateSummary();
                MessageBox.Show($"🎟️ Đã áp dụng mã khuyến mãi '{code}' thành công!", "Áp mã thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Mã giảm giá không hợp lệ! Vui lòng chọn một trong các mã: BOOK10, SINHVIEN, FREESHIP, VIP50K.", "Mã không tồn tại", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void cbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (borderQrTransferInfo == null) return;
            var method = (cbPaymentMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            borderQrTransferInfo.Visibility = method.Contains("VietQR", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void btnPlaceOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0 || SessionManager.CurrentUser == null) return;

            var recipient = txtRecipientName.Text.Trim();
            var phone = txtRecipientPhone.Text.Trim();
            var address = txtShippingAddress.Text.Trim();
            var note = txtOrderNote.Text.Trim();

            if (string.IsNullOrEmpty(recipient) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(address))
            {
                MessageBox.Show("Vui lòng điền đầy đủ Họ tên người nhận, Số điện thoại và Địa chỉ nhận sách!", "Thiếu thông tin giao hàng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                int userId = SessionManager.CurrentUser.UserId;

                // Kiểm tra tồn kho
                foreach (var item in _cartItems)
                {
                    var dbBook = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                    if (dbBook == null || dbBook.StockQuantity < item.Quantity)
                    {
                        MessageBox.Show($"Sách '{item.Title}' hiện chỉ còn {dbBook?.StockQuantity ?? 0} cuốn trong kho!", "Không đủ tồn kho", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                decimal subtotal = _cartItems.Sum(i => i.SubTotal);
                decimal shipping = 25000m;
                decimal discount = 0m;
                string code = _appliedVoucher.ToUpperInvariant();
                if (code == "BOOK10") discount = Math.Round(subtotal * 0.10m / 1000m, 0) * 1000m;
                else if (code == "SINHVIEN") discount = Math.Round(subtotal * 0.15m / 1000m, 0) * 1000m;
                else if (code == "FREESHIP") discount = shipping;
                else if (code == "VIP50K" && subtotal >= 300000m) discount = 50000m;

                decimal finalTotal = Math.Max(0m, subtotal + shipping - discount);
                bool hasRental = _cartItems.Any(i => i.OrderType == "Thuê");
                int maxRentalDays = hasRental ? _cartItems.Where(i => i.OrderType == "Thuê").Max(i => i.RentalDays) : 0;

                var paymentMethod = (cbPaymentMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tiền mặt khi nhận hàng (COD)";
                bool isOnlinePaid = !paymentMethod.Contains("COD", StringComparison.OrdinalIgnoreCase);

                string fullShippingAddress = address;
                if (hasRental)
                {
                    fullShippingAddress = $"[THUÊ {maxRentalDays} NGÀY - Hạn trả: {DateTime.Now.AddDays(maxRentalDays):dd/MM/yyyy}] {address}";
                }
                if (!string.IsNullOrEmpty(note))
                {
                    fullShippingAddress += $" (Ghi chú: {note})";
                }

                var newOrder = new Order
                {
                    UserId = userId,
                    TotalAmount = finalTotal,
                    OrderStatus = hasRental ? "Renting" : "Pending",
                    ShippingAddress = fullShippingAddress,
                    RecipientPhone = phone,
                    PaymentMethod = hasRental ? $"{paymentMethod} (Có sách thuê)" : paymentMethod,
                    PaymentStatus = isOnlinePaid ? "Paid" : "Unpaid",
                    CreatedAt = DateTime.UtcNow
                };

                context.Orders.Add(newOrder);
                context.SaveChanges();

                foreach (var item in _cartItems)
                {
                    context.OrderItems.Add(new OrderItem
                    {
                        OrderId = newOrder.OrderId,
                        BookId = item.BookId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });

                    var dbBook = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                    if (dbBook != null)
                    {
                        dbBook.StockQuantity -= item.Quantity;
                        if (dbBook.StockQuantity == 0 && item.OrderType == "Thuê")
                        {
                            dbBook.BookStatus = "Đã thuê";
                        }
                    }
                }

                // Xóa giỏ hàng trong DB
                var dbCart = context.Carts.Include(c => c.CartItems).FirstOrDefault(c => c.UserId == userId);
                if (dbCart != null)
                {
                    context.CartItems.RemoveRange(dbCart.CartItems);
                }

                context.SaveChanges();

                // Tạo nội dung hóa đơn điện tử
                _lastOrderCode = $"ORD-{newOrder.OrderId:D4}";
                var sb = new StringBuilder();
                sb.AppendLine("================================================");
                sb.AppendLine($"        HÓA ĐƠN ĐIỆN TỬ BOOKSTORE (#{_lastOrderCode})");
                sb.AppendLine("================================================");
                sb.AppendLine($"Ngày lập:       {DateTime.Now:dd/MM/yyyy HH:mm}");
                sb.AppendLine($"Khách hàng:     {recipient} ({phone})");
                sb.AppendLine($"Địa chỉ nhận:   {fullShippingAddress}");
                sb.AppendLine($"Thanh toán:     {paymentMethod} ({newOrder.PaymentStatus})");
                sb.AppendLine("------------------------------------------------");
                foreach (var item in _cartItems)
                {
                    sb.AppendLine($"• {item.Title} [{item.TypeBadgeText}]");
                    sb.AppendLine($"  SL: {item.Quantity} x {item.UnitPrice:N0} đ = {item.SubTotal:N0} đ");
                }
                sb.AppendLine("------------------------------------------------");
                sb.AppendLine($"Tạm tính:       {subtotal:N0} đ");
                sb.AppendLine($"Phí vận chuyển: {shipping:N0} đ");
                sb.AppendLine($"Giảm giá:      -{discount:N0} đ");
                sb.AppendLine($"TỔNG CỘNG:      {finalTotal:N0} đ");
                sb.AppendLine("================================================");

                _lastInvoiceText = sb.ToString();
                txtInvoiceOrderCode.Text = $"Mã đơn hàng: #{_lastOrderCode} • Trạng thái: {(hasRental ? "Đang thuê sách" : "Chờ xác nhận")}";
                txtInvoiceContent.Text = _lastInvoiceText;

                _cartItems.Clear();
                _appliedVoucher = string.Empty;
                txtVoucherCode.Text = string.Empty;
                RecalculateSummary();

                gridInvoiceModal.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tạo đơn hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportInvoiceFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastInvoiceText)) return;

            var dlg = new SaveFileDialog
            {
                Title = "Lưu hóa đơn điện tử BookStore",
                Filter = "Text Invoice (*.txt)|*.txt",
                FileName = $"HoaDon_{_lastOrderCode}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, _lastInvoiceText, Encoding.UTF8);
                MessageBox.Show($"💾 Đã xuất hóa đơn ra file thành công:\n{dlg.FileName}", "Xuất hóa đơn", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnCloseInvoiceModal_Click(object sender, RoutedEventArgs e)
        {
            gridInvoiceModal.Visibility = Visibility.Collapsed;
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

        private void btnGoMyOrders_Click(object sender, RoutedEventArgs e)
        {
            var ordersWin = new CustomerOrdersWindow();
            ordersWin.Show();
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

