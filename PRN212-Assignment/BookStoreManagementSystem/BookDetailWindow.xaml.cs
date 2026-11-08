using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BookStoreManagementSystem.Models;
using BookStoreManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BookStoreManagementSystem
{
    public partial class BookDetailWindow : Window
    {
        private int _currentBookId;
        private readonly string _returnScreen;
        private BookCardViewModel? _currentBook;
        private BookImageItemViewModel? _selectedGalleryImage;
        private int _selectedImageIndex = 0;
        private int _buyQuantity = 1;

        public BookDetailWindow() : this(0, "BookList")
        {
        }

        public BookDetailWindow(int bookId, string returnScreen = "BookList")
        {
            InitializeComponent();
            _currentBookId = bookId;
            _returnScreen = returnScreen;
            Loaded += BookDetailWindow_Loaded;
        }

        private void BookDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HomePage.EnsureRichSampleCatalogIfNeeded();
            UpdateHeaderAuthUI();
            LoadBookDetail(_currentBookId);
        }

        private void UpdateHeaderAuthUI()
        {
            txtCurrentUserName.Text = SessionManager.DisplayName;
            txtCurrentUserRole.Text = SessionManager.RoleDisplay;
            btnAuthAction.Content = SessionManager.IsLoggedIn ? "🔄 Đổi TK / Thoát" : "🔐 Đăng Nhập";
            btnOpenAdmin.Visibility = SessionManager.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            panelAdminImageManager.Visibility = SessionManager.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            panelAdminQuickBookActions.Visibility = SessionManager.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            if (SessionManager.CurrentUser != null)
            {
                if (!string.IsNullOrWhiteSpace(SessionManager.CurrentUser.PhoneNumber))
                {
                    txtModalRentPhone.Text = SessionManager.CurrentUser.PhoneNumber;
                }
                if (!string.IsNullOrWhiteSpace(SessionManager.CurrentUser.Address))
                {
                    txtModalRentAddress.Text = SessionManager.CurrentUser.Address;
                }
            }
        }

        public void LoadBookDetail(int bookId)
        {
            try
            {
                using var context = new BookStoreDbContext();
                HomePage.EnsureValidBookCoversInDatabase(context);

                var allBooks = context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .Include(b => b.BookImages)
                    .OrderByDescending(b => b.BookId)
                    .ToList();

                if (!allBooks.Any())
                {
                    MessageBox.Show("Hiện chưa có sách nào trong cơ sở dữ liệu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var targetEntity = allBooks.FirstOrDefault(b => b.BookId == bookId) ?? allBooks.First();
                _currentBookId = targetEntity.BookId;
                _currentBook = BookCardViewModel.FromEntity(targetEntity);
                _buyQuantity = 1;

                // Chọn ảnh bìa mặc định trong album
                if (_currentBook.GalleryImages.Any())
                {
                    var coverItem = _currentBook.GalleryImages.FirstOrDefault(i => i.IsCover) ?? _currentBook.GalleryImages.First();
                    _selectedImageIndex = _currentBook.GalleryImages.IndexOf(coverItem);
                    if (_selectedImageIndex < 0) _selectedImageIndex = 0;
                    _selectedGalleryImage = _currentBook.GalleryImages[_selectedImageIndex];
                }
                else
                {
                    _selectedImageIndex = 0;
                    _selectedGalleryImage = null;
                }

                BindBookToUI();
                LoadRelatedBooks(allBooks, targetEntity);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải chi tiết sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BindBookToUI()
        {
            if (_currentBook == null) return;

            Title = $"BookStore - {_currentBook.Title} (/books/detail/{_currentBook.BookId})";
            txtBreadcrumbRoute.Text = $"/books/detail/{_currentBook.BookId}";
            txtBreadcrumbTitle.Text = _currentBook.Title;

            txtCategoryBadge.Text = $"📚 {_currentBook.CategoryName}";
            txtBookCodeBadge.Text = $"#BOOK-{_currentBook.BookId:D3}";
            txtBookTitle.Text = _currentBook.Title;
            txtBookAuthor.Text = $"Tác giả: {_currentBook.Author}";

            txtStatusBadge.Text = _currentBook.BookStatus;
            borderStatusBadge.Background = new BrushConverter().ConvertFromString(_currentBook.StatusBadgeColor) as Brush;

            txtStockBadge.Text = _currentBook.StockStatusText;
            borderStockBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentBook.IsInStock ? "#DCFCE7" : "#FEE2E2"));
            txtStockBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_currentBook.StockBadgeColor));

            txtPublisher.Text = string.IsNullOrWhiteSpace(_currentBook.Publisher) ? "Đang cập nhật" : _currentBook.Publisher;
            txtPublicationYear.Text = _currentBook.PublicationYear.HasValue ? _currentBook.PublicationYear.Value.ToString() : "Đang cập nhật";
            txtIsbn.Text = string.IsNullOrWhiteSpace(_currentBook.Isbn) ? "Đang cập nhật" : _currentBook.Isbn;
            txtStockCount.Text = $"{_currentBook.StockQuantity} cuốn";

            txtBuyPrice.Text = _currentBook.FormattedPrice;
            txtBuyQuantity.Text = _buyQuantity.ToString();
            txtDescription.Text = _currentBook.Description;

            btnAddToCartBuy.IsEnabled = _currentBook.IsInStock;
            btnBuyNow.IsEnabled = _currentBook.IsInStock;
            btnAddRentToCart.IsEnabled = _currentBook.IsInStock;
            btnRentNow.IsEnabled = _currentBook.IsInStock;

            // Cập nhật gói thuê
            if (rbRent7Days != null)
            {
                rbRent7Days.Content = $"Gói 7 ngày (1 tuần) — {_currentBook.RentalPrice7Days:N0} đ";
                rbRent14Days.Content = $"Gói 14 ngày (2 tuần) — {_currentBook.RentalPrice14Days:N0} đ";
                rbRent30Days.Content = $"Gói 30 ngày (1 tháng) — {_currentBook.RentalPrice30Days:N0} đ";
            }
            UpdateRentalPackageUI();

            // Cập nhật khung Admin sửa nhanh
            txtAdminQuickPrice.Text = _currentBook.Price.ToString("0");
            txtAdminQuickStock.Text = _currentBook.StockQuantity.ToString();
            foreach (ComboBoxItem item in cbAdminQuickStatus.Items)
            {
                if (string.Equals(item.Content?.ToString(), _currentBook.BookStatus, StringComparison.OrdinalIgnoreCase))
                {
                    cbAdminQuickStatus.SelectedItem = item;
                    break;
                }
            }
            if (cbAdminQuickStatus.SelectedIndex < 0) cbAdminQuickStatus.SelectedIndex = 0;

            RefreshGalleryPreviewUI();
        }

        private void RefreshGalleryPreviewUI()
        {
            if (_currentBook == null) return;

            int total = _currentBook.GalleryImages.Count;
            txtGalleryHeader.Text = $"🖼️ Album ảnh sách ({total})";

            if (total > 0 && _selectedGalleryImage != null)
            {
                imgMainCover.Source = _selectedGalleryImage.ImageSource;
                txtImageCounterBadge.Text = $"🖼️ Ảnh {_selectedImageIndex + 1} / {total}";
            }
            else
            {
                imgMainCover.Source = _currentBook.CoverImage;
                txtImageCounterBadge.Text = "🖼️ Chưa có ảnh";
            }

            btnPrevImage.Visibility = total > 1 ? Visibility.Visible : Visibility.Collapsed;
            btnNextImage.Visibility = total > 1 ? Visibility.Visible : Visibility.Collapsed;

            icGalleryThumbnails.ItemsSource = null;
            icGalleryThumbnails.ItemsSource = _currentBook.GalleryImages;

            btnAdminSetAsCover.IsEnabled = _selectedGalleryImage != null;
            btnAdminDeleteImage.IsEnabled = _selectedGalleryImage != null;
        }

        private void LoadRelatedBooks(List<Book> allBooks, Book currentEntity)
        {
            var sameCategory = allBooks
                .Where(b => b.BookId != currentEntity.BookId && b.CategoryId == currentEntity.CategoryId)
                .ToList();

            var others = allBooks
                .Where(b => b.BookId != currentEntity.BookId && b.CategoryId != currentEntity.CategoryId)
                .ToList();

            var combined = sameCategory.Concat(others).Take(5).Select(BookCardViewModel.FromEntity).ToList();
            icRelatedBooks.ItemsSource = combined;

            txtRelatedSubtitle.Text = sameCategory.Any()
                ? $"Có {sameCategory.Count} đầu sách cùng thể loại '{currentEntity.Category?.CategoryName}' và các sách nổi bật khác"
                : "Các đầu sách nổi bật khác đang có sẵn tại BookStore";
        }

        // ==================== GALLERY NAVIGATION ====================
        private void btnSelectThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null) return;
            if (sender is Button btn && btn.Tag is BookImageItemViewModel imgItem)
            {
                _selectedGalleryImage = imgItem;
                _selectedImageIndex = _currentBook.GalleryImages.IndexOf(imgItem);
                if (_selectedImageIndex < 0) _selectedImageIndex = 0;
                RefreshGalleryPreviewUI();
            }
        }

        private void btnPrevImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null || _currentBook.GalleryImages.Count <= 1) return;
            _selectedImageIndex = (_selectedImageIndex - 1 + _currentBook.GalleryImages.Count) % _currentBook.GalleryImages.Count;
            _selectedGalleryImage = _currentBook.GalleryImages[_selectedImageIndex];
            RefreshGalleryPreviewUI();
        }

        private void btnNextImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null || _currentBook.GalleryImages.Count <= 1) return;
            _selectedImageIndex = (_selectedImageIndex + 1) % _currentBook.GalleryImages.Count;
            _selectedGalleryImage = _currentBook.GalleryImages[_selectedImageIndex];
            RefreshGalleryPreviewUI();
        }

        // ==================== ADMIN IMAGE MANAGEMENT ====================
        private void btnAdminAddImages_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null) return;
            if (BookCardViewModel.PromptAndUploadBookImages(_currentBook.BookId, _currentBook.Title, setAsPrimaryCover: false, out int count))
            {
                LoadBookDetail(_currentBook.BookId);
                MessageBox.Show($"✅ Đã tải lên thêm {count} ảnh cho sách '{_currentBook.Title}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnAdminReplaceCover_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null) return;
            if (BookCardViewModel.PromptAndUploadBookImages(_currentBook.BookId, _currentBook.Title, setAsPrimaryCover: true, out _))
            {
                LoadBookDetail(_currentBook.BookId);
                MessageBox.Show($"✅ Đã đổi ảnh bìa chính cho sách '{_currentBook.Title}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnAdminSetAsCover_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null || _selectedGalleryImage == null) return;
            if (BookCardViewModel.SetCoverImageInDatabase(_currentBook.BookId, _selectedGalleryImage.ImageId))
            {
                LoadBookDetail(_currentBook.BookId);
                MessageBox.Show("⭐ Đã đặt bức ảnh đang xem làm ảnh bìa chính!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnAdminDeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null || _selectedGalleryImage == null) return;
            if (MessageBox.Show("Bạn có chắc chắn muốn xóa bức ảnh đang xem khỏi sách này không?", "Xác nhận xóa ảnh", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (BookCardViewModel.DeleteImageFromDatabase(_currentBook.BookId, _selectedGalleryImage.ImageId))
            {
                LoadBookDetail(_currentBook.BookId);
            }
        }

        // ==================== ADMIN QUICK STOCK / PRICE UPDATE ====================
        private void btnAdminSaveQuickBook_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null || !SessionManager.IsAdmin) return;

            if (!decimal.TryParse(txtAdminQuickPrice.Text.Trim(), out decimal newPrice) || newPrice < 0)
            {
                MessageBox.Show("Giá bán không hợp lệ!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtAdminQuickStock.Text.Trim(), out int newStock) || newStock < 0)
            {
                MessageBox.Show("Số lượng tồn kho không hợp lệ!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var status = (cbAdminQuickStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bán";

            try
            {
                using var context = new BookStoreDbContext();
                var dbBook = context.Books.FirstOrDefault(b => b.BookId == _currentBook.BookId);
                if (dbBook == null) return;

                dbBook.Price = newPrice;
                dbBook.StockQuantity = newStock;
                dbBook.BookStatus = status;
                context.SaveChanges();

                LoadBookDetail(_currentBook.BookId);
                MessageBox.Show($"✅ Đã cập nhật nhanh thông tin sách '{dbBook.Title}' thành công!", "Đã lưu CSDL", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu thông tin sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== BUY & RENT ACTIONS ====================
        private void btnDecreaseQty_Click(object sender, RoutedEventArgs e)
        {
            if (_buyQuantity > 1)
            {
                _buyQuantity--;
                txtBuyQuantity.Text = _buyQuantity.ToString();
            }
        }

        private void btnIncreaseQty_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null) return;
            if (_buyQuantity < Math.Max(1, _currentBook.StockQuantity))
            {
                _buyQuantity++;
                txtBuyQuantity.Text = _buyQuantity.ToString();
            }
        }

        private (int Days, decimal Fee) GetSelectedRentPackage()
        {
            if (_currentBook == null) return (7, 0m);
            if (rbRent14Days?.IsChecked == true) return (14, _currentBook.RentalPrice14Days);
            if (rbRent30Days?.IsChecked == true) return (30, _currentBook.RentalPrice30Days);
            return (7, _currentBook.RentalPrice7Days);
        }

        private void RentPackage_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRentalPackageUI();
        }

        private void UpdateRentalPackageUI()
        {
            if (_currentBook == null || txtSelectedRentPrice == null || txtSelectedRentDaysLabel == null || txtExpectedDueDate == null)
                return;

            var (days, fee) = GetSelectedRentPackage();
            txtSelectedRentPrice.Text = $"{fee:N0} đ";
            txtSelectedRentDaysLabel.Text = $" / {days} ngày";
            txtExpectedDueDate.Text = $"📅 Hạn hoàn trả dự kiến: {DateTime.Now.AddDays(days):dd/MM/yyyy}";
        }

        private void AddBookToUserCart(int quantity, bool navigateToHome)
        {
            if (_currentBook == null || !_currentBook.IsInStock)
            {
                MessageBox.Show("Sách này hiện đã hết hàng trong kho!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                int? userId = SessionManager.CurrentUser?.UserId;

                if (!userId.HasValue)
                {
                    var customer = context.Users.Include(u => u.Role).FirstOrDefault(u => u.RoleId == 1)
                                   ?? context.Users.Include(u => u.Role).FirstOrDefault();
                    if (customer != null)
                    {
                        SessionManager.CurrentUser = customer;
                        userId = customer.UserId;
                        UpdateHeaderAuthUI();
                    }
                }

                if (userId.HasValue)
                {
                    var cart = context.Carts
                        .Include(c => c.CartItems)
                        .FirstOrDefault(c => c.UserId == userId.Value);

                    if (cart == null)
                    {
                        cart = new Cart
                        {
                            UserId = userId.Value,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        context.Carts.Add(cart);
                        context.SaveChanges();
                    }

                    var item = cart.CartItems.FirstOrDefault(ci => ci.BookId == _currentBook.BookId);
                    if (item != null)
                    {
                        item.Quantity = Math.Min(_currentBook.StockQuantity, item.Quantity + quantity);
                    }
                    else
                    {
                        context.CartItems.Add(new CartItem
                        {
                            CartId = cart.CartId,
                            BookId = _currentBook.BookId,
                            Quantity = Math.Min(_currentBook.StockQuantity, quantity),
                            AddedAt = DateTime.UtcNow
                        });
                    }

                    cart.UpdatedAt = DateTime.UtcNow;
                    context.SaveChanges();
                }

                if (navigateToHome)
                {
                    var cartWin = new CartCheckoutWindow();
                    cartWin.Show();
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        $"🛒 Đã thêm {quantity} cuốn '{_currentBook.Title}' vào giỏ hàng thành công!",
                        "Đã thêm vào giỏ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi thêm vào giỏ hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddToCartBuy_Click(object sender, RoutedEventArgs e)
        {
            AddBookToUserCart(_buyQuantity, navigateToHome: false);
        }

        private void btnBuyNow_Click(object sender, RoutedEventArgs e)
        {
            AddBookToUserCart(_buyQuantity, navigateToHome: true);
        }

        private void btnAddRentToCart_Click(object sender, RoutedEventArgs e)
        {
            AddBookToUserCart(1, navigateToHome: false);
        }

        private void btnRentNow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null || !_currentBook.IsInStock) return;

            var (days, fee) = GetSelectedRentPackage();
            txtModalRentBookTitle.Text = _currentBook.Title;
            txtModalRentSummary.Text = $"Kỳ hạn thuê: {days} ngày • Phí thuê: {fee:N0} đ • Hạn trả: {DateTime.Now.AddDays(days):dd/MM/yyyy}";
            gridQuickRentModal.Visibility = Visibility.Visible;
        }

        private void btnCloseQuickRentModal_Click(object sender, RoutedEventArgs e)
        {
            gridQuickRentModal.Visibility = Visibility.Collapsed;
        }

        private void btnConfirmQuickRent_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null) return;

            var phone = txtModalRentPhone.Text.Trim();
            var address = txtModalRentAddress.Text.Trim();

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(address))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Số điện thoại và Địa chỉ nhận/hoàn trả sách!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (days, fee) = GetSelectedRentPackage();
            var dueDateStr = DateTime.Now.AddDays(days).ToString("dd/MM/yyyy");

            try
            {
                using var context = new BookStoreDbContext();
                var dbBook = context.Books.FirstOrDefault(b => b.BookId == _currentBook.BookId);
                if (dbBook == null || dbBook.StockQuantity <= 0)
                {
                    MessageBox.Show("Sách này hiện vừa hết hàng trong kho!", "Hết hàng", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int userId;
                if (SessionManager.CurrentUser != null)
                {
                    userId = SessionManager.CurrentUser.UserId;
                }
                else
                {
                    var fallbackUser = context.Users.Include(u => u.Role).FirstOrDefault(u => u.RoleId == 1)
                                       ?? context.Users.Include(u => u.Role).FirstOrDefault();
                    if (fallbackUser == null)
                    {
                        MessageBox.Show("Không tìm thấy tài khoản khách hàng trong CSDL!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    SessionManager.CurrentUser = fallbackUser;
                    userId = fallbackUser.UserId;
                    UpdateHeaderAuthUI();
                }

                var rentalOrder = new Order
                {
                    UserId = userId,
                    TotalAmount = fee,
                    OrderStatus = "Renting",
                    ShippingAddress = $"[THUÊ {days} NGÀY - Hạn trả: {dueDateStr}] {address}",
                    RecipientPhone = phone,
                    PaymentMethod = $"Thuê sách ({days} ngày)",
                    PaymentStatus = "Paid",
                    CreatedAt = DateTime.UtcNow
                };

                context.Orders.Add(rentalOrder);
                context.SaveChanges();

                context.OrderItems.Add(new OrderItem
                {
                    OrderId = rentalOrder.OrderId,
                    BookId = dbBook.BookId,
                    Quantity = 1,
                    UnitPrice = fee
                });

                dbBook.StockQuantity -= 1;
                if (dbBook.StockQuantity == 0)
                {
                    dbBook.BookStatus = "Đã thuê";
                }

                context.SaveChanges();

                gridQuickRentModal.Visibility = Visibility.Collapsed;
                LoadBookDetail(_currentBook.BookId);

                MessageBox.Show(
                    $"🎉 Đăng ký thuê sách thành công!\n\n" +
                    $"• Mã đơn thuê: #ORD-{rentalOrder.OrderId:D4}\n" +
                    $"• Sách: {dbBook.Title}\n" +
                    $"• Kỳ hạn: {days} ngày (Hạn trả: {dueDateStr})\n" +
                    $"• Tổng phí thuê: {fee:N0} đ",
                    "Thuê sách thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tạo đơn thuê sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== RELATED BOOKS & NAVIGATION ====================
        private void btnOpenRelatedBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel relatedBook)
            {
                LoadBookDetail(relatedBook.BookId);
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (string.Equals(_returnScreen, "Home", StringComparison.OrdinalIgnoreCase))
            {
                var homeWin = new HomePage();
                homeWin.Show();
                Close();
            }
            else if (string.Equals(_returnScreen, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var adminWin = new AdminDashboardWindow();
                adminWin.Show();
                Close();
            }
            else
            {
                var listWin = new BookListWindow();
                listWin.Show();
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

        private void btnOpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin) return;
            var adminWin = new AdminDashboardWindow();
            adminWin.Show();
            Close();
        }

        private void btnOpenProfile_Click(object sender, RoutedEventArgs e)
        {
            var profileWin = new UserProfileWindow("BookList");
            profileWin.Show();
            Close();
        }

        private void btnAuthAction_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
            var loginWin = new LoginWindow();
            loginWin.Show();
            Close();
        }
    }
}

