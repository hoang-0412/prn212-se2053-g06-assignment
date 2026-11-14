using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BookStoreManagementSystem.Models;
using BookStoreManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace BookStoreManagementSystem
{
    public partial class AdminDashboardWindow : Window
    {
        private List<BookCardViewModel> _allBooks = new();
        private List<CategoryFilterItem> _allCategories = new();
        private List<OrderViewModel> _allOrders = new();
        private List<UserAdminViewModel> _allUsers = new();
        private List<VoucherAdminViewModel> _allVouchers = new();

        private BookCardViewModel? _selectedBook;
        private CategoryFilterItem? _selectedCategory;
        private OrderViewModel? _selectedOrder;
        private UserAdminViewModel? _selectedUser;
        private VoucherAdminViewModel? _selectedVoucher;

        private byte[]? _pendingBookImageData;
        private string? _pendingBookImageMimeType;
        private bool _removeBookImageRequested = false;
        private bool _isLoaded = false;

        public AdminDashboardWindow()
        {
            InitializeComponent();
            Loaded += AdminDashboardWindow_Loaded;
        }

        private void AdminDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HomePage.EnsureRichSampleCatalogIfNeeded();
            SetActiveTab("Overview");
            LoadAllDashboardData();
        }

        private void SetActiveTab(string tabName)
        {
            viewOverview.Visibility = tabName == "Overview" ? Visibility.Visible : Visibility.Collapsed;
            viewBooks.Visibility = tabName == "Books" ? Visibility.Visible : Visibility.Collapsed;
            viewCategories.Visibility = tabName == "Categories" ? Visibility.Visible : Visibility.Collapsed;
            viewOrders.Visibility = tabName == "Orders" ? Visibility.Visible : Visibility.Collapsed;
            viewUsers.Visibility = tabName == "Users" ? Visibility.Visible : Visibility.Collapsed;
            viewVouchers.Visibility = tabName == "Vouchers" ? Visibility.Visible : Visibility.Collapsed;

            var activeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
            var inactiveBrush = Brushes.Transparent;

            navOverview.Background = tabName == "Overview" ? activeBrush : inactiveBrush;
            navBooks.Background = tabName == "Books" ? activeBrush : inactiveBrush;
            navCategories.Background = tabName == "Categories" ? activeBrush : inactiveBrush;
            navOrders.Background = tabName == "Orders" ? activeBrush : inactiveBrush;
            navUsers.Background = tabName == "Users" ? activeBrush : inactiveBrush;
            navVouchers.Background = tabName == "Vouchers" ? activeBrush : inactiveBrush;

            switch (tabName)
            {
                case "Overview":
                    txtPageTitle.Text = "Tổng Quan Hệ Thống (Admin Dashboard)";
                    txtPageSubtitle.Text = "Theo dõi doanh thu, sách bán, sách cho thuê và cảnh báo tồn kho theo thời gian thực";
                    break;
                case "Books":
                    txtPageTitle.Text = "Quản Lý Kho Sách (Mua Bán & Cho Thuê)";
                    txtPageSubtitle.Text = "Thêm mới, chỉnh sửa thông tin sách, tải ảnh bìa và chuyển đổi hình thức Bán / Cho thuê";
                    break;
                case "Categories":
                    txtPageTitle.Text = "Quản Lý Thể Loại Sách";
                    txtPageSubtitle.Text = "Tạo mới, cập nhật hoặc xóa danh mục thể loại sách trong hiệu sách";
                    break;
                case "Orders":
                    txtPageTitle.Text = "Quản Lý Đơn Mua & Đơn Thuê Sách";
                    txtPageSubtitle.Text = "Duyệt đơn hàng, cập nhật thanh toán và xác nhận khách hoàn trả sách thuê";
                    break;
                case "Users":
                    txtPageTitle.Text = "Quản Lý Tài Khoản & Phân Quyền";
                    txtPageSubtitle.Text = "Quản trị danh sách khách hàng và quản trị viên hệ thống";
                    break;
                case "Vouchers":
                    txtPageTitle.Text = "Quản Lý Voucher";
                    txtPageSubtitle.Text = "Tạo mã giảm giá, giới hạn lượt lưu và số lần sử dụng theo người dùng";
                    break;
            }
        }

        private void navOverview_Click(object sender, RoutedEventArgs e) => SetActiveTab("Overview");
        private void navBooks_Click(object sender, RoutedEventArgs e) => SetActiveTab("Books");
        private void navCategories_Click(object sender, RoutedEventArgs e) => SetActiveTab("Categories");
        private void navOrders_Click(object sender, RoutedEventArgs e) => SetActiveTab("Orders");
        private void navUsers_Click(object sender, RoutedEventArgs e) => SetActiveTab("Users");
        private void navVouchers_Click(object sender, RoutedEventArgs e) => SetActiveTab("Vouchers");

        public void LoadAllDashboardData()
        {
            try
            {
                using var context = new BookStoreDbContext();

                // 1. Categories
                var categories = context.Categories
                    .AsNoTracking()
                    .Include(c => c.Books)
                    .OrderBy(c => c.CategoryName)
                    .ToList();

                _allCategories = categories.Select(c => new CategoryFilterItem
                {
                    CategoryId = c.CategoryId,
                    Name = c.CategoryName,
                    Description = c.Description,
                    Count = c.Books.Count
                }).ToList();

                dgAdminCategories.ItemsSource = _allCategories;
                cbBookCategory.ItemsSource = categories;

                var catFilterList = new List<CategoryFilterItem>
                {
                    new CategoryFilterItem { CategoryId = null, Name = "Tất cả thể loại" }
                };
                catFilterList.AddRange(_allCategories);
                cbAdminBookCategoryFilter.ItemsSource = catFilterList;
                cbAdminBookCategoryFilter.DisplayMemberPath = "Name";
                cbAdminBookCategoryFilter.SelectedIndex = 0;

                // 2. Books
                HomePage.EnsureValidBookCoversInDatabase(context);
                var books = context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .Include(b => b.BookImages)
                    .OrderByDescending(b => b.BookId)
                    .ToList();

                _allBooks = books.Select(b =>
                {
                    var coverImgData = b.BookImages.FirstOrDefault(i => i.IsCover)?.ImageData
                                       ?? b.BookImages.FirstOrDefault()?.ImageData;
                    return new BookCardViewModel
                    {
                        BookId = b.BookId,
                        CategoryId = b.CategoryId,
                        Title = b.Title,
                        Author = b.Author,
                        CategoryName = b.Category?.CategoryName ?? "Chưa phân loại",
                        Publisher = b.Publisher,
                        PublicationYear = b.PublicationYear,
                        Isbn = b.Isbn,
                        Price = b.Price,
                        StockQuantity = b.StockQuantity,
                        Description = b.Description,
                        BookStatus = string.IsNullOrWhiteSpace(b.BookStatus) ? "Bán" : b.BookStatus,
                        RawCoverImage = coverImgData,
                        CoverImage = BookCardViewModel.LoadImageSafe(coverImgData),
                        CreatedAt = b.CreatedAt
                    };
                }).ToList();

                _allBooks = books.Select(BookCardViewModel.FromEntity).ToList();
                // 3. Orders
                var orders = context.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Book)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToList();

                _allOrders = orders.Select(o =>
                {
                    string custName = o.User?.FullName ?? $"User #{o.UserId}";
                    string custEmail = o.User?.Email ?? "";

                    if (!string.IsNullOrEmpty(o.ShippingAddress) && o.ShippingAddress.StartsWith("[Khách lẻ:"))
                    {
                        int closeBracket = o.ShippingAddress.IndexOf(']');
                        if (closeBracket > 10)
                        {
                            custName = o.ShippingAddress.Substring(10, closeBracket - 10).Trim();
                            custEmail = "Khách vãng lai (Không TK)";
                        }
                    }
                    else if (string.Equals(custEmail, "walkin@bookstore.local", StringComparison.OrdinalIgnoreCase))
                    {
                        custEmail = "Khách vãng lai (Không TK)";
                    }

                    return new OrderViewModel
                    {
                        OrderId = o.OrderId,
                        UserId = o.UserId,
                        CustomerName = custName,
                        CustomerEmail = custEmail,
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
                    };
                }).ToList();

                // 4. Users & Roles
                var roles = context.Roles.AsNoTracking().OrderBy(r => r.RoleId).ToList();
                cbAdminUserRole.ItemsSource = roles;

                var users = context.Users
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .Include(u => u.Orders)
                    .Where(u => u.Email != "walkin@bookstore.local")
                    .OrderByDescending(u => u.UserId)
                    .ToList();

                _allUsers = users.Select(u => new UserAdminViewModel
                {
                    UserId = u.UserId,
                    RoleId = u.RoleId,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Address = u.Address,
                    RoleName = u.Role?.RoleName ?? "Customer",
                    UserStatus = string.IsNullOrWhiteSpace(u.UserStatus) ? "active" : u.UserStatus,
                    BanReason = u.BanReason,
                    OrderCount = u.Orders.Count,
                    ActiveRentalCount = u.Orders.Count(o => string.Equals(o.OrderStatus, "Renting", StringComparison.OrdinalIgnoreCase) || string.Equals(o.OrderStatus, "Đang thuê", StringComparison.OrdinalIgnoreCase) || string.Equals(o.OrderStatus, "Overdue", StringComparison.OrdinalIgnoreCase)),
                    TotalSpent = u.Orders.Where(o => !string.Equals(o.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)).Sum(o => o.TotalAmount),
                    CreatedAt = u.CreatedAt
                }).ToList();

                dgAdminUsers.ItemsSource = _allUsers;
                LoadVouchers(context);

                _isLoaded = true;
                UpdateOverviewTab(categories);
                ApplyBookFilters();
                ApplyOrderFilters();
                ApplyUserFilters();
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải dữ liệu Admin Dashboard:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadVouchers(BookStoreDbContext context)
        {
            _allVouchers = context.Vouchers
                .AsNoTracking()
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new VoucherAdminViewModel
                {
                    VoucherId = v.VoucherId,
                    Name = v.Name,
                    Code = v.Code,
                    DiscountType = v.DiscountType,
                    DiscountValue = v.DiscountValue,
                    StartAt = v.StartAt,
                    ExpiryAt = v.ExpiryAt,
                    MaxUsagePerUser = v.MaxUsagePerUser,
                    TotalQuantity = v.TotalQuantity,
                    SavedCount = v.SavedCount,
                    UsedCount = v.UsedCount,
                    IsActive = v.IsActive
                }).ToList();

            dgAdminVouchers.ItemsSource = _allVouchers;
        }

        private void UpdateOverviewTab(List<Category> categories)
        {
            var validOrders = _allOrders.Where(o => !string.Equals(o.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)).ToList();
            var totalRevenue = validOrders.Sum(o => o.TotalAmount);
            var rentalOrdersCount = _allOrders.Count(o => o.IsRentalOrder);
            var purchaseOrdersCount = _allOrders.Count - rentalOrdersCount;

            kpiTotalRevenue.Text = $"{totalRevenue:N0} đ";
            kpiRevenueSub.Text = $"{validOrders.Count} đơn hợp lệ ({purchaseOrdersCount} mua, {rentalOrdersCount} thuê)";

            var totalStock = _allBooks.Sum(b => b.StockQuantity);
            kpiTotalBooks.Text = $"{_allBooks.Count} đầu sách";
            kpiBookSub.Text = $"Tổng tồn kho: {totalStock} cuốn ({_allCategories.Count} thể loại)";

            var forRentCount = _allBooks.Count(b => b.IsForRent);
            var forSaleCount = _allBooks.Count(b => b.IsForSale);
            kpiRentalBooks.Text = $"{forRentCount} sách thuê";
            kpiRentalSub.Text = $"Sách cho thuê: {forRentCount} | Sách bán: {forSaleCount}";

            kpiTotalOrders.Text = $"{_allOrders.Count} đơn hàng";
            kpiOrderSub.Text = $"{_allUsers.Count} tài khoản trên hệ thống";

            // Category Progress Stats
            int maxBooks = Math.Max(1, _allBooks.Count);
            var catStats = categories.Select(c => new CategoryStatViewModel
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName,
                Description = c.Description ?? "",
                BookCount = c.Books.Count,
                TotalStock = c.Books.Sum(b => b.StockQuantity),
                Percentage = Math.Round((double)c.Books.Count * 100.0 / maxBooks, 1)
            }).ToList();
            icCategoryStats.ItemsSource = catStats;

            // Recent Orders
            dgRecentOrders.ItemsSource = _allOrders.Take(8).ToList();
        }

        // ==================== VOUCHER CRUD ====================
        private void dgAdminVouchers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgAdminVouchers.SelectedItem is not VoucherAdminViewModel voucher) return;

            _selectedVoucher = voucher;
            txtVoucherName.Text = voucher.Name;
            txtVoucherCode.Text = voucher.Code;
            txtVoucherDiscountValue.Text = voucher.DiscountValue.ToString("0.##");
            txtVoucherMaxUsage.Text = voucher.MaxUsagePerUser.ToString();
            txtVoucherTotalQuantity.Text = voucher.TotalQuantity.ToString();
            dpVoucherStart.SelectedDate = voucher.StartAt.ToLocalTime().Date;
            dpVoucherExpiry.SelectedDate = voucher.ExpiryAt.ToLocalTime().Date;
            chkVoucherActive.IsChecked = voucher.IsActive;
            cbVoucherDiscountType.SelectedIndex = voucher.DiscountType == "Amount" ? 1 : 0;
        }

        private void btnRefreshVouchers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new BookStoreDbContext();
                LoadVouchers(context);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải voucher:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryReadVoucherForm(out string name, out string code, out string discountType, out decimal discountValue,
            out DateTime startAt, out DateTime expiryAt, out int maxUsage, out int totalQuantity, out bool isActive)
        {
            name = txtVoucherName.Text.Trim();
            code = txtVoucherCode.Text.Trim().ToUpperInvariant();
            discountType = (cbVoucherDiscountType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Percent";
            discountValue = 0;
            startAt = dpVoucherStart.SelectedDate?.Date ?? DateTime.Today;
            expiryAt = dpVoucherExpiry.SelectedDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Today.AddDays(1).AddTicks(-1);
            maxUsage = 0;
            totalQuantity = 0;
            isActive = chkVoucherActive.IsChecked == true;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code) ||
                !decimal.TryParse(txtVoucherDiscountValue.Text.Trim(), out discountValue) ||
                !int.TryParse(txtVoucherMaxUsage.Text.Trim(), out maxUsage) ||
                !int.TryParse(txtVoucherTotalQuantity.Text.Trim(), out totalQuantity))
            {
                MessageBox.Show("Vui lòng nhập đúng tên, mã, giá trị giảm và các giới hạn voucher.", "Dữ liệu không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (code.Length > 50 || discountValue <= 0 || (discountType == "Percent" && discountValue > 100) ||
                maxUsage <= 0 || totalQuantity <= 0 || expiryAt <= startAt)
            {
                MessageBox.Show("Kiểm tra lại: giảm phần trăm phải từ 0 đến 100, giới hạn phải lớn hơn 0 và ngày hết hạn phải sau ngày bắt đầu.", "Dữ liệu không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void btnAddVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadVoucherForm(out var name, out var code, out var discountType, out var discountValue,
                out var startAt, out var expiryAt, out var maxUsage, out var totalQuantity, out var isActive)) return;

            try
            {
                using var context = new BookStoreDbContext();
                if (context.Vouchers.Any(v => v.Code == code))
                {
                    MessageBox.Show("Mã voucher đã tồn tại.", "Trùng mã voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                context.Vouchers.Add(new Voucher
                {
                    Name = name, Code = code, DiscountType = discountType, DiscountValue = discountValue,
                    StartAt = startAt.ToUniversalTime(), ExpiryAt = expiryAt.ToUniversalTime(),
                    MaxUsagePerUser = maxUsage, TotalQuantity = totalQuantity, IsActive = isActive,
                    CreatedAt = DateTime.UtcNow
                });
                context.SaveChanges();
                LoadVouchers(context);
                ClearVoucherForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tạo voucher:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpdateVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVoucher == null)
            {
                MessageBox.Show("Vui lòng chọn voucher cần cập nhật.", "Chưa chọn voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryReadVoucherForm(out var name, out var code, out var discountType, out var discountValue,
                out var startAt, out var expiryAt, out var maxUsage, out var totalQuantity, out var isActive)) return;

            try
            {
                using var context = new BookStoreDbContext();
                var voucher = context.Vouchers.Find(_selectedVoucher.VoucherId);
                if (voucher == null) return;
                if (context.Vouchers.Any(v => v.Code == code && v.VoucherId != voucher.VoucherId))
                {
                    MessageBox.Show("Mã voucher đã tồn tại.", "Trùng mã voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (totalQuantity < voucher.SavedCount)
                {
                    MessageBox.Show($"Tổng lượt lưu không thể nhỏ hơn số lượt đã lưu ({voucher.SavedCount}).", "Giới hạn voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                voucher.Name = name; voucher.Code = code; voucher.DiscountType = discountType; voucher.DiscountValue = discountValue;
                voucher.StartAt = startAt.ToUniversalTime(); voucher.ExpiryAt = expiryAt.ToUniversalTime();
                voucher.MaxUsagePerUser = maxUsage; voucher.TotalQuantity = totalQuantity; voucher.IsActive = isActive;
                context.SaveChanges();
                LoadVouchers(context);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể cập nhật voucher:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVoucher == null) return;
            if (MessageBox.Show($"Xóa voucher '{_selectedVoucher.Code}'?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                using var context = new BookStoreDbContext();
                var voucher = context.Vouchers.Find(_selectedVoucher.VoucherId);
                if (voucher != null) { context.Vouchers.Remove(voucher); context.SaveChanges(); }
                LoadVouchers(context);
                ClearVoucherForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể xóa voucher:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClearVoucherForm_Click(object sender, RoutedEventArgs e) => ClearVoucherForm();

        private void ClearVoucherForm()
        {
            _selectedVoucher = null;
            dgAdminVouchers.SelectedItem = null;
            txtVoucherName.Clear(); txtVoucherCode.Clear(); txtVoucherDiscountValue.Clear();
            txtVoucherMaxUsage.Text = "1"; txtVoucherTotalQuantity.Text = "100";
            dpVoucherStart.SelectedDate = DateTime.Today;
            dpVoucherExpiry.SelectedDate = DateTime.Today.AddMonths(1);
            cbVoucherDiscountType.SelectedIndex = 0;
            chkVoucherActive.IsChecked = true;
        }

        // ==================== BOOKS CRUD ====================
        private void txtAdminBookSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyBookFilters();
        private void AdminBookFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyBookFilters();

        private void ApplyBookFilters()
        {
            if (!_isLoaded) return;
            IEnumerable<BookCardViewModel> query = _allBooks;

            var keyword = txtAdminBookSearch?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(b =>
                    b.Title.ToLower().Contains(keyword) ||
                    b.Author.ToLower().Contains(keyword) ||
                    (b.Isbn != null && b.Isbn.ToLower().Contains(keyword)));
            }

            if (cbAdminBookCategoryFilter?.SelectedItem is CategoryFilterItem cat && cat.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == cat.CategoryId.Value);
            }

            if (cbAdminBookStatusFilter?.SelectedItem is ComboBoxItem statusItem)
            {
                var status = statusItem.Content?.ToString();
                if (!string.IsNullOrEmpty(status) && status != "Tất cả hình thức")
                {
                    query = query.Where(b => string.Equals(b.BookStatus, status, StringComparison.OrdinalIgnoreCase));
                }
            }

            dgAdminBooks.ItemsSource = query.ToList();
        }

        private void dgAdminBooks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgAdminBooks.SelectedItem is BookCardViewModel book)
            {
                _selectedBook = book;
                _pendingBookImageData = null;
                _pendingBookImageMimeType = null;
                _removeBookImageRequested = false;

                txtBookFormHeader.Text = $"📝 Sửa Sách #{book.BookId}";
                txtBookTitle.Text = book.Title;
                txtBookAuthor.Text = book.Author;
                cbBookCategory.SelectedValue = book.CategoryId;
                txtBookPublisher.Text = book.Publisher ?? "";
                txtBookYear.Text = book.PublicationYear?.ToString() ?? "";
                txtBookPrice.Text = book.Price.ToString("0.##");
                txtBookStock.Text = book.StockQuantity.ToString();
                txtBookIsbn.Text = book.Isbn ?? "";
                txtBookDescription.Text = book.Description ?? "";

                foreach (ComboBoxItem item in cbBookFormStatus.Items)
                {
                    if (string.Equals(item.Content?.ToString(), book.BookStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        cbBookFormStatus.SelectedItem = item;
                        break;
                    }
                }

                imgBookPreview.Source = book.CoverImage;
            }
        }

        private void btnClearBookForm_Click(object sender, RoutedEventArgs e)
        {
            dgAdminBooks.SelectedItem = null;
            _selectedBook = null;
            _pendingBookImageData = null;
            _pendingBookImageMimeType = null;
            _removeBookImageRequested = false;

            txtBookFormHeader.Text = "📝 Thêm Sách Mới";
            txtBookTitle.Text = "";
            txtBookAuthor.Text = "";
            if (cbBookCategory.Items.Count > 0) cbBookCategory.SelectedIndex = 0;
            txtBookPublisher.Text = "";
            txtBookYear.Text = DateTime.Now.Year.ToString();
            txtBookPrice.Text = "120000";
            txtBookStock.Text = "15";
            txtBookIsbn.Text = "";
            txtBookDescription.Text = "";
            cbBookFormStatus.SelectedIndex = 0;
            imgBookPreview.Source = null;
        }

        private void btnUploadBookCover_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh bìa cho sách",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var bytes = File.ReadAllBytes(dlg.FileName);
                    var ext = Path.GetExtension(dlg.FileName).ToLower();
                    _pendingBookImageMimeType = ext switch
                    {
                        ".png" => "image/png",
                        ".bmp" => "image/bmp",
                        ".webp" => "image/webp",
                        _ => "image/jpeg"
                    };
                    _pendingBookImageData = bytes;
                    _removeBookImageRequested = false;
                    imgBookPreview.Source = BookCardViewModel.LoadImageSafe(bytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể đọc file ảnh: {ex.Message}", "Lỗi ảnh", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnClearBookCover_Click(object sender, RoutedEventArgs e)
        {
            _pendingBookImageData = null;
            _pendingBookImageMimeType = null;
            _removeBookImageRequested = true;
            imgBookPreview.Source = null;
        }

        private bool TryValidateBookForm(out string title, out string author, out int categoryId, out decimal price, out int stock, out int? year, out string? isbn, out string status)
        {
            title = txtBookTitle.Text.Trim();
            author = txtBookAuthor.Text.Trim();
            categoryId = cbBookCategory.SelectedValue is int cid ? cid : 0;
            price = 0;
            stock = 0;
            year = null;
            isbn = string.IsNullOrWhiteSpace(txtBookIsbn.Text) ? null : txtBookIsbn.Text.Trim();
            status = (cbBookFormStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bán";

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(author) || categoryId <= 0)
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Tiêu đề sách, Tác giả và chọn Thể loại!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!decimal.TryParse(txtBookPrice.Text.Trim(), out price) || price < 0)
            {
                MessageBox.Show("Đơn giá sách phải là số hợp lệ (>= 0)!", "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(txtBookStock.Text.Trim(), out stock) || stock < 0)
            {
                MessageBox.Show("Số lượng tồn kho phải là số nguyên (>= 0)!", "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(txtBookYear.Text))
            {
                if (int.TryParse(txtBookYear.Text.Trim(), out int parsedYear))
                {
                    year = parsedYear;
                }
                else
                {
                    MessageBox.Show("Năm xuất bản phải là số nguyên!", "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private void btnAddBook_Click(object sender, RoutedEventArgs e)
        {
            if (!TryValidateBookForm(out var title, out var author, out var categoryId, out var price, out var stock, out var year, out var isbn, out var status))
                return;

            try
            {
                using var context = new BookStoreDbContext();
                if (!string.IsNullOrEmpty(isbn) && context.Books.Any(b => b.Isbn == isbn))
                {
                    MessageBox.Show($"Mã ISBN '{isbn}' đã tồn tại trên một quyển sách khác!", "Trùng ISBN", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newBook = new Book
                {
                    Title = title,
                    Author = author,
                    CategoryId = categoryId,
                    Publisher = string.IsNullOrWhiteSpace(txtBookPublisher.Text) ? null : txtBookPublisher.Text.Trim(),
                    PublicationYear = year,
                    Isbn = isbn,
                    Price = price,
                    StockQuantity = stock,
                    Description = string.IsNullOrWhiteSpace(txtBookDescription.Text) ? null : txtBookDescription.Text.Trim(),
                    BookStatus = status,
                    CreatedAt = DateTime.UtcNow
                };

                context.Books.Add(newBook);
                context.SaveChanges();

                if (_pendingBookImageData != null && _pendingBookImageData.Length > 0)
                {
                    context.BookImages.Add(new BookImage
                    {
                        BookId = newBook.BookId,
                        ImageData = _pendingBookImageData,
                        MimeType = _pendingBookImageMimeType ?? "image/jpeg",
                        IsCover = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    context.SaveChanges();
                }

                MessageBox.Show($"✅ Đã thêm sách '{newBook.Title}' vào kho thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                btnClearBookForm_Click(sender, e);
                LoadAllDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm sách mới:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpdateBook_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBook == null)
            {
                MessageBox.Show("Vui lòng chọn một quyển sách từ bảng bên trái để cập nhật!", "Chưa chọn sách", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryValidateBookForm(out var title, out var author, out var categoryId, out var price, out var stock, out var year, out var isbn, out var status))
                return;

            try
            {
                using var context = new BookStoreDbContext();
                var book = context.Books
                    .Include(b => b.BookImages)
                    .FirstOrDefault(b => b.BookId == _selectedBook.BookId);

                if (book == null)
                {
                    MessageBox.Show("Không tìm thấy sách trong CSDL!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(isbn) && context.Books.Any(b => b.Isbn == isbn && b.BookId != book.BookId))
                {
                    MessageBox.Show($"Mã ISBN '{isbn}' đã thuộc về quyển sách khác!", "Trùng ISBN", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                book.Title = title;
                book.Author = author;
                book.CategoryId = categoryId;
                book.Publisher = string.IsNullOrWhiteSpace(txtBookPublisher.Text) ? null : txtBookPublisher.Text.Trim();
                book.PublicationYear = year;
                book.Isbn = isbn;
                book.Price = price;
                book.StockQuantity = stock;
                book.Description = string.IsNullOrWhiteSpace(txtBookDescription.Text) ? null : txtBookDescription.Text.Trim();
                book.BookStatus = status;

                if (_removeBookImageRequested && book.BookImages.Any())
                {
                    context.BookImages.RemoveRange(book.BookImages);
                }
                else if (_pendingBookImageData != null && _pendingBookImageData.Length > 0)
                {
                    var existingCover = book.BookImages.FirstOrDefault(i => i.IsCover) ?? book.BookImages.FirstOrDefault();
                    if (existingCover != null)
                    {
                        existingCover.ImageData = _pendingBookImageData;
                        existingCover.MimeType = _pendingBookImageMimeType ?? "image/jpeg";
                        existingCover.IsCover = true;
                    }
                    else
                    {
                        context.BookImages.Add(new BookImage
                        {
                            BookId = book.BookId,
                            ImageData = _pendingBookImageData,
                            MimeType = _pendingBookImageMimeType ?? "image/jpeg",
                            IsCover = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                context.SaveChanges();
                MessageBox.Show($"✅ Đã cập nhật thông tin sách '{book.Title}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteBook_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBook == null)
            {
                MessageBox.Show("Vui lòng chọn một quyển sách cần xóa!", "Chưa chọn sách", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa quyển sách '{_selectedBook.Title}' không?",
                "Xác nhận xóa sách",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var context = new BookStoreDbContext();
                var book = context.Books
                    .Include(b => b.OrderItems)
                    .FirstOrDefault(b => b.BookId == _selectedBook.BookId);

                if (book == null) return;

                if (book.OrderItems.Any())
                {
                    MessageBox.Show(
                        "Quyển sách này đã từng phát sinh trong đơn hàng nên không thể xóa vĩnh viễn (để bảo toàn lịch sử hóa đơn).\n\nHệ thống sẽ đặt Tồn kho = 0 để ngừng giao dịch sách này.",
                        "Ràng buộc dữ liệu hóa đơn",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    book.StockQuantity = 0;
                    context.SaveChanges();
                }
                else
                {
                    context.Books.Remove(book);
                    context.SaveChanges();
                    MessageBox.Show("🗑️ Đã xóa sách khỏi CSDL thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                btnClearBookForm_Click(sender, e);
                LoadAllDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể xóa sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== CATEGORIES CRUD ====================
        private void dgAdminCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgAdminCategories.SelectedItem is CategoryFilterItem cat)
            {
                _selectedCategory = cat;
                txtCategoryName.Text = cat.Name;
                txtCategoryDescription.Text = cat.Description ?? "";
            }
        }

        private void btnClearCategoryForm_Click(object sender, RoutedEventArgs e)
        {
            dgAdminCategories.SelectedItem = null;
            _selectedCategory = null;
            txtCategoryName.Text = "";
            txtCategoryDescription.Text = "";
        }

        private void btnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            var name = txtCategoryName.Text.Trim();
            var desc = txtCategoryDescription.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Vui lòng nhập Tên thể loại!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                if (context.Categories.Any(c => c.CategoryName.ToLower() == name.ToLower()))
                {
                    MessageBox.Show("Tên thể loại này đã tồn tại!", "Trùng tên", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                context.Categories.Add(new Category
                {
                    CategoryName = name,
                    Description = string.IsNullOrEmpty(desc) ? null : desc
                });
                context.SaveChanges();

                MessageBox.Show($"✅ Đã thêm thể loại '{name}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                btnClearCategoryForm_Click(sender, e);
                LoadAllDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm thể loại:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpdateCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null || !_selectedCategory.CategoryId.HasValue)
            {
                MessageBox.Show("Vui lòng chọn một thể loại để sửa!", "Chưa chọn thể loại", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var name = txtCategoryName.Text.Trim();
            var desc = txtCategoryDescription.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Tên thể loại không được để trống!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var cat = context.Categories.FirstOrDefault(c => c.CategoryId == _selectedCategory.CategoryId.Value);
                if (cat == null) return;

                cat.CategoryName = name;
                cat.Description = string.IsNullOrEmpty(desc) ? null : desc;
                context.SaveChanges();

                MessageBox.Show("✅ Đã cập nhật thể loại thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật thể loại:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null || !_selectedCategory.CategoryId.HasValue)
            {
                MessageBox.Show("Vui lòng chọn một thể loại cần xóa!", "Chưa chọn thể loại", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var cat = context.Categories
                    .Include(c => c.Books)
                    .FirstOrDefault(c => c.CategoryId == _selectedCategory.CategoryId.Value);

                if (cat == null) return;

                if (cat.Books.Any())
                {
                    MessageBox.Show($"Thể loại '{cat.CategoryName}' đang chứa {cat.Books.Count} đầu sách. Vui lòng chuyển hoặc xóa các sách đó trước khi xóa thể loại!", "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show($"Xác nhận xóa thể loại '{cat.CategoryName}'?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    context.Categories.Remove(cat);
                    context.SaveChanges();
                    btnClearCategoryForm_Click(sender, e);
                    LoadAllDashboardData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa thể loại:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== ORDERS & RENTALS MANAGEMENT ====================
        private void AdminOrderFilter_Changed(object sender, EventArgs e) => ApplyOrderFilters();

        private void ApplyOrderFilters()
        {
            if (!_isLoaded) return;
            IEnumerable<OrderViewModel> query = _allOrders;

            var keyword = txtAdminOrderSearch?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(o =>
                    o.OrderCode.ToLower().Contains(keyword) ||
                    o.CustomerName.ToLower().Contains(keyword) ||
                    o.RecipientPhone.ToLower().Contains(keyword) ||
                    o.ShippingAddress.ToLower().Contains(keyword));
            }

            if (cbAdminOrderTypeFilter?.SelectedItem is ComboBoxItem typeItem)
            {
                var typeStr = typeItem.Content?.ToString();
                if (typeStr == "Đơn Mua Sách")
                    query = query.Where(o => !o.IsRentalOrder);
                else if (typeStr == "Đơn Thuê Sách")
                    query = query.Where(o => o.IsRentalOrder);
            }

            if (cbAdminOrderStatusFilter?.SelectedItem is ComboBoxItem statusItem)
            {
                var statusStr = (statusItem.Tag ?? statusItem.Content)?.ToString();
                if (!string.IsNullOrEmpty(statusStr) && statusStr != "Tất cả trạng thái")
                {
                    query = query.Where(o => string.Equals(o.OrderStatus, statusStr, StringComparison.OrdinalIgnoreCase));
                }
            }

            dgAdminOrders.ItemsSource = query.ToList();
        }

        private void dgAdminOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgAdminOrders.SelectedItem is OrderViewModel order)
            {
                _selectedOrder = order;
                txtAdminOrderTitle.Text = $"🧾 Chi Tiết Đơn {order.OrderCode} ({order.OrderTypeDisplay})";
                txtOrderCustomerInfo.Text = order.CustomerEmail == "Khách vãng lai (Không TK)"
                    ? $"Khách hàng: {order.CustomerName} ({order.CustomerEmail})"
                    : $"Khách hàng: {order.CustomerName}";
                txtOrderPhoneInfo.Text = $"SĐT nhận sách: {order.RecipientPhone}";
                txtOrderAddressInfo.Text = string.IsNullOrWhiteSpace(order.CancelReason)
                    ? $"Địa chỉ / Kỳ hạn: {order.ShippingAddress}"
                    : $"Địa chỉ / Kỳ hạn: {order.ShippingAddress}\nLý do hủy đơn: {order.CancelReason}";
                txtOrderPaymentMethodInfo.Text = $"Thanh toán: {order.PaymentMethod} • Tổng: {order.FormattedTotalAmount}";

                dgAdminOrderItems.ItemsSource = order.Items;

                foreach (ComboBoxItem item in cbAdminEditOrderStatus.Items)
                {
                    var val = (item.Tag ?? item.Content)?.ToString();
                    if (string.Equals(val, order.OrderStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        cbAdminEditOrderStatus.SelectedItem = item;
                        break;
                    }
                }

                foreach (ComboBoxItem item in cbAdminEditPaymentStatus.Items)
                {
                    var val = (item.Tag ?? item.Content)?.ToString();
                    if (string.Equals(val, order.PaymentStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        cbAdminEditPaymentStatus.SelectedItem = item;
                        break;
                    }
                }

                bool isLocked = IsOrderLocked(order.OrderStatus, order.PaymentStatus);
                cbAdminEditOrderStatus.IsEnabled = !isLocked;
                cbAdminEditPaymentStatus.IsEnabled = !isLocked;
                btnUpdateOrderStatus.IsEnabled = !isLocked;
                btnConfirmBookReturn.IsEnabled = !isLocked;
            }
        }

        private static bool IsOrderLocked(string? orderStatus, string? paymentStatus)
        {
            bool isFinalOrderStatus = string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Returned", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Đã hủy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Đã trả sách", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderStatus, "Hoàn thành", StringComparison.OrdinalIgnoreCase);

            bool isPaid = string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentStatus, "Đã thanh toán", StringComparison.OrdinalIgnoreCase);

            return isFinalOrderStatus || isPaid;
        }

        private void btnUpdateOrderStatus_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn hàng từ danh sách bên trái!", "Chưa chọn đơn", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsOrderLocked(_selectedOrder.OrderStatus, _selectedOrder.PaymentStatus))
            {
                MessageBox.Show("Đơn hàng đã ở trạng thái khóa (Đã hủy / Đã trả sách / Hoàn thành / Đã thanh toán), không thể chỉnh sửa!", "Không thể chỉnh sửa", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedOrderItem = cbAdminEditOrderStatus.SelectedItem as ComboBoxItem;
            var selectedPaymentItem = cbAdminEditPaymentStatus.SelectedItem as ComboBoxItem;
            var newOrderStatus = (selectedOrderItem?.Tag ?? selectedOrderItem?.Content)?.ToString() ?? _selectedOrder.OrderStatus;
            var newPaymentStatus = (selectedPaymentItem?.Tag ?? selectedPaymentItem?.Content)?.ToString() ?? _selectedOrder.PaymentStatus;

            try
            {
                using var context = new BookStoreDbContext();
                var order = context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefault(o => o.OrderId == _selectedOrder.OrderId);

                if (order == null) return;

                if (IsOrderLocked(order.OrderStatus, order.PaymentStatus))
                {
                    MessageBox.Show("Đơn hàng đã ở trạng thái khóa (Đã hủy / Đã trả sách / Hoàn thành / Đã thanh toán), không thể chỉnh sửa!", "Không thể chỉnh sửa", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string oldStatus = order.OrderStatus;
                order.OrderStatus = newOrderStatus;
                order.PaymentStatus = newPaymentStatus;

                // Nếu chuyển sang Cancelled hoặc Returned từ trạng thái chưa hoàn kho -> hoàn lại tồn kho
                bool wasReturnedOrCancelled = string.Equals(oldStatus, "Returned", StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(oldStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
                bool isNowReturnedOrCancelled = string.Equals(newOrderStatus, "Returned", StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(newOrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

                if (!wasReturnedOrCancelled && isNowReturnedOrCancelled)
                {
                    foreach (var item in order.OrderItems)
                    {
                        var book = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                        if (book != null)
                        {
                            book.StockQuantity += item.Quantity;
                        }
                    }
                }

                context.SaveChanges();
                MessageBox.Show($"✅ Đã cập nhật đơn hàng #{order.OrderId:D4} sang trạng thái '{newOrderStatus}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật đơn hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnConfirmBookReturn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOrder == null)
            {
                MessageBox.Show("Vui lòng chọn đơn thuê sách cần xác nhận trả!", "Chưa chọn đơn", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsOrderLocked(_selectedOrder.OrderStatus, _selectedOrder.PaymentStatus))
            {
                MessageBox.Show("Đơn hàng đã ở trạng thái khóa (Đã hủy / Đã trả sách / Hoàn thành / Đã thanh toán), không thể chỉnh sửa!", "Không thể chỉnh sửa", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var order = context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefault(o => o.OrderId == _selectedOrder.OrderId);

                if (order == null) return;

                if (IsOrderLocked(order.OrderStatus, order.PaymentStatus))
                {
                    MessageBox.Show("Đơn hàng đã ở trạng thái khóa (Đã hủy / Đã trả sách / Hoàn thành / Đã thanh toán), không thể chỉnh sửa!", "Không thể chỉnh sửa", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                order.OrderStatus = "Returned";
                order.PaymentStatus = "Paid";

                foreach (var item in order.OrderItems)
                {
                    var book = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                    if (book != null)
                    {
                        book.StockQuantity += item.Quantity;
                    }
                }

                context.SaveChanges();
                MessageBox.Show($"📥 Đã xác nhận khách trả sách cho đơn #{order.OrderId:D4}! Trạng thái chuyển thành 'Đã trả sách' và số lượng đã được cộng lại vào kho.", "Hoàn trả sách thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllDashboardData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xác nhận trả sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnOpenCreateOrderPos_Click(object sender, RoutedEventArgs e)
        {
            OpenCreateOrderPosDialog(null);
        }

        private void btnCreateOrderForSelectedUser_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn một tài khoản ở bảng bên trái hoặc bấm nút '➕ Tạo Đơn Mua / Thuê Mới' để tạo đơn khách vãng lai!", "Chưa chọn tài khoản", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OpenCreateOrderPosDialog(_selectedUser.UserId);
        }

        private void OpenCreateOrderPosDialog(int? preselectedUserId)
        {
            var createOrderWin = new AdminCreateOrderWindow(preselectedUserId)
            {
                Owner = this
            };

            createOrderWin.ShowDialog();
            if (createOrderWin.OrderCreated)
            {
                LoadAllDashboardData();
                SetActiveTab("Orders");

                if (createOrderWin.CreatedOrderId.HasValue)
                {
                    var createdOrder = _allOrders.FirstOrDefault(o => o.OrderId == createOrderWin.CreatedOrderId.Value);
                    if (createdOrder != null)
                    {
                        dgAdminOrders.SelectedItem = createdOrder;
                        dgAdminOrders.ScrollIntoView(createdOrder);
                    }
                }
            }
        }

        // ==================== USERS MANAGEMENT ====================
        private void txtAdminUserSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyUserFilters();
        private void cbAdminUserRoleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyUserFilters();

        private void ApplyUserFilters()
        {
            if (!_isLoaded) return;
            IEnumerable<UserAdminViewModel> query = _allUsers;

            var keyword = txtAdminUserSearch?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(keyword) ||
                    (u.IsAdminRole && u.Email.ToLower().Contains(keyword)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(keyword)) ||
                    (u.Address != null && u.Address.ToLower().Contains(keyword)));
            }

            if (cbAdminUserRoleFilter?.SelectedItem is ComboBoxItem roleItem)
            {
                var roleFilter = roleItem.Content?.ToString();
                if (!string.IsNullOrEmpty(roleFilter) && roleFilter != "Tất cả vai trò")
                {
                    query = query.Where(u => string.Equals(u.RoleName, roleFilter, StringComparison.OrdinalIgnoreCase));
                }
            }

            var filtered = query.ToList();
            dgAdminUsers.ItemsSource = filtered;

            if (txtAdminUserSummary != null)
            {
                int adminCount = _allUsers.Count(u => u.RoleId == 2 || string.Equals(u.RoleName, "Admin", StringComparison.OrdinalIgnoreCase));
                int custCount = _allUsers.Count - adminCount;
                txtAdminUserSummary.Text = $"Hiển thị {filtered.Count}/{_allUsers.Count} tài khoản ({adminCount} Admin • {custCount} Khách hàng)";
            }
        }

        private void btnOpenMyProfile_Click(object sender, RoutedEventArgs e)
        {
            var profileWin = new UserProfileWindow("Admin");
            profileWin.Show();
            Close();
        }
        private void dgAdminUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgAdminUsers.SelectedItem is UserAdminViewModel u)
            {
                _selectedUser = u;
                bool isOtherAccount = SessionManager.CurrentUser == null || u.UserId != SessionManager.CurrentUser.UserId;

                txtAdminUserFullName.Text = u.FullName;
                txtAdminUserEmail.Text = isOtherAccount ? "" : u.Email;
                txtAdminUserPhone.Text = u.PhoneNumber ?? "";
                txtAdminUserAddress.Text = u.Address ?? "";
                cbAdminUserRole.SelectedValue = u.RoleId;
                txtAdminUserPassword.Text = "";

                lblAdminUserEmail.Visibility = isOtherAccount ? Visibility.Collapsed : Visibility.Visible;
                txtAdminUserEmail.Visibility = isOtherAccount ? Visibility.Collapsed : Visibility.Visible;
                txtAdminUserEmail.IsReadOnly = isOtherAccount;
                txtAdminUserEmail.IsEnabled = !isOtherAccount;
                lblAdminUserPassword.Visibility = isOtherAccount ? Visibility.Collapsed : Visibility.Visible;
                txtAdminUserPassword.Visibility = isOtherAccount ? Visibility.Collapsed : Visibility.Visible;
                txtAdminUserPassword.IsEnabled = !isOtherAccount;
                if (!isOtherAccount)
                {
                    lblAdminUserPassword.Text = "MẬT KHẨU CỦA BẠN (Để trống nếu giữ nguyên)";
                }

                var userOrders = _allOrders.Where(o => o.UserId == u.UserId).ToList();
                dgAdminSelectedUserOrders.ItemsSource = userOrders;
                txtSelectedUserOrdersHeader.Text = $"📦 Lịch Sử Đơn Hàng Của '{u.FullName}' ({userOrders.Count} đơn • Tổng chi: {u.FormattedTotalSpent})";
            }
        }

        private void btnClearUserForm_Click(object sender, RoutedEventArgs e)
        {
            dgAdminUsers.SelectedItem = null;
            _selectedUser = null;
            txtAdminUserFullName.Text = "";
            txtAdminUserEmail.Text = "";
            txtAdminUserPhone.Text = "";
            txtAdminUserAddress.Text = "";
            if (cbAdminUserRole.Items.Count > 0) cbAdminUserRole.SelectedIndex = 0;
            lblAdminUserEmail.Visibility = Visibility.Visible;
            txtAdminUserEmail.Visibility = Visibility.Visible;
            txtAdminUserEmail.IsReadOnly = false;
            txtAdminUserEmail.IsEnabled = true;
            lblAdminUserPassword.Visibility = Visibility.Visible;
            lblAdminUserPassword.Text = "MẬT KHẨU ĐĂNG NHẬP *";
            txtAdminUserPassword.Visibility = Visibility.Visible;
            txtAdminUserPassword.IsEnabled = true;
            txtAdminUserPassword.Text = "";
            dgAdminSelectedUserOrders.ItemsSource = null;
            txtSelectedUserOrdersHeader.Text = "📦 Lịch Sử Đơn Mua & Thuê Của Tài Khoản Đang Chọn (Chọn 1 tài khoản ở bảng trên)";
        }

        private void btnAddUser_Click(object sender, RoutedEventArgs e)
        {
            var fullName = txtAdminUserFullName.Text.Trim();
            var email = txtAdminUserEmail.Text.Trim();
            var phone = txtAdminUserPhone.Text.Trim();
            var addr = txtAdminUserAddress.Text.Trim();
            var pwd = txtAdminUserPassword.Text.Trim();
            int roleId = cbAdminUserRole.SelectedValue is int rid ? rid : 1;

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("Khi tạo mới tài khoản, vui lòng nhập Họ tên, Email và Mật khẩu!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                if (context.Users.Any(u => u.Email.ToLower() == email.ToLower()))
                {
                    MessageBox.Show("Email này đã tồn tại trên hệ thống!", "Trùng Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var user = new User
                {
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone,
                    Address = string.IsNullOrEmpty(addr) ? null : addr,
                    PasswordHash = pwd,
                    RoleId = roleId,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(user);
                context.SaveChanges();

                btnClearUserForm_Click(sender, e);
                LoadAllDashboardData();
                MessageBox.Show($"✅ Đã tạo tài khoản '{user.FullName}' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm người dùng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpdateUser_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn một người dùng để cập nhật!", "Chưa chọn người dùng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var fullName = txtAdminUserFullName.Text.Trim();
            var email = txtAdminUserEmail.Text.Trim();
            var phone = txtAdminUserPhone.Text.Trim();
            var addr = txtAdminUserAddress.Text.Trim();
            var pwd = txtAdminUserPassword.Text.Trim();
            int roleId = cbAdminUserRole.SelectedValue is int rid ? rid : _selectedUser.RoleId;

            bool isOtherAccount = SessionManager.CurrentUser == null || _selectedUser.UserId != SessionManager.CurrentUser.UserId;

            if (string.IsNullOrEmpty(fullName) || (!isOtherAccount && string.IsNullOrEmpty(email)))
            {
                MessageBox.Show(isOtherAccount ? "Họ tên không được để trống!" : "Họ tên và Email không được để trống!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var user = context.Users.FirstOrDefault(u => u.UserId == _selectedUser.UserId);
                if (user == null) return;

                if (!isOtherAccount && context.Users.Any(u => u.Email.ToLower() == email.ToLower() && u.UserId != user.UserId))
                {
                    MessageBox.Show("Email này đã thuộc về người dùng khác!", "Trùng Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                user.FullName = fullName;
                if (!isOtherAccount)
                {
                    user.Email = email;
                }
                user.PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone;
                user.Address = string.IsNullOrEmpty(addr) ? null : addr;
                user.RoleId = roleId;
                if (!isOtherAccount && !string.IsNullOrEmpty(pwd))
                {
                    user.PasswordHash = pwd;
                }

                context.SaveChanges();
                LoadAllDashboardData();
                MessageBox.Show("✅ Đã cập nhật thông tin người dùng thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật người dùng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn một người dùng cần xóa!", "Chưa chọn", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SessionManager.CurrentUser != null && _selectedUser.UserId == SessionManager.CurrentUser.UserId)
            {
                MessageBox.Show("Bạn không thể tự xóa tài khoản Admin đang đăng nhập!", "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var user = context.Users
                    .Include(u => u.Orders)
                    .FirstOrDefault(u => u.UserId == _selectedUser.UserId);

                if (user == null) return;

                if (string.Equals(user.UserStatus, "deActive", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Tài khoản '{user.FullName}' đã ở trạng thái deActive!\nLý do: {user.BanReason}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string? reason = PromptBanReason(user.FullName);
                if (string.IsNullOrWhiteSpace(reason)) return;

                user.UserStatus = "deActive";
                user.BanReason = reason.Trim();
                context.SaveChanges();
                btnClearUserForm_Click(sender, e);
                LoadAllDashboardData();
                MessageBox.Show($"✅ Đã chuyển tài khoản '{user.FullName}' sang trạng thái deActive!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi vô hiệu hóa người dùng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? PromptBanReason(string fullName)
        {
            var dlg = new Window
            {
                Title = $"Khóa tài khoản {fullName}",
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
                Text = $"Nhập lý do vô hiệu hóa tài khoản '{fullName}' *",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
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
                Content = "Xác nhận",
                Width = 110,
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
                    MessageBox.Show("Vui lòng nhập lý do vô hiệu hóa tài khoản!", "Thiếu lý do", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            return dlg.ShowDialog() == true ? result : null;
        }

        // ==================== NAVIGATION & REPORT EXPORT ====================
        private void btnExportAdminReport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Xuất Báo Cáo Doanh Thu & Kho Sách BookStore",
                Filter = "CSV UTF-8 (*.csv)|*.csv|Text Report (*.txt)|*.txt",
                FileName = $"BaoCao_BookStore_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== BAO CAO TONG QUAN HE THONG BOOKSTORE ===");
                sb.AppendLine($"Thoi gian xuat: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine($"Tong so dau sach: {_allBooks.Count}, Tong ton kho: {_allBooks.Sum(b => b.StockQuantity)}");
                sb.AppendLine($"Tong so don hang: {_allOrders.Count}, Tong doanh thu hop le: {_allOrders.Where(o => o.OrderStatus != "Cancelled").Sum(o => o.TotalAmount):N0} VND");
                sb.AppendLine();
                sb.AppendLine("MA DON,KHACH HANG,SO DIEN THOAI,LOAI DON,TRANG THAI,THANH TOAN,TONG TIEN,NGAY TAO");
                foreach (var o in _allOrders)
                {
                    sb.AppendLine($"{o.OrderCode},\"{o.CustomerName}\",{o.RecipientPhone},\"{o.OrderTypeDisplay}\",{o.StatusDisplay},{o.PaymentStatus},{o.TotalAmount},{o.FormattedCreatedAt}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show($"📥 Đã xuất báo cáo thống kê ra file thành công:\n{dlg.FileName}", "Xuất báo cáo thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void btnOpenAllBooks_Click(object sender, RoutedEventArgs e)
        {
            var bookListWin = new BookListWindow();
            bookListWin.Show();
            Close();
        }

        private void btnOpenStorefront_Click(object sender, RoutedEventArgs e)
        {
            var home = new HomePage();
            home.Show();
            Close();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
            var login = new LoginWindow();
            login.Show();
            Close();
        }
    }
}

