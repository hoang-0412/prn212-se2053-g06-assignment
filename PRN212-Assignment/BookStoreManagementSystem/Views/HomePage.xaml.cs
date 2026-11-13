using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BookStoreManagementSystem.Models;
using BookStoreManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BookStoreManagementSystem
{
    public partial class HomePage : Window
    {
        private List<BookCardViewModel> _allBooks = new();
        private List<CategoryFilterItem> _categoryItems = new();
        private List<OrderViewModel> _myOrders = new();
        private readonly ObservableCollection<CartItemViewModel> _cartItems = new();

        private BookCardViewModel? _selectedBookForDetail;
        private BookImageItemViewModel? _selectedGalleryImage;
        private BookCardViewModel? _selectedBookForRent;
        private bool _isLoaded = false;
        private int _homeCurrentPage = 1;
        private int _homeTotalPages = 1;
        private const int HomePageSize = 10;

        public HomePage()
        {
            InitializeComponent();
            icCartItems.ItemsSource = _cartItems;
            Loaded += HomePage_Loaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureRichSampleCatalogIfNeeded();
            UpdateUserHeaderUI();
            LoadDataFromDatabase();
            LoadUserCartFromDatabase();
        }

        private void UpdateUserHeaderUI()
        {
            headerControl?.RefreshAuthAndCart();

            if (SessionManager.CurrentUser != null)
            {
                if (!string.IsNullOrWhiteSpace(SessionManager.CurrentUser.PhoneNumber))
                {
                    txtCheckoutPhone.Text = SessionManager.CurrentUser.PhoneNumber;
                    txtRentPhone.Text = SessionManager.CurrentUser.PhoneNumber;
                }
                if (!string.IsNullOrWhiteSpace(SessionManager.CurrentUser.Address))
                {
                    txtCheckoutAddress.Text = SessionManager.CurrentUser.Address;
                    txtRentAddress.Text = SessionManager.CurrentUser.Address;
                }
            }
        }

        private void headerControl_HomeClicked(object? sender, EventArgs e)
        {
            headerControl.SetActiveNav("Home");
            _homeCurrentPage = 1;
            btnResetFilters_Click(this, new RoutedEventArgs());
        }

        private void headerControl_RentalsClicked(object? sender, EventArgs e)
        {
            headerControl.SetActiveNav("Rentals");
            _homeCurrentPage = 1;
            if (cbBookStatus != null) cbBookStatus.SelectedIndex = 2; // "Cho thuê"
        }

        private void headerControl_FeaturedClicked(object? sender, EventArgs e)
        {
            headerControl.SetActiveNav("Featured");
            _homeCurrentPage = 1;
            if (cbBookStatus != null) cbBookStatus.SelectedIndex = 0;
            if (cbSortBy != null) cbSortBy.SelectedIndex = 2; // Giá: Cao đến thấp
        }

        private void headerControl_PromotionsClicked(object? sender, EventArgs e)
        {
            headerControl.SetActiveNav("Promotions");
            _homeCurrentPage = 1;
            if (rbPriceUnder100 != null) rbPriceUnder100.IsChecked = true;
            MessageBox.Show(
                "🎁 ƯU ĐÃI ĐANG ÁP DỤNG TẠI G07 BOOKSTORE:\n\n" +
                "• Mã BOOK10: Giảm 10% cho mọi đơn hàng\n" +
                "• Mã SINHVIEN: Giảm 15% cho đơn thuê sách\n" +
                "• Mã VIP50K: Giảm trực tiếp 50.000 đ cho đơn từ 300.000 đ\n" +
                "• Mã FREESHIP: Miễn phí giao hàng toàn quốc\n\n" +
                "Nhập mã trực tiếp tại màn hình Giỏ Hàng & Thanh Toán!",
                "Chương Trình Khuyến Mãi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void headerControl_SearchClicked(object? sender, EventArgs e)
        {
            if (gridHeaderSearchBar.Visibility == Visibility.Visible)
            {
                gridHeaderSearchBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                gridHeaderSearchBar.Visibility = Visibility.Visible;
                txtSearch.Focus();
            }
        }

        public static void EnsureRichSampleCatalogIfNeeded()
        {
            try
            {
                using var context = new BookStoreDbContext();
                EnsureValidBookCoversInDatabase(context);
                if (context.Books.Count() <= 3)
                {
                    var extraCategories = new (string Name, string Desc)[]
                    {
                        ("Văn học - Tiểu thuyết", "Tác phẩm văn học kinh điển trong nước và quốc tế"),
                        ("Kỹ năng sống - Tâm lý", "Sách phát triển bản thân, tư duy phản biện và tâm lý học"),
                        ("Khoa học - Lịch sử", "Khám phá vũ trụ, lịch sử nhân loại và tri thức khoa học")
                    };

                    foreach (var c in extraCategories)
                    {
                        if (!context.Categories.Any(x => x.CategoryName == c.Name))
                        {
                            context.Categories.Add(new Category { CategoryName = c.Name, Description = c.Desc });
                        }
                    }
                    context.SaveChanges();

                    var catMap = context.Categories.ToDictionary(c => c.CategoryName, c => c.CategoryId);
                    int defaultCatId = catMap.Values.FirstOrDefault();

                var starterBooks = new[]
                    {
                        new Book
                        {
                            CategoryId = catMap.GetValueOrDefault("Công nghệ thông tin", defaultCatId),
                            Title = "The Pragmatic Programmer",
                            Author = "Andrew Hunt & David Thomas",
                            Publisher = "Addison-Wesley",
                            PublicationYear = 2019,
                            Isbn = "9780135957059",
                            Price = 520000m,
                            StockQuantity = 18,
                            BookStatus = "Bán",
                            Description = "Cuốn sách gối đầu giường giúp lập trình viên rèn luyện tư duy thiết kế phần mềm chuyên nghiệp."
                        },
                        new Book
                        {
                            CategoryId = catMap.GetValueOrDefault("Công nghệ thông tin", defaultCatId),
                            Title = "Designing Data-Intensive Applications",
                            Author = "Martin Kleppmann",
                            Publisher = "O'Reilly Media",
                            PublicationYear = 2017,
                            Isbn = "9781449373320",
                            Price = 650000m,
                            StockQuantity = 10,
                            BookStatus = "Cho thuê",
                            Description = "Kiến thức cốt lõi về kiến trúc hệ thống phân tán, cơ sở dữ liệu lớn và xử lý dữ liệu hiệu năng cao."
                        },
                        new Book
                        {
                            CategoryId = catMap.GetValueOrDefault("Kinh tế - Khởi nghiệp", defaultCatId),
                            Title = "Khởi Nghiệp Tinh Gọn (The Lean Startup)",
                            Author = "Eric Ries",
                            Publisher = "NXB Trẻ",
                            PublicationYear = 2021,
                            Isbn = "9786041182011",
                            Price = 155000m,
                            StockQuantity = 25,
                            BookStatus = "Bán",
                            Description = "Phương pháp xây dựng doanh nghiệp khởi nghiệp hiện đại, tối ưu sản phẩm và kiểm chứng thị trường."
                        },
                        new Book
                        {
                            CategoryId = catMap.GetValueOrDefault("Kinh tế - Khởi nghiệp", defaultCatId),
                            Title = "Tâm Lý Học Về Tiền (The Psychology of Money)",
                            Author = "Morgan Housel",
                            Publisher = "NXB Dân Trí",
                            PublicationYear = 2022,
                            Isbn = "9786043317015",
                            Price = 189000m,
                            StockQuantity = 12,
                            BookStatus = "Cho thuê",
                            Description = "19 câu chuyện sâu sắc về cách con người ứng xử với tiền bạc, đầu tư và hạnh phúc tài chính."
                        },
                        new Book
                        {
                            CategoryId = catMap.GetValueOrDefault("Văn học - Tiểu thuyết", defaultCatId),
                            Title = "Nhà Giả Kim (The Alchemist)",
                            Author = "Paulo Coelho",
                            Publisher = "NXB Hội Nhà Văn",
                            PublicationYear = 2020,
                            Isbn = "9786049916441",
                            Price = 85000m,
                            StockQuantity = 35,
                            BookStatus = "Bán",
                            Description = "Hành trình đi tìm kho báu và theo đuổi ước mơ của chàng chăn cừu Santiago."
                        },
                        new Book
                        {
                            CategoryId = catMap.GetValueOrDefault("Kỹ năng sống - Tâm lý", defaultCatId),
                            Title = "Tư Duy Nhanh Và Chậm (Thinking, Fast and Slow)",
                            Author = "Daniel Kahneman",
                            Publisher = "NXB Thế Giới",
                            PublicationYear = 2021,
                            Isbn = "9786047791026",
                            Price = 245000m,
                            StockQuantity = 14,
                            BookStatus = "Cho thuê",
                            Description = "Khám phá hai hệ thống tư duy chi phối nhận thức và quyết định của con người."
                        },
                        new Book
                        {
                            CategoryId = catMap.GetValueOrDefault("Khoa học - Lịch sử", defaultCatId),
                            Title = "Sapiens: Lược Sử Loài Người",
                            Author = "Yuval Noah Harari",
                            Publisher = "NXB Tri Thức",
                            PublicationYear = 2021,
                            Isbn = "9786049436253",
                            Price = 260000m,
                            StockQuantity = 22,
                            BookStatus = "Bán",
                            Description = "Toàn cảnh lịch sử tiến hóa, văn minh và tương lai của loài người."
                        }
                    };

                    foreach (var b in starterBooks)
                    {
                        if (!context.Books.Any(x => x.Isbn == b.Isbn))
                        {
                            context.Books.Add(b);
                        }
                    }
                    context.SaveChanges();
                }
            }
            catch
            {
                // Ignore if DB connection isn't ready yet; LoadDataFromDatabase will report if needed
            }
        }

        public static void EnsureValidBookCoversInDatabase(BookStoreDbContext context)
        {
            var allBooks = context.Books
                .Include(b => b.Category)
                .Include(b => b.BookImages)
                .ToList();

            bool changed = false;
            foreach (var book in allBooks)
            {
                var invalidImgs = book.BookImages.Where(i => i.ImageData == null || i.ImageData.Length < 32).ToList();
                if (invalidImgs.Any())
                {
                    context.BookImages.RemoveRange(invalidImgs);
                    foreach (var inv in invalidImgs)
                    {
                        book.BookImages.Remove(inv);
                    }
                    changed = true;
                }

                if (!book.BookImages.Any())
                {
                    var pngBytes = BookCardViewModel.GenerateDefaultCoverPng(
                        book.Title,
                        book.Author,
                        book.Category?.CategoryName ?? "BookStore",
                        book.BookId);

                    if (pngBytes.Length > 0)
                    {
                        context.BookImages.Add(new BookImage
                        {
                            BookId = book.BookId,
                            ImageData = pngBytes,
                            MimeType = "image/png",
                            IsCover = true,
                            CreatedAt = DateTime.UtcNow
                        });
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                context.SaveChanges();
            }
        }

        private void LoadDataFromDatabase()
        {
            try
            {
                using var context = new BookStoreDbContext();

                // 1. Load Categories with Book counts
                var categoriesFromDb = context.Categories
                    .AsNoTracking()
                    .Include(c => c.Books)
                    .OrderBy(c => c.CategoryName)
                    .ToList();

                var totalBooksCount = context.Books.Count();

                _categoryItems = new List<CategoryFilterItem>
                {
                    new CategoryFilterItem
                    {
                        CategoryId = null,
                        Name = "Tất cả thể loại",
                        Count = totalBooksCount
                    }
                };

                foreach (var cat in categoriesFromDb)
                {
                    _categoryItems.Add(new CategoryFilterItem
                    {
                        CategoryId = cat.CategoryId,
                        Name = cat.CategoryName,
                        Count = cat.Books.Count
                    });
                }

                lstCategories.ItemsSource = _categoryItems;
                if (lstCategories.SelectedIndex < 0)
                {
                    lstCategories.SelectedIndex = 0;
                }

                // 2. Load Books with category and cover image
                EnsureValidBookCoversInDatabase(context);
                var booksFromDb = context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .Include(b => b.BookImages)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();

                _allBooks = booksFromDb.Select(BookCardViewModel.FromEntity).ToList();

                // 3. Apply filters and render
                _isLoaded = true;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải dữ liệu từ CSDL: {ex.Message}\n\nVui lòng kiểm tra lại dịch vụ SQL Server và chuỗi kết nối trong appsettings.json.", 
                                "Lỗi kết nối CSDL", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        private void LoadUserCartFromDatabase()
        {
            if (SessionManager.CurrentUser == null)
            {
                UpdateCartBadge();
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var cart = context.Carts
                    .AsNoTracking()
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Book)
                    .FirstOrDefault(c => c.UserId == SessionManager.CurrentUser.UserId);

                if (cart != null && cart.CartItems.Any())
                {
                    _cartItems.Clear();
                    foreach (var ci in cart.CartItems)
                    {
                        if (ci.Book != null)
                        {
                            bool isRent = string.Equals(ci.Book.BookStatus, "Cho thuê", StringComparison.OrdinalIgnoreCase);
                            decimal rent7Price = Math.Max(10000m, Math.Round(ci.Book.Price * 0.15m / 1000m, 0) * 1000m);

                            _cartItems.Add(new CartItemViewModel
                            {
                                BookId = ci.BookId,
                                Title = ci.Book.Title,
                                OriginalBookPrice = ci.Book.Price,
                                UnitPrice = isRent ? rent7Price : ci.Book.Price,
                                Quantity = ci.Quantity,
                                OrderType = isRent ? "Thuê" : "Mua",
                                RentalDays = isRent ? 7 : 0,
                                CoverImage = _allBooks.FirstOrDefault(b => b.BookId == ci.BookId)?.CoverImage
                            });
                        }
                    }
                    icCartItems.Items.Refresh();
                }
                UpdateCartBadge();
            }
            catch
            {
                UpdateCartBadge();
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

                // Group by BookId because UQ_Cart_Book is unique on (cartId, bookId)
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
                // Ignore sync failure if offline
            }
        }

        private void ApplyFilters()
        {
            if (!_isLoaded || _allBooks == null || icBooks == null) return;

            IEnumerable<BookCardViewModel> query = _allBooks;

            // 1. Search Query
            var keyword = txtSearch?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(b => 
                    b.Title.ToLower().Contains(keyword) || 
                    b.Author.ToLower().Contains(keyword) || 
                    b.CategoryName.ToLower().Contains(keyword) ||
                    (!string.IsNullOrEmpty(b.Publisher) && b.Publisher.ToLower().Contains(keyword)));
            }

            // 2. Category Filter
            if (lstCategories?.SelectedItem is CategoryFilterItem selectedCat && selectedCat.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == selectedCat.CategoryId.Value);
            }

            // 3. Price Filter
            if (rbPriceUnder100?.IsChecked == true)
            {
                query = query.Where(b => b.Price < 100000);
            }
            else if (rbPrice100To300?.IsChecked == true)
            {
                query = query.Where(b => b.Price >= 100000 && b.Price <= 300000);
            }
            else if (rbPriceAbove300?.IsChecked == true)
            {
                query = query.Where(b => b.Price > 300000);
            }

            // 4. Status Filter
            if (cbBookStatus?.SelectedItem is ComboBoxItem selectedStatusItem)
            {
                var statusText = selectedStatusItem.Content?.ToString();
                if (!string.IsNullOrEmpty(statusText) && statusText != "Tất cả hình thức")
                {
                    query = query.Where(b => b.BookStatus.Equals(statusText, StringComparison.OrdinalIgnoreCase));
                }
            }

            // 5. Stock Filter
            if (chkInStockOnly?.IsChecked == true)
            {
                query = query.Where(b => b.IsInStock);
            }

            // 6. Sorting
            var sortIndex = cbSortBy?.SelectedIndex ?? 0;
            query = sortIndex switch
            {
                1 => query.OrderBy(b => b.Price),
                2 => query.OrderByDescending(b => b.Price),
                3 => query.OrderBy(b => b.Title),
                4 => query.OrderByDescending(b => b.Title),
                _ => query.OrderByDescending(b => b.CreatedAt)
            };

            var filteredList = query.ToList();

            // Render to UI
            int totalFiltered = filteredList.Count;
            _homeTotalPages = Math.Max(1, (int)Math.Ceiling((double)totalFiltered / HomePageSize));
            if (_homeCurrentPage > _homeTotalPages) _homeCurrentPage = _homeTotalPages;
            if (_homeCurrentPage < 1) _homeCurrentPage = 1;

            var pagedList = filteredList
                .Skip((_homeCurrentPage - 1) * HomePageSize)
                .Take(HomePageSize)
                .ToList();

            icBooks.ItemsSource = pagedList;

            if (txtHomePageIndicator != null && btnHomePrevPage != null && btnHomeNextPage != null)
            {
                txtHomePageIndicator.Text = $"Trang {_homeCurrentPage} / {_homeTotalPages}";
                btnHomePrevPage.IsEnabled = _homeCurrentPage > 1;
                btnHomeNextPage.IsEnabled = _homeCurrentPage < _homeTotalPages;
            }

            if (panelEmptyState != null)
            {
                panelEmptyState.Visibility = filteredList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void btnHomePrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_homeCurrentPage > 1)
            {
                _homeCurrentPage--;
                ApplyFilters();
            }
        }

        private void btnHomeNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_homeCurrentPage < _homeTotalPages)
            {
                _homeCurrentPage++;
                ApplyFilters();
            }
        }

        // --- Filter Event Handlers ---
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrEmpty(txtSearch.Text);
            txtSearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
            btnClearSearch.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilters();
        }

        private void btnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
        }

        private void lstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void cbBookStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void cbSortBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void btnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            if (lstCategories != null && lstCategories.Items.Count > 0)
            {
                lstCategories.SelectedIndex = 0;
            }
            if (rbPriceAll != null) rbPriceAll.IsChecked = true;
            if (chkInStockOnly != null) chkInStockOnly.IsChecked = false;
            if (cbBookStatus != null) cbBookStatus.SelectedIndex = 0;
            if (cbSortBy != null) cbSortBy.SelectedIndex = 0;
            ApplyFilters();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDataFromDatabase();
        }

        // --- Book Detail Modal ---
        // --- Book Detail Modal & Admin Image Upload ---
        private void btnAdminUploadCardImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                if (BookCardViewModel.PromptAndUploadBookImages(book.BookId, book.Title, setAsPrimaryCover: true, out int uploadedCount))
                {
                    LoadDataFromDatabase();
                    LoadUserCartFromDatabase();
                    MessageBox.Show(
                        $"✅ Đã tải lên {uploadedCount} ảnh cho sách '{book.Title}' thành công!",
                        "Cập nhật ảnh sách",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }

        private void btnViewDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                var detailWin = new BookDetailWindow(book.BookId, "Home");
                detailWin.Show();
                Close();
            }
        }

        private void ShowBookDetailModal(BookCardViewModel book)
        {
            _selectedBookForDetail = book;
            _selectedGalleryImage = book.GalleryImages.FirstOrDefault(i => i.IsCover) ?? book.GalleryImages.FirstOrDefault();

            txtDetailTitle.Text = book.Title;
            txtDetailAuthor.Text = $"Tác giả: {book.Author}";
            txtDetailCategory.Text = book.CategoryName;
            txtDetailStatus.Text = book.BookStatus;
            borderDetailStatus.Background = new BrushConverter().ConvertFromString(book.StatusBadgeColor) as Brush;
            txtDetailPrice.Text = book.FormattedPrice;
            txtDetailRentPrice.Text = $"{book.RentalPrice7Days:N0} đ / tuần";
            txtDetailStock.Text = book.StockStatusText;
            txtDetailPublisher.Text = string.IsNullOrEmpty(book.Publisher) ? "Đang cập nhật" : book.Publisher;
            txtDetailYear.Text = book.PublicationYear.HasValue ? book.PublicationYear.Value.ToString() : "Đang cập nhật";
            txtDetailIsbn.Text = string.IsNullOrEmpty(book.Isbn) ? "Đang cập nhật" : book.Isbn;
            txtDetailDescription.Text = book.Description;

            imgDetailCover.Source = _selectedGalleryImage?.ImageSource ?? book.CoverImage;
            txtDetailImageCount.Text = $"🖼️ Album ảnh ({book.GalleryImages.Count})";
            icDetailGallery.ItemsSource = book.GalleryImages;

            panelAdminDetailImageControls.Visibility = SessionManager.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnDetailSetSelectedAsCover.IsEnabled = _selectedGalleryImage != null;
            btnDetailDeleteSelectedImage.IsEnabled = _selectedGalleryImage != null;

            btnDetailAddToCart.IsEnabled = book.IsInStock;
            btnDetailRentBook.IsEnabled = book.IsInStock;

            gridDetailModal.Visibility = Visibility.Visible;
        }

        private void RefreshHomeDetailModalBook(int bookId)
        {
            LoadDataFromDatabase();
            LoadUserCartFromDatabase();
            var updatedBook = _allBooks.FirstOrDefault(b => b.BookId == bookId);
            if (updatedBook != null)
            {
                ShowBookDetailModal(updatedBook);
            }
        }

        private void btnSelectGalleryThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookImageItemViewModel imgItem)
            {
                _selectedGalleryImage = imgItem;
                imgDetailCover.Source = imgItem.ImageSource;
                btnDetailSetSelectedAsCover.IsEnabled = true;
                btnDetailDeleteSelectedImage.IsEnabled = true;
            }
        }

        private void btnDetailUploadImages_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail == null) return;
            int bookId = _selectedBookForDetail.BookId;

            if (BookCardViewModel.PromptAndUploadBookImages(bookId, _selectedBookForDetail.Title, setAsPrimaryCover: false, out int count))
            {
                RefreshHomeDetailModalBook(bookId);
                MessageBox.Show($"✅ Đã thêm {count} ảnh vào album sách '{_selectedBookForDetail?.Title}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnDetailChangeCover_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail == null) return;
            int bookId = _selectedBookForDetail.BookId;

            if (BookCardViewModel.PromptAndUploadBookImages(bookId, _selectedBookForDetail.Title, setAsPrimaryCover: true, out _))
            {
                RefreshHomeDetailModalBook(bookId);
                MessageBox.Show($"✅ Đã cập nhật ảnh bìa chính cho sách '{_selectedBookForDetail?.Title}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnDetailSetSelectedAsCover_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail == null || _selectedGalleryImage == null) return;
            int bookId = _selectedBookForDetail.BookId;

            if (BookCardViewModel.SetCoverImageInDatabase(bookId, _selectedGalleryImage.ImageId))
            {
                RefreshHomeDetailModalBook(bookId);
                MessageBox.Show("⭐ Đã đặt ảnh đang chọn làm ảnh bìa chính!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnDetailDeleteSelectedImage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail == null || _selectedGalleryImage == null) return;

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa bức ảnh này khỏi sách không?", "Xác nhận xóa ảnh", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            int bookId = _selectedBookForDetail.BookId;
            if (BookCardViewModel.DeleteImageFromDatabase(bookId, _selectedGalleryImage.ImageId))
            {
                RefreshHomeDetailModalBook(bookId);
            }
        }

        private void btnViewDetail_LegacyUnused(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                _selectedBookForDetail = book;

                txtDetailTitle.Text = book.Title;
                txtDetailAuthor.Text = $"Tác giả: {book.Author}";
                txtDetailCategory.Text = book.CategoryName;
                txtDetailStatus.Text = book.BookStatus;
                borderDetailStatus.Background = new BrushConverter().ConvertFromString(book.StatusBadgeColor) as Brush;
                txtDetailPrice.Text = book.FormattedPrice;
                txtDetailRentPrice.Text = $"{book.RentalPrice7Days:N0} đ / tuần";
                txtDetailStock.Text = book.StockStatusText;
                txtDetailPublisher.Text = string.IsNullOrEmpty(book.Publisher) ? "Đang cập nhật" : book.Publisher;
                txtDetailYear.Text = book.PublicationYear.HasValue ? book.PublicationYear.Value.ToString() : "Đang cập nhật";
                txtDetailIsbn.Text = string.IsNullOrEmpty(book.Isbn) ? "Đang cập nhật" : book.Isbn;
                txtDetailDescription.Text = book.Description;

                btnDetailAddToCart.IsEnabled = book.IsInStock;
                btnDetailRentBook.IsEnabled = book.IsInStock;

                gridDetailModal.Visibility = Visibility.Visible;
            }
        }

        private void btnCloseDetail_Click(object sender, RoutedEventArgs e)
        {
            gridDetailModal.Visibility = Visibility.Collapsed;
            _selectedBookForDetail = null;
        }

        private void btnDetailAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail != null)
            {
                AddToCart(_selectedBookForDetail, orderType: "Mua", rentalDays: 0, unitPrice: _selectedBookForDetail.Price);
                gridDetailModal.Visibility = Visibility.Collapsed;
            }
        }

        private void btnDetailRentBook_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail != null)
            {
                var book = _selectedBookForDetail;
                gridDetailModal.Visibility = Visibility.Collapsed;
                OpenRentModalForBook(book);
            }
        }

        // --- Book Rental Registration Modal ---
        private void btnOpenRentModal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                OpenRentModalForBook(book);
            }
        }

        private void OpenRentModalForBook(BookCardViewModel book)
        {
            if (!book.IsInStock)
            {
                MessageBox.Show("Quyển sách này hiện đang hết hàng hoặc đã được khách khác thuê!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _selectedBookForRent = book;
            txtRentBookTitle.Text = book.Title;
            txtRentBookOriginPrice.Text = $"Tác giả: {book.Author} • Giá bìa gốc: {book.FormattedPrice} • Kho còn: {book.StockQuantity} cuốn";
            rbRent7Days.IsChecked = true;
            UpdateRentalCalculationUI();
            gridRentModal.Visibility = Visibility.Visible;
        }

        private void btnCloseRentModal_Click(object sender, RoutedEventArgs e)
        {
            gridRentModal.Visibility = Visibility.Collapsed;
            _selectedBookForRent = null;
        }

        private void RentOption_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRentalCalculationUI();
        }

        private (int Days, decimal Fee) GetSelectedRentalPackage()
        {
            if (_selectedBookForRent == null) return (7, 0m);

            if (rbRent14Days?.IsChecked == true)
                return (14, _selectedBookForRent.RentalPrice14Days);
            if (rbRent30Days?.IsChecked == true)
                return (30, _selectedBookForRent.RentalPrice30Days);

            return (7, _selectedBookForRent.RentalPrice7Days);
        }

        private void UpdateRentalCalculationUI()
        {
            if (_selectedBookForRent == null || txtRentCalculatedFee == null || txtRentDueDate == null) return;

            var (days, fee) = GetSelectedRentalPackage();
            txtRentCalculatedFee.Text = $"{fee:N0} đ ({days} ngày)";
            txtRentDueDate.Text = DateTime.Now.AddDays(days).ToString("dd/MM/yyyy");
        }

        private void btnAddRentToCart_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForRent == null) return;

            var (days, fee) = GetSelectedRentalPackage();
            AddToCart(_selectedBookForRent, orderType: "Thuê", rentalDays: days, unitPrice: fee);
            gridRentModal.Visibility = Visibility.Collapsed;
            MessageBox.Show($"📅 Đã thêm sách thuê '{_selectedBookForRent.Title}' (Gói {days} ngày - {fee:N0} đ) vào giỏ hàng!", "Đã thêm vào giỏ", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnConfirmRentNow_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForRent == null) return;

            var phone = txtRentPhone.Text.Trim();
            var address = txtRentAddress.Text.Trim();

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(address))
            {
                MessageBox.Show("Vui lòng nhập Số điện thoại và Địa chỉ nhận/trả sách thuê!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (days, fee) = GetSelectedRentalPackage();
            var dueDateStr = DateTime.Now.AddDays(days).ToString("dd/MM/yyyy");

            try
            {
                using var context = new BookStoreDbContext();
                int userId = EnsureCustomerUserId(context);

                var dbBook = context.Books.FirstOrDefault(b => b.BookId == _selectedBookForRent.BookId);
                if (dbBook == null || dbBook.StockQuantity <= 0)
                {
                    MessageBox.Show("Sách này vừa hết hàng trong kho!", "Không đủ tồn kho", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
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
                if (dbBook.StockQuantity == 0 || string.Equals(dbBook.BookStatus, "Cho thuê", StringComparison.OrdinalIgnoreCase))
                {
                    if (dbBook.StockQuantity == 0)
                    {
                        dbBook.BookStatus = "Đã thuê";
                    }
                }

                context.SaveChanges();

                gridRentModal.Visibility = Visibility.Collapsed;
                LoadDataFromDatabase();

                MessageBox.Show(
                    $"🎉 Đăng ký thuê sách thành công!\n\n" +
                    $"• Mã đơn thuê: #ORD-{rentalOrder.OrderId:D4}\n" +
                    $"• Sách thuê: {dbBook.Title}\n" +
                    $"• Kỳ hạn: {days} ngày (Hạn trả: {dueDateStr})\n" +
                    $"• Phí thuê: {fee:N0} đ\n\n" +
                    $"Bạn có thể xem và bấm 'Trả Sách' bất cứ lúc nào tại mục '📦 Đơn Mua & Thuê'.",
                    "Thuê sách thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tạo đơn thuê sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Cart Management ---
        private void btnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                if (book.IsForRent)
                {
                    // Sách đang để chế độ Cho thuê nhưng khách bấm nút Mua -> vẫn cho phép mua theo giá bìa
                    AddToCart(book, orderType: "Mua", rentalDays: 0, unitPrice: book.Price);
                }
                else
                {
                    AddToCart(book, orderType: "Mua", rentalDays: 0, unitPrice: book.Price);
                }
            }
        }

        private void AddToCart(BookCardViewModel book, string orderType, int rentalDays, decimal unitPrice)
        {
            if (!book.IsInStock)
            {
                MessageBox.Show("Quyển sách này hiện đã hết hàng hoặc đang được thuê!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var totalInCartForBook = _cartItems.Where(c => c.BookId == book.BookId).Sum(c => c.Quantity);
            if (totalInCartForBook >= book.StockQuantity)
            {
                MessageBox.Show($"Số lượng trong giỏ hàng đã đạt giới hạn tồn kho ({book.StockQuantity} cuốn)!", "Giới hạn tồn kho", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingItem = _cartItems.FirstOrDefault(c => c.BookId == book.BookId && c.OrderType == orderType && c.RentalDays == rentalDays);
            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                _cartItems.Add(new CartItemViewModel
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    OriginalBookPrice = book.Price,
                    UnitPrice = unitPrice,
                    Quantity = 1,
                    OrderType = orderType,
                    RentalDays = rentalDays,
                    CoverImage = book.CoverImage
                });
            }

            icCartItems.Items.Refresh();
            UpdateCartBadge();
            SyncCartToDatabase();
        }

        private void UpdateCartBadge()
        {
            var totalCount = _cartItems.Sum(x => x.Quantity);
            headerControl?.SetCartCount(totalCount);

            var totalAmount = _cartItems.Sum(x => x.SubTotal);
            txtCartTotalAmount.Text = totalAmount.ToString("N0") + " đ";

            panelEmptyCart.Visibility = _cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnOpenCart_Click(object sender, RoutedEventArgs e)
        {
            UpdateCartBadge();
            var cartWin = new CartCheckoutWindow();
            cartWin.Show();
            Close();
        }

        private void btnCloseCart_Click(object sender, RoutedEventArgs e)
        {
            gridCartModal.Visibility = Visibility.Collapsed;
        }

        private void btnIncreaseQty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                var book = _allBooks.FirstOrDefault(b => b.BookId == item.BookId);
                var maxStock = book?.StockQuantity ?? 99;
                var currentTotalForBook = _cartItems.Where(c => c.BookId == item.BookId).Sum(c => c.Quantity);

                if (currentTotalForBook < maxStock)
                {
                    item.Quantity++;
                    icCartItems.Items.Refresh();
                    UpdateCartBadge();
                    SyncCartToDatabase();
                }
                else
                {
                    MessageBox.Show($"Kho chỉ còn {maxStock} cuốn!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void btnDecreaseQty_Click(object sender, RoutedEventArgs e)
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
                icCartItems.Items.Refresh();
                UpdateCartBadge();
                SyncCartToDatabase();
            }
        }

        private void btnRemoveCartItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                _cartItems.Remove(item);
                icCartItems.Items.Refresh();
                UpdateCartBadge();
                SyncCartToDatabase();
            }
        }

        private int EnsureCustomerUserId(BookStoreDbContext context)
        {
            if (SessionManager.CurrentUser != null)
            {
                return SessionManager.CurrentUser.UserId;
            }

            // Nếu đang ở chế độ Khách vãng lai, lấy tài khoản Customer mặc định trong DB (hoặc tạo mới)
            var defaultCustomer = context.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.RoleId == 1)
                ?? context.Users.Include(u => u.Role).FirstOrDefault();

            if (defaultCustomer != null)
            {
                SessionManager.CurrentUser = defaultCustomer;
                UpdateUserHeaderUI();
                return defaultCustomer.UserId;
            }

            var role = context.Roles.FirstOrDefault() ?? new Role { RoleName = "Customer" };
            if (role.RoleId == 0)
            {
                context.Roles.Add(role);
                context.SaveChanges();
            }

            var newGuestUser = new User
            {
                RoleId = role.RoleId,
                FullName = "Khách hàng BookStore",
                Email = "khachhang@bookstore.com",
                PasswordHash = "123456",
                PhoneNumber = txtCheckoutPhone.Text.Trim(),
                Address = txtCheckoutAddress.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(newGuestUser);
            context.SaveChanges();

            SessionManager.CurrentUser = newGuestUser;
            UpdateUserHeaderUI();
            return newGuestUser.UserId;
        }

        private void btnCheckout_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0)
            {
                MessageBox.Show("Giỏ hàng của bạn đang trống! Vui lòng chọn sách trước khi thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var phone = txtCheckoutPhone.Text.Trim();
            var address = txtCheckoutAddress.Text.Trim();
            var paymentMethod = (cbCheckoutPaymentMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "COD";

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(address))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Số điện thoại và Địa chỉ giao sách!", "Thiếu thông tin giao hàng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var total = _cartItems.Sum(x => x.SubTotal);
            bool hasRentalItems = _cartItems.Any(x => x.OrderType == "Thuê");

            var result = MessageBox.Show(
                $"Xác nhận đặt đơn hàng gồm {_cartItems.Sum(x => x.Quantity)} cuốn sách với tổng số tiền {total:N0} đ?\n\n" +
                $"• Người nhận: {SessionManager.DisplayName} ({phone})\n" +
                $"• Địa chỉ: {address}\n" +
                $"• Thanh toán: {paymentMethod}", 
                "Xác nhận thanh toán", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var context = new BookStoreDbContext();
                int userId = EnsureCustomerUserId(context);

                int maxRentDays = _cartItems.Where(x => x.OrderType == "Thuê").Select(x => x.RentalDays).DefaultIfEmpty(0).Max();
                string finalAddress = hasRentalItems
                    ? $"[THUÊ {maxRentDays} NGÀY - Hạn trả: {DateTime.Now.AddDays(maxRentDays):dd/MM/yyyy}] {address}"
                    : address;

                var order = new Order
                {
                    UserId = userId,
                    TotalAmount = total,
                    OrderStatus = hasRentalItems ? "Renting" : "Pending",
                    ShippingAddress = finalAddress,
                    RecipientPhone = phone,
                    PaymentMethod = paymentMethod.Length > 50 ? paymentMethod.Substring(0, 50) : paymentMethod,
                    PaymentStatus = paymentMethod.Contains("COD", StringComparison.OrdinalIgnoreCase) ? "Unpaid" : "Paid",
                    CreatedAt = DateTime.UtcNow
                };

                context.Orders.Add(order);
                context.SaveChanges();

                foreach (var item in _cartItems)
                {
                    context.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.OrderId,
                        BookId = item.BookId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });

                    var dbBook = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                    if (dbBook != null)
                    {
                        dbBook.StockQuantity = Math.Max(0, dbBook.StockQuantity - item.Quantity);
                        if (item.OrderType == "Thuê" && dbBook.StockQuantity == 0)
                        {
                            dbBook.BookStatus = "Đã thuê";
                        }
                    }
                }

                // Clear DB Cart
                var userCart = context.Carts.Include(c => c.CartItems).FirstOrDefault(c => c.UserId == userId);
                if (userCart != null && userCart.CartItems.Any())
                {
                    context.CartItems.RemoveRange(userCart.CartItems);
                }

                context.SaveChanges();

                _cartItems.Clear();
                icCartItems.Items.Refresh();
                UpdateCartBadge();
                gridCartModal.Visibility = Visibility.Collapsed;
                LoadDataFromDatabase();

                MessageBox.Show(
                    $"🎉 Đặt hàng thành công!\n\n" +
                    $"• Mã hóa đơn: #ORD-{order.OrderId:D4}\n" +
                    $"• Tổng thanh toán: {total:N0} đ\n" +
                    $"• Trạng thái: {(hasRentalItems ? "Đang thuê sách (Renting)" : "Chờ xác nhận giao hàng (Pending)")}\n\n" +
                    $"Đơn hàng đã được lưu vào CSDL BookStoreDB. Bạn có thể kiểm tra tại mục '📦 Đơn Mua & Thuê'.",
                    "Thanh toán thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu đơn hàng vào CSDL:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== MY ORDERS & BOOK RENTALS MODAL ====================
        private void btnOpenMyOrders_Click(object sender, RoutedEventArgs e)
        {
            LoadCustomerOrders();
            var ordersWin = new CustomerOrdersWindow();
            ordersWin.Show();
            Close();
        }

        private void btnCloseMyOrders_Click(object sender, RoutedEventArgs e)
        {
            gridMyOrdersModal.Visibility = Visibility.Collapsed;
        }

        private void LoadCustomerOrders(string filterMode = "All")
        {
            try
            {
                using var context = new BookStoreDbContext();
                IQueryable<Order> query = context.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Book);

                if (SessionManager.CurrentUser != null && !SessionManager.IsAdmin)
                {
                    query = query.Where(o => o.UserId == SessionManager.CurrentUser.UserId);
                    txtMyOrdersSubtitle.Text = $"Đơn hàng của tài khoản: {SessionManager.CurrentUser.FullName} ({SessionManager.CurrentUser.Email})";
                }
                else
                {
                    txtMyOrdersSubtitle.Text = "Hiển thị toàn bộ đơn mua & sách thuê trên hệ thống";
                }

                var orders = query.OrderByDescending(o => o.CreatedAt).ToList();

                _myOrders = orders.Select(o => new OrderViewModel
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    CustomerName = o.User?.FullName ?? $"Khách #{o.UserId}",
                    CustomerEmail = o.User?.Email ?? "",
                    RecipientPhone = o.RecipientPhone,
                    ShippingAddress = o.ShippingAddress,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    PaymentMethod = o.PaymentMethod,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt,
                    TotalQuantity = o.OrderItems.Sum(i => i.Quantity),
                    ItemsSummary = string.Join(", ", o.OrderItems.Select(i => $"{i.Book?.Title} (x{i.Quantity})"))
                }).ToList();

                IEnumerable<OrderViewModel> filtered = _myOrders;
                if (filterMode == "Buy")
                    filtered = filtered.Where(o => !o.IsRentalOrder);
                else if (filterMode == "Rent")
                    filtered = filtered.Where(o => o.IsRentalOrder);

                dgCustomerOrders.ItemsSource = filtered.ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải lịch sử đơn hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnFilterOrdersAll_Click(object sender, RoutedEventArgs e) => LoadCustomerOrders("All");
        private void btnFilterOrdersBuy_Click(object sender, RoutedEventArgs e) => LoadCustomerOrders("Buy");
        private void btnFilterOrdersRent_Click(object sender, RoutedEventArgs e) => LoadCustomerOrders("Rent");

        private void btnCustomerReturnBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderViewModel orderVm)
            {
                if (MessageBox.Show(
                    $"Bạn xác nhận muốn hoàn trả sách cho đơn thuê {orderVm.OrderCode} ({orderVm.ItemsSummary})?",
                    "Xác nhận trả sách thuê",
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

                    order.OrderStatus = "Returned";
                    order.PaymentStatus = "Paid";

                    foreach (var item in order.OrderItems)
                    {
                        var book = context.Books.FirstOrDefault(b => b.BookId == item.BookId);
                        if (book != null)
                        {
                            book.StockQuantity += item.Quantity;
                            if (string.Equals(book.BookStatus, "Đã thuê", StringComparison.OrdinalIgnoreCase))
                            {
                                book.BookStatus = "Cho thuê";
                            }
                        }
                    }

                    context.SaveChanges();
                    LoadCustomerOrders();
                    LoadDataFromDatabase();

                    MessageBox.Show($"📥 Đã trả sách thành công cho đơn {orderVm.OrderCode}! Cảm ơn bạn đã sử dụng dịch vụ thuê sách của BookStore.", "Hoàn trả thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi hoàn trả sách:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnCustomerCancelOrder_Click(object sender, RoutedEventArgs e)
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
                    LoadCustomerOrders();
                    LoadDataFromDatabase();

                    MessageBox.Show($"Đã hủy đơn hàng {orderVm.OrderCode} và hoàn lại số lượng tồn kho!", "Đã hủy đơn", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi hủy đơn hàng:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ==================== ADMIN & AUTH NAVIGATION ====================
        private void btnOpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.IsAdmin)
            {
                // Tự động đăng nhập nhanh Admin hoặc hỏi người dùng
                try
                {
                    using var context = new BookStoreDbContext();
                    var adminUser = context.Users
                        .Include(u => u.Role)
                        .FirstOrDefault(u => u.RoleId == 2 || u.Role.RoleName == "Admin");

                    if (adminUser != null)
                    {
                        SessionManager.CurrentUser = adminUser;
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            var adminWin = new AdminDashboardWindow();
            adminWin.Show();
            Close();
        }

        private void btnOpenAllBooks_Click(object sender, RoutedEventArgs e)
        {
            var bookListWin = new BookListWindow();
            bookListWin.Show();
            Close();
        }

        private void btnOpenProfile_Click(object sender, RoutedEventArgs e)
        {
            var profileWin = new UserProfileWindow("Home");
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
