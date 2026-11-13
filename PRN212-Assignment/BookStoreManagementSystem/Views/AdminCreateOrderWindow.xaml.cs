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
    public class AdminUserSelectOption
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string DisplayLabel => $"#{UserId} - {FullName} ({Email} • {(string.IsNullOrWhiteSpace(PhoneNumber) ? "Chưa có SĐT" : PhoneNumber)})";
        public override string ToString() => DisplayLabel;
    }

    public partial class AdminCreateOrderWindow : Window
    {
        private List<BookCardViewModel> _allBooks = new();
        private List<AdminUserSelectOption> _allUsers = new();
        private List<CartItemViewModel> _selectedItems = new();
        private int? _preselectedUserId;
        private bool _isLoaded = false;

        public bool OrderCreated { get; private set; } = false;
        public int? CreatedOrderId { get; private set; }

        public AdminCreateOrderWindow(int? preselectedUserId = null)
        {
            InitializeComponent();
            _preselectedUserId = preselectedUserId;
            Loaded += AdminCreateOrderWindow_Loaded;
        }

        private void AdminCreateOrderWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPosDataFromDatabase();
            _isLoaded = true;

            if (_preselectedUserId.HasValue)
            {
                rbCustomerExistingUser.IsChecked = true;
                var match = _allUsers.FirstOrDefault(u => u.UserId == _preselectedUserId.Value);
                if (match != null)
                {
                    cbUsers.SelectedItem = match;
                }
            }

            UpdateSummaryUI();
        }

        private void LoadPosDataFromDatabase()
        {
            try
            {
                using var context = new BookStoreDbContext();
                HomePage.EnsureValidBookCoversInDatabase(context);

                var categories = context.Categories.AsNoTracking().OrderBy(c => c.CategoryName).ToList();
                var catFilterList = new List<CategoryFilterItem>
                {
                    new CategoryFilterItem { CategoryId = null, Name = "Tất cả thể loại" }
                };
                catFilterList.AddRange(categories.Select(c => new CategoryFilterItem
                {
                    CategoryId = c.CategoryId,
                    Name = c.CategoryName
                }));
                cbCategoryFilter.ItemsSource = catFilterList;
                cbCategoryFilter.DisplayMemberPath = "Name";
                cbCategoryFilter.SelectedIndex = 0;

                var books = context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .Include(b => b.BookImages)
                    .OrderByDescending(b => b.StockQuantity > 0)
                    .ThenBy(b => b.Title)
                    .ToList();

                _allBooks = books.Select(BookCardViewModel.FromEntity).ToList();
                dgAvailableBooks.ItemsSource = _allBooks;

                var users = context.Users
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .Where(u => u.Email != "walkin@bookstore.local")
                    .OrderBy(u => u.FullName)
                    .ToList();

                _allUsers = users.Select(u => new AdminUserSelectOption
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber ?? "",
                    Address = u.Address ?? "",
                    RoleName = u.Role?.RoleName ?? "Customer"
                }).ToList();

                cbUsers.ItemsSource = _allUsers;
                if (_allUsers.Count > 0)
                {
                    cbUsers.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải dữ liệu kho sách & khách hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== BOOK FILTERING ====================
        private void BookFilter_Changed(object sender, EventArgs e)
        {
            if (!_isLoaded) return;
            IEnumerable<BookCardViewModel> query = _allBooks;

            var keyword = txtBookSearch?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(b =>
                    b.Title.ToLower().Contains(keyword) ||
                    b.Author.ToLower().Contains(keyword) ||
                    (b.Isbn != null && b.Isbn.ToLower().Contains(keyword)));
            }

            if (cbCategoryFilter?.SelectedItem is CategoryFilterItem cat && cat.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == cat.CategoryId.Value);
            }

            if (cbStatusFilter?.SelectedItem is ComboBoxItem statusItem)
            {
                var status = statusItem.Content?.ToString();
                if (!string.IsNullOrEmpty(status) && status != "Tất cả hình thức")
                {
                    query = query.Where(b => string.Equals(b.BookStatus, status, StringComparison.OrdinalIgnoreCase));
                }
            }

            dgAvailableBooks.ItemsSource = query.ToList();
        }

        private int GetDefaultRentalDays()
        {
            if (cbDefaultRentalDays?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int days))
            {
                return days;
            }
            return 7;
        }

        private static decimal CalculateRentalPrice(decimal coverPrice, int rentalDays)
        {
            decimal rate = rentalDays switch
            {
                14 => 0.25m,
                30 => 0.40m,
                _ => 0.15m
            };
            return Math.Max(10000m, Math.Round(coverPrice * rate / 1000m, 0) * 1000m);
        }

        private void cbDefaultRentalDays_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            int days = GetDefaultRentalDays();
            foreach (var item in _selectedItems.Where(x => x.OrderType == "Thuê"))
            {
                item.RentalDays = days;
                item.UnitPrice = CalculateRentalPrice(item.OriginalBookPrice, days);
            }
            RefreshSelectedItemsGrid();
        }

        // ==================== ADD / MODIFY ITEMS IN ORDER ====================
        private void btnAddBuyItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                AddBookToOrder(book, "Mua", 0);
            }
        }

        private void btnAddRentItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                AddBookToOrder(book, "Thuê", GetDefaultRentalDays());
            }
        }

        private void AddBookToOrder(BookCardViewModel book, string orderType, int rentalDays)
        {
            if (book.StockQuantity <= 0)
            {
                MessageBox.Show($"Sách '{book.Title}' hiện đã hết hàng trong kho!", "Hết hàng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int currentBookTotalInOrder = _selectedItems.Where(x => x.BookId == book.BookId).Sum(x => x.Quantity);
            if (currentBookTotalInOrder >= book.StockQuantity)
            {
                MessageBox.Show($"Kho chỉ còn {book.StockQuantity} cuốn '{book.Title}'!", "Vượt quá tồn kho", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal unitPrice = orderType == "Thuê"
                ? CalculateRentalPrice(book.Price, rentalDays)
                : book.Price;

            var existing = _selectedItems.FirstOrDefault(x =>
                x.BookId == book.BookId &&
                x.OrderType == orderType &&
                x.RentalDays == rentalDays);

            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                _selectedItems.Add(new CartItemViewModel
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    OriginalBookPrice = book.Price,
                    UnitPrice = unitPrice,
                    Quantity = 1,
                    OrderType = orderType,
                    RentalDays = orderType == "Thuê" ? rentalDays : 0,
                    CoverImage = book.CoverImage
                });
            }

            RefreshSelectedItemsGrid();
        }

        private void btnCycleItemMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                if (item.OrderType == "Mua")
                {
                    item.OrderType = "Thuê";
                    item.RentalDays = 7;
                    item.UnitPrice = CalculateRentalPrice(item.OriginalBookPrice, 7);
                }
                else if (item.RentalDays == 7)
                {
                    item.RentalDays = 14;
                    item.UnitPrice = CalculateRentalPrice(item.OriginalBookPrice, 14);
                }
                else if (item.RentalDays == 14)
                {
                    item.RentalDays = 30;
                    item.UnitPrice = CalculateRentalPrice(item.OriginalBookPrice, 30);
                }
                else
                {
                    item.OrderType = "Mua";
                    item.RentalDays = 0;
                    item.UnitPrice = item.OriginalBookPrice;
                }

                RefreshSelectedItemsGrid();
            }
        }

        private void btnIncreaseItemQty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                var book = _allBooks.FirstOrDefault(b => b.BookId == item.BookId);
                int maxStock = book?.StockQuantity ?? 99;
                int currentBookTotal = _selectedItems.Where(x => x.BookId == item.BookId).Sum(x => x.Quantity);

                if (currentBookTotal >= maxStock)
                {
                    MessageBox.Show($"Kho chỉ còn tối đa {maxStock} cuốn '{item.Title}'!", "Giới hạn tồn kho", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Quantity++;
                RefreshSelectedItemsGrid();
            }
        }

        private void btnDecreaseItemQty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    _selectedItems.Remove(item);
                }
                RefreshSelectedItemsGrid();
            }
        }

        private void btnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                _selectedItems.Remove(item);
                RefreshSelectedItemsGrid();
            }
        }

        private void btnClearItems_Click(object sender, RoutedEventArgs e)
        {
            _selectedItems.Clear();
            RefreshSelectedItemsGrid();
        }

        private void RefreshSelectedItemsGrid()
        {
            dgSelectedItems.ItemsSource = null;
            dgSelectedItems.ItemsSource = _selectedItems;
            UpdateSummaryUI();
        }

        // ==================== CUSTOMER MODE: WALK-IN GUEST VS EXISTING USER ====================
        private void CustomerMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            bool isExistingUser = rbCustomerExistingUser.IsChecked == true;
            panelSelectExistingUser.Visibility = isExistingUser ? Visibility.Visible : Visibility.Collapsed;

            if (isExistingUser)
            {
                ApplySelectedUserToFields();
            }
            else
            {
                txtRecipientName.Text = "Khách vãng lai (Tại quầy)";
                txtRecipientPhone.Text = "Tại quầy";
                txtShippingAddress.Text = "Nhận sách trực tiếp tại quầy BookStore";
            }
        }

        private void txtUserSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var keyword = txtUserSearch.Text.Trim().ToLower();
            var filtered = string.IsNullOrEmpty(keyword)
                ? _allUsers
                : _allUsers.Where(u =>
                    u.FullName.ToLower().Contains(keyword) ||
                    u.Email.ToLower().Contains(keyword) ||
                    u.PhoneNumber.ToLower().Contains(keyword)).ToList();

            cbUsers.ItemsSource = filtered;
            if (filtered.Count > 0)
            {
                cbUsers.SelectedIndex = 0;
            }
        }

        private void cbUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (rbCustomerExistingUser.IsChecked == true)
            {
                ApplySelectedUserToFields();
            }
        }

        private void ApplySelectedUserToFields()
        {
            if (cbUsers.SelectedItem is AdminUserSelectOption user)
            {
                txtRecipientName.Text = user.FullName;
                txtRecipientPhone.Text = string.IsNullOrWhiteSpace(user.PhoneNumber) ? "0900000000" : user.PhoneNumber;
                txtShippingAddress.Text = string.IsNullOrWhiteSpace(user.Address) ? "Nhận tại cửa hàng BookStore" : user.Address;
            }
        }

        // ==================== PAYMENT & SUMMARY CALCULATION ====================
        private void cbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || cbPaymentStatus == null) return;
            var methodTag = (cbPaymentMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Cash";
            cbPaymentStatus.SelectedIndex = string.Equals(methodTag, "COD", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private void cbDiscountPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            UpdateSummaryUI();
        }

        private (decimal subTotal, decimal discount, decimal finalTotal) CalculateTotals()
        {
            decimal subTotal = _selectedItems.Sum(x => x.SubTotal);
            decimal discount = 0m;

            var preset = (cbDiscountPreset?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "NONE";
            switch (preset)
            {
                case "PCT_5":
                    discount = Math.Round(subTotal * 0.05m / 1000m, 0) * 1000m;
                    break;
                case "PCT_10":
                    discount = Math.Round(subTotal * 0.10m / 1000m, 0) * 1000m;
                    break;
                case "PCT_15":
                    discount = Math.Round(subTotal * 0.15m / 1000m, 0) * 1000m;
                    break;
                case "FIX_20000":
                    discount = 20000m;
                    break;
                case "FIX_50000":
                    discount = 50000m;
                    break;
            }

            if (discount > subTotal) discount = subTotal;
            decimal finalTotal = Math.Max(0m, subTotal - discount);
            return (subTotal, discount, finalTotal);
        }

        private void UpdateSummaryUI()
        {
            if (txtSelectedItemsBadge == null) return;

            int totalQty = _selectedItems.Sum(x => x.Quantity);
            int buyQty = _selectedItems.Where(x => x.OrderType == "Mua").Sum(x => x.Quantity);
            int rentQty = _selectedItems.Where(x => x.OrderType == "Thuê").Sum(x => x.Quantity);

            txtSelectedItemsBadge.Text = $"{totalQty} cuốn ({_selectedItems.Count} dòng)";
            txtSummaryTotalQty.Text = $"{totalQty} cuốn ({buyQty} Mua • {rentQty} Thuê)";

            var (subTotal, discount, finalTotal) = CalculateTotals();
            txtSummarySubTotal.Text = $"{subTotal:N0} đ";
            txtSummaryDiscount.Text = $"-{discount:N0} đ";
            txtSummaryFinalTotal.Text = $"{finalTotal:N0} đ";
        }

        private int EnsureWalkInCustomerUserId(BookStoreDbContext context)
        {
            var walkInUser = context.Users.FirstOrDefault(u => u.Email == "walkin@bookstore.local");
            if (walkInUser != null)
            {
                return walkInUser.UserId;
            }

            var customerRole = context.Roles.FirstOrDefault(r => r.RoleName == "Customer")
                               ?? context.Roles.FirstOrDefault();
            int roleId = customerRole?.RoleId ?? 1;

            walkInUser = new User
            {
                RoleId = roleId,
                FullName = "Khách Vãng Lai (Tại Quầy)",
                Email = "walkin@bookstore.local",
                PasswordHash = "walkin_disabled_login",
                PhoneNumber = "Tại quầy",
                Address = "Mua/Thuê trực tiếp tại quầy BookStore",
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(walkInUser);
            context.SaveChanges();
            return walkInUser.UserId;
        }

        private void btnCreateOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 quyển sách (Mua hoặc Thuê) vào đơn hàng!", "Chưa chọn sách", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isExistingUser = rbCustomerExistingUser.IsChecked == true;
            var selectedUserOption = cbUsers.SelectedItem as AdminUserSelectOption;

            if (isExistingUser && selectedUserOption == null)
            {
                MessageBox.Show("Vui lòng chọn một tài khoản User từ danh sách hoặc chuyển sang chế độ 'Khách vãng lai (Không chọn tài khoản)'!", "Chưa chọn tài khoản", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string customerName = string.IsNullOrWhiteSpace(txtRecipientName.Text)
                ? (isExistingUser && selectedUserOption != null ? selectedUserOption.FullName : "Khách vãng lai (Tại quầy)")
                : txtRecipientName.Text.Trim();

            string phone = string.IsNullOrWhiteSpace(txtRecipientPhone.Text)
                ? "Tại quầy"
                : txtRecipientPhone.Text.Trim();

            string rawAddress = string.IsNullOrWhiteSpace(txtShippingAddress.Text)
                ? "Nhận sách trực tiếp tại quầy BookStore"
                : txtShippingAddress.Text.Trim();

            string paymentMethod = (cbPaymentMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tiền mặt tại quầy (Cash)";
            string paymentStatus = (cbPaymentStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Paid";
            string selectedStatusTag = (cbOrderStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";

            bool hasRentalItems = _selectedItems.Any(x => x.OrderType == "Thuê");
            int maxRentDays = _selectedItems.Where(x => x.OrderType == "Thuê").Select(x => x.RentalDays).DefaultIfEmpty(0).Max();

            string orderStatus = selectedStatusTag == "Auto"
                ? (hasRentalItems ? "Renting" : (paymentStatus == "Paid" ? "Completed" : "Pending"))
                : selectedStatusTag;

            var (subTotal, discount, finalTotal) = CalculateTotals();

            var confirm = MessageBox.Show(
                $"Xác nhận tạo đơn hàng mới cho Admin?\n\n" +
                $"• Khách hàng: {customerName} {(isExistingUser ? $"(User #{selectedUserOption!.UserId})" : "(Khách vãng lai - Không dùng TK)")}\n" +
                $"• SĐT: {phone}\n" +
                $"• Số lượng: {_selectedItems.Sum(x => x.Quantity)} cuốn\n" +
                $"• Phương thức TT: {paymentMethod} ({paymentStatus})\n" +
                $"• Trạng thái đơn: {orderStatus}\n" +
                $"• Tổng thanh toán: {finalTotal:N0} đ",
                "Xác nhận tạo đơn Mua / Thuê",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var context = new BookStoreDbContext();
                int targetUserId = isExistingUser && selectedUserOption != null
                    ? selectedUserOption.UserId
                    : EnsureWalkInCustomerUserId(context);

                var addressParts = new List<string>();
                if (!isExistingUser)
                {
                    addressParts.Add($"[Khách lẻ: {customerName}]");
                }
                if (hasRentalItems)
                {
                    addressParts.Add($"[THUÊ {maxRentDays} NGÀY - Hạn trả: {DateTime.Now.AddDays(maxRentDays):dd/MM/yyyy}]");
                }
                if (discount > 0)
                {
                    addressParts.Add($"[Giảm: {discount:N0}đ]");
                }
                addressParts.Add(rawAddress);

                string finalShippingAddress = string.Join(" ", addressParts);
                if (finalShippingAddress.Length > 250)
                {
                    finalShippingAddress = finalShippingAddress.Substring(0, 250);
                }

                var newOrder = new Order
                {
                    UserId = targetUserId,
                    TotalAmount = finalTotal,
                    OrderStatus = orderStatus,
                    ShippingAddress = finalShippingAddress,
                    RecipientPhone = phone.Length > 20 ? phone.Substring(0, 20) : phone,
                    PaymentMethod = paymentMethod.Length > 50 ? paymentMethod.Substring(0, 50) : paymentMethod,
                    PaymentStatus = paymentStatus,
                    CreatedAt = DateTime.UtcNow
                };

                context.Orders.Add(newOrder);
                context.SaveChanges();

                foreach (var item in _selectedItems)
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
                        dbBook.StockQuantity = Math.Max(0, dbBook.StockQuantity - item.Quantity);
                    }
                }

                context.SaveChanges();

                OrderCreated = true;
                CreatedOrderId = newOrder.OrderId;

                var printConfirm = MessageBox.Show(
                    $"🎉 Đã tạo đơn hàng #ORD-{newOrder.OrderId:D4} thành công!\n\n" +
                    $"• Khách hàng: {customerName}\n" +
                    $"• Tổng tiền: {finalTotal:N0} đ ({paymentMethod} - {paymentStatus})\n" +
                    $"• Trạng thái: {orderStatus}\n\n" +
                    $"Bạn có muốn xuất hóa đơn (.txt) cho đơn hàng này ngay không?",
                    "Tạo đơn thành công",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (printConfirm == MessageBoxResult.Yes)
                {
                    ExportOrderInvoiceTxt(newOrder.OrderId, customerName, phone, finalShippingAddress, paymentMethod, paymentStatus, orderStatus, subTotal, discount, finalTotal);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu đơn hàng mới:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportOrderInvoiceTxt(int orderId, string customerName, string phone, string address, string paymentMethod, string paymentStatus, string orderStatus, decimal subTotal, decimal discount, decimal finalTotal)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Lưu Hóa Đơn Mua / Thuê Sách (Admin POS)",
                Filter = "Text Invoice (*.txt)|*.txt",
                FileName = $"HoaDon_ORD_{orderId:D4}_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("============================================================");
                sb.AppendLine("              HIỆU SÁCH TRỰC TUYẾN BOOKSTORE                ");
                sb.AppendLine("           HÓA ĐƠN BÁN HÀNG & CHO THUÊ SÁCH (POS)           ");
                sb.AppendLine("============================================================");
                sb.AppendLine($"Mã hóa đơn:       #ORD-{orderId:D4}");
                sb.AppendLine($"Ngày lập đơn:     {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine($"Người lập đơn:    {SessionManager.DisplayName} (Admin)");
                sb.AppendLine($"Khách hàng:       {customerName}");
                sb.AppendLine($"Số điện thoại:    {phone}");
                sb.AppendLine($"Địa chỉ / Ghi chú:{address}");
                sb.AppendLine($"Phương thức TT:   {paymentMethod} ({paymentStatus})");
                sb.AppendLine($"Trạng thái đơn:   {orderStatus}");
                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine("DANH SÁCH SÁCH TRONG ĐƠN:");
                int idx = 1;
                foreach (var item in _selectedItems)
                {
                    sb.AppendLine($"{idx++}. {item.Title}");
                    sb.AppendLine($"   Hình thức: {item.TypeBadgeText} | SL: {item.Quantity} x {item.UnitPrice:N0} đ = {item.SubTotal:N0} đ");
                }
                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine($"Tạm tính:         {subTotal:N0} đ");
                sb.AppendLine($"Giảm giá:        -{discount:N0} đ");
                sb.AppendLine($"TỔNG THANH TOÁN:  {finalTotal:N0} đ");
                sb.AppendLine("============================================================");
                sb.AppendLine("        Cảm ơn quý khách đã mua & thuê sách tại BookStore!   ");

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Đã lưu hóa đơn tại:\n{dlg.FileName}", "Xuất hóa đơn", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnResetAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedItems.Clear();
            txtBookSearch.Text = string.Empty;
            if (cbCategoryFilter.Items.Count > 0) cbCategoryFilter.SelectedIndex = 0;
            if (cbStatusFilter.Items.Count > 0) cbStatusFilter.SelectedIndex = 0;
            rbCustomerWalkIn.IsChecked = true;
            txtRecipientName.Text = "Khách vãng lai (Tại quầy)";
            txtRecipientPhone.Text = "Tại quầy";
            txtShippingAddress.Text = "Nhận sách trực tiếp tại quầy BookStore";
            cbPaymentMethod.SelectedIndex = 0;
            cbPaymentStatus.SelectedIndex = 0;
            cbOrderStatus.SelectedIndex = 0;
            cbDiscountPreset.SelectedIndex = 0;
            RefreshSelectedItemsGrid();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
