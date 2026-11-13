using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BookStoreManagementSystem.Models;
using BookStoreManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BookStoreManagementSystem
{
    public partial class BookListWindow : Window
    {
        private List<BookCardViewModel> _allBooks = new();
        private List<BookCardViewModel> _filteredBooks = new();
        private List<CategoryFilterItem> _categoryItems = new();

        private BookCardViewModel? _selectedBookForDetail;
        private BookImageItemViewModel? _selectedGalleryImage;
        private bool _isTableView = false;
        private bool _isLoaded = false;
        private bool _isUpdatingFilters = false;
        private string _statusFilterMode = "All";

        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages = 1;

        public BookListWindow()
        {
            InitializeComponent();
            Loaded += BookListWindow_Loaded;
        }

        private void BookListWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HomePage.EnsureRichSampleCatalogIfNeeded();
            UpdateUserHeaderUI();
            LoadAllBooksFromDatabase();
        }

        private void UpdateUserHeaderUI()
        {
            headerControl?.RefreshAuthAndCart();
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

        public void LoadAllBooksFromDatabase()
        {
            try
            {
                _isUpdatingFilters = true;
                using var context = new BookStoreDbContext();

                // 1. Load Categories
                var categoriesFromDb = context.Categories
                    .AsNoTracking()
                    .Include(c => c.Books)
                    .OrderBy(c => c.CategoryName)
                    .ToList();

                var booksFromDb = context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .Include(b => b.BookImages)
                    .OrderByDescending(b => b.CreatedAt)
                    .ThenByDescending(b => b.BookId)
                    .ToList();

                _categoryItems = new List<CategoryFilterItem>
                {
                    new CategoryFilterItem
                    {
                        CategoryId = null,
                        Name = "Tất cả thể loại",
                        Count = booksFromDb.Count
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

                int prevCatIndex = lstCategories.SelectedIndex;
                lstCategories.ItemsSource = _categoryItems;
                lstCategories.SelectedIndex = prevCatIndex >= 0 && prevCatIndex < _categoryItems.Count ? prevCatIndex : 0;

                // 2. Map Books to ViewModels (with full BookImages loaded)
                _allBooks = booksFromDb.Select(BookCardViewModel.FromEntity).ToList();

                // 3. Populate Dynamic Publisher & Publication Year Filters
                var publishers = _allBooks
                    .Select(b => b.Publisher?.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList();

                var publisherOptions = new List<string> { "Tất cả Nhà xuất bản" };
                publisherOptions.AddRange(publishers!);
                cbPublisherFilter.ItemsSource = publisherOptions;
                if (cbPublisherFilter.SelectedIndex < 0) cbPublisherFilter.SelectedIndex = 0;

                _isUpdatingFilters = false;
                _isLoaded = true;
                ApplyFiltersAndPagination();
            }
            catch (Exception ex)
            {
                _isUpdatingFilters = false;
                MessageBox.Show(
                    $"Không thể tải danh sách sách từ CSDL:\n{ex.Message}",
                    "Lỗi kết nối CSDL",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ApplyFiltersAndPagination(bool resetPage = false)
        {
            if (!_isLoaded || _isUpdatingFilters) return;

            if (resetPage)
            {
                _currentPage = 1;
            }

            IEnumerable<BookCardViewModel> query = _allBooks;

            // 1. Keyword Search
            var keyword = txtSearch?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(b =>
                    b.Title.ToLower().Contains(keyword) ||
                    b.Author.ToLower().Contains(keyword) ||
                    b.CategoryName.ToLower().Contains(keyword) ||
                    b.BookCode.ToLower().Contains(keyword) ||
                    (!string.IsNullOrEmpty(b.Isbn) && b.Isbn.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(b.Publisher) && b.Publisher.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(b.Description) && b.Description.ToLower().Contains(keyword)));
            }

            // 2. Category Filter
            if (lstCategories?.SelectedItem is CategoryFilterItem selectedCat && selectedCat.CategoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == selectedCat.CategoryId.Value);
            }

            // 3. Transaction Status Filter (Top Pills)
            if (_statusFilterMode != "All")
            {
                query = query.Where(b => b.BookStatus.Equals(_statusFilterMode, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Price Filter
            bool hasMinPrice = decimal.TryParse(txtMinPrice?.Text?.Trim(), out decimal minPrice) && minPrice >= 0;
            bool hasMaxPrice = decimal.TryParse(txtMaxPrice?.Text?.Trim(), out decimal maxPrice) && maxPrice > 0;

            if (hasMinPrice || hasMaxPrice)
            {
                if (hasMinPrice) query = query.Where(b => b.Price >= minPrice);
                if (hasMaxPrice) query = query.Where(b => b.Price <= maxPrice);
            }
            else
            {
                if (rbPriceUnder100?.IsChecked == true)
                    query = query.Where(b => b.Price < 100000m);
                else if (rbPrice100To250?.IsChecked == true)
                    query = query.Where(b => b.Price >= 100000m && b.Price <= 250000m);
                else if (rbPrice250To500?.IsChecked == true)
                    query = query.Where(b => b.Price > 250000m && b.Price <= 500000m);
                else if (rbPriceAbove500?.IsChecked == true)
                    query = query.Where(b => b.Price > 500000m);
            }

            // 5. Publisher Filter
            if (cbPublisherFilter?.SelectedItem is string selectedPublisher && selectedPublisher != "Tất cả Nhà xuất bản")
            {
                query = query.Where(b => string.Equals(b.Publisher?.Trim(), selectedPublisher, StringComparison.OrdinalIgnoreCase));
            }

            // 6. Publication Year Range Filter
            bool hasMinYear = int.TryParse(txtMinYear?.Text?.Trim(), out int minYear) && minYear > 0;
            bool hasMaxYear = int.TryParse(txtMaxYear?.Text?.Trim(), out int maxYear) && maxYear > 0;
            if (hasMinYear)
            {
                query = query.Where(b => b.PublicationYear.HasValue && b.PublicationYear.Value >= minYear);
            }
            if (hasMaxYear)
            {
                query = query.Where(b => b.PublicationYear.HasValue && b.PublicationYear.Value <= maxYear);
            }

            // 7. Sorting
            int sortIndex = cbSortBy?.SelectedIndex ?? 0;
            query = sortIndex switch
            {
                1 => query.OrderBy(b => b.Price),
                2 => query.OrderByDescending(b => b.Price),
                3 => query.OrderBy(b => b.Title),
                4 => query.OrderByDescending(b => b.Title),
                5 => query.OrderByDescending(b => b.PublicationYear ?? 0),
                _ => query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.BookId)
            };

            _filteredBooks = query.ToList();

            // 9. Pagination Calculation
            int totalFiltered = _filteredBooks.Count;
            _totalPages = Math.Max(1, (int)Math.Ceiling((double)totalFiltered / _pageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pagedBooks = _filteredBooks
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            // 10. Bind to Grid & Table Views
            icBooksGrid.ItemsSource = pagedBooks;
            dgBooksTable.ItemsSource = pagedBooks;

            txtResultSummary.Text = $"Tìm thấy: {totalFiltered} / {_allBooks.Count} đầu sách";
            txtPageIndicator.Text = $"Trang {_currentPage} / {_totalPages}";

            btnFirstPage.IsEnabled = _currentPage > 1;
            btnPrevPage.IsEnabled = _currentPage > 1;
            btnNextPage.IsEnabled = _currentPage < _totalPages;
            btnLastPage.IsEnabled = _currentPage < _totalPages;

            UpdateViewVisibility(totalFiltered == 0);
        }

        private void UpdateViewVisibility(bool isEmpty)
        {
            if (panelEmptyState == null || scrollGridView == null || borderTableView == null) return;

            if (isEmpty)
            {
                panelEmptyState.Visibility = Visibility.Visible;
                scrollGridView.Visibility = Visibility.Collapsed;
                borderTableView.Visibility = Visibility.Collapsed;
            }
            else
            {
                panelEmptyState.Visibility = Visibility.Collapsed;
                scrollGridView.Visibility = _isTableView ? Visibility.Collapsed : Visibility.Visible;
                borderTableView.Visibility = _isTableView ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ==================== VIEW MODE SWITCHER ====================
        private void btnViewGrid_Click(object sender, RoutedEventArgs e)
        {
            _isTableView = false;
            btnViewGrid.Style = (Style)FindResource("PrimaryButtonStyle");
            btnViewTable.Style = (Style)FindResource("SecondaryButtonStyle");
            UpdateViewVisibility(_filteredBooks.Count == 0);
        }

        private void btnViewTable_Click(object sender, RoutedEventArgs e)
        {
            _isTableView = true;
            btnViewTable.Style = (Style)FindResource("PrimaryButtonStyle");
            btnViewGrid.Style = (Style)FindResource("SecondaryButtonStyle");
            UpdateViewVisibility(_filteredBooks.Count == 0);
        }

        // ==================== FILTER & SORT HANDLERS ====================
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool hasText = !string.IsNullOrEmpty(txtSearch.Text);
            txtSearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
            btnClearSearch.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void btnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
        }

        private void lstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void FilterSelection_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void FilterCheck_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void CustomPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void cbSortBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFiltersAndPagination(resetPage: false);
        }

        private void btnPillAll_Click(object sender, RoutedEventArgs e)
        {
            _statusFilterMode = "All";
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void btnPillForSale_Click(object sender, RoutedEventArgs e)
        {
            _statusFilterMode = "Bán";
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void btnPillForRent_Click(object sender, RoutedEventArgs e)
        {
            _statusFilterMode = "Cho thuê";
            ApplyFiltersAndPagination(resetPage: true);
        }

        private void btnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingFilters = true;
            txtSearch.Text = string.Empty;
            txtSearchPlaceholder.Visibility = Visibility.Visible;
            btnClearSearch.Visibility = Visibility.Collapsed;

            _statusFilterMode = "All";
            if (lstCategories.Items.Count > 0) lstCategories.SelectedIndex = 0;
            if (rbPriceAll != null) rbPriceAll.IsChecked = true;
            txtMinPrice.Text = string.Empty;
            txtMaxPrice.Text = string.Empty;
            if (cbPublisherFilter.Items.Count > 0) cbPublisherFilter.SelectedIndex = 0;
            if (txtMinYear != null) txtMinYear.Text = string.Empty;
            if (txtMaxYear != null) txtMaxYear.Text = string.Empty;
            if (cbSortBy != null) cbSortBy.SelectedIndex = 0;
            _isUpdatingFilters = false;

            ApplyFiltersAndPagination(resetPage: true);
        }

        private void btnRefreshData_Click(object sender, RoutedEventArgs e)
        {
            LoadAllBooksFromDatabase();
        }

        // ==================== PAGINATION HANDLERS ====================
        private void btnFirstPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            ApplyFiltersAndPagination();
        }

        private void btnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                ApplyFiltersAndPagination();
            }
        }

        private void btnNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                ApplyFiltersAndPagination();
            }
        }

        private void btnLastPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = _totalPages;
            ApplyFiltersAndPagination();
        }

        // ==================== ADMIN IMAGE UPLOAD & BOOK DETAIL MODAL ====================
        private void btnAdminUploadCardImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                if (BookCardViewModel.PromptAndUploadBookImages(book.BookId, book.Title, setAsPrimaryCover: true, out int uploadedCount))
                {
                    LoadAllBooksFromDatabase();
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
                var detailWin = new BookDetailWindow(book.BookId, "BookList");
                detailWin.Show();
                Close();
            }
        }

        private void dgBooksTable_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgBooksTable.SelectedItem is BookCardViewModel book)
            {
                var detailWin = new BookDetailWindow(book.BookId, "BookList");
                detailWin.Show();
                Close();
            }
        }

        private void ShowBookDetailModal(BookCardViewModel book)
        {
            _selectedBookForDetail = book;
            _selectedGalleryImage = book.GalleryImages.FirstOrDefault(i => i.IsCover) ?? book.GalleryImages.FirstOrDefault();

            txtDetailBookCode.Text = book.BookCode;
            txtDetailTitle.Text = book.Title;
            txtDetailAuthor.Text = $"Tác giả: {book.Author}";
            txtDetailCategory.Text = book.CategoryName;
            txtDetailStatus.Text = book.BookStatus;
            borderDetailStatus.Background = new BrushConverter().ConvertFromString(book.StatusBadgeColor) as Brush;
            txtDetailStock.Text = book.StockStatusText;
            txtDetailCreatedAt.Text = $"Ngày nhập kho: {book.FormattedCreatedAt}";

            txtDetailPrice.Text = book.FormattedPrice;
            txtDetailRentPrice.Text = $"{book.RentalPrice7Days:N0} đ / {book.RentalPrice14Days:N0} đ";
            txtDetailRent30Price.Text = $"{book.RentalPrice30Days:N0} đ / tháng";

            txtDetailPublisher.Text = book.PublisherDisplay;
            txtDetailYear.Text = book.YearDisplay;
            txtDetailIsbn.Text = book.IsbnDisplay;
            txtDetailDescription.Text = book.Description;

            imgDetailCover.Source = _selectedGalleryImage?.ImageSource ?? book.CoverImage;
            txtDetailImageCount.Text = $"🖼️ Album ảnh ({book.GalleryImages.Count})";
            icDetailGallery.ItemsSource = book.GalleryImages;

            panelAdminDetailImageControls.Visibility = SessionManager.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnDetailSetSelectedAsCover.IsEnabled = _selectedGalleryImage != null;
            btnDetailDeleteSelectedImage.IsEnabled = _selectedGalleryImage != null;

            btnDetailAddToCart.IsEnabled = book.IsInStock;
            gridDetailModal.Visibility = Visibility.Visible;
        }

        private void RefreshDetailModalBook(int bookId)
        {
            LoadAllBooksFromDatabase();
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
                RefreshDetailModalBook(bookId);
                MessageBox.Show($"✅ Đã thêm {count} ảnh vào album sách '{_selectedBookForDetail?.Title}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnDetailChangeCover_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail == null) return;
            int bookId = _selectedBookForDetail.BookId;

            if (BookCardViewModel.PromptAndUploadBookImages(bookId, _selectedBookForDetail.Title, setAsPrimaryCover: true, out _))
            {
                RefreshDetailModalBook(bookId);
                MessageBox.Show($"✅ Đã cập nhật ảnh bìa chính cho sách '{_selectedBookForDetail?.Title}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnDetailSetSelectedAsCover_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail == null || _selectedGalleryImage == null) return;
            int bookId = _selectedBookForDetail.BookId;

            if (BookCardViewModel.SetCoverImageInDatabase(bookId, _selectedGalleryImage.ImageId))
            {
                RefreshDetailModalBook(bookId);
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
                RefreshDetailModalBook(bookId);
            }
        }

        private void btnCloseDetail_Click(object sender, RoutedEventArgs e)
        {
            gridDetailModal.Visibility = Visibility.Collapsed;
            _selectedBookForDetail = null;
            _selectedGalleryImage = null;
        }

        private void btnDetailAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBookForDetail != null)
            {
                var book = _selectedBookForDetail;
                gridDetailModal.Visibility = Visibility.Collapsed;
                AddBookToDatabaseCartAndNavigate(book);
            }
        }

        private void btnSelectBookAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                AddBookToDatabaseCartAndNavigate(book);
            }
        }

        private void AddBookToDatabaseCartAndNavigate(BookCardViewModel book)
        {
            if (!book.IsInStock)
            {
                MessageBox.Show(
                    $"Quyển sách '{book.Title}' hiện đang hết hàng hoặc đang được khách khác thuê!",
                    "Không khả dụng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                int? userId = SessionManager.CurrentUser?.UserId;

                if (!userId.HasValue)
                {
                    var defaultCustomer = context.Users
                        .Include(u => u.Role)
                        .FirstOrDefault(u => u.RoleId == 1)
                        ?? context.Users.Include(u => u.Role).FirstOrDefault();

                    if (defaultCustomer != null)
                    {
                        SessionManager.CurrentUser = defaultCustomer;
                        userId = defaultCustomer.UserId;
                        UpdateUserHeaderUI();
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

                    var existingItem = cart.CartItems.FirstOrDefault(ci => ci.BookId == book.BookId);
                    if (existingItem != null)
                    {
                        if (existingItem.Quantity < book.StockQuantity)
                        {
                            existingItem.Quantity += 1;
                        }
                    }
                    else
                    {
                        context.CartItems.Add(new CartItem
                        {
                            CartId = cart.CartId,
                            BookId = book.BookId,
                            Quantity = 1,
                            AddedAt = DateTime.UtcNow
                        });
                    }

                    cart.UpdatedAt = DateTime.UtcNow;
                    context.SaveChanges();
                }

                var result = MessageBox.Show(
                    $"✅ Đã thêm sách '{book.Title}' vào giỏ hàng!\n\nBạn có muốn chuyển sang màn Giỏ Hàng & Thanh Toán (/cart/checkout) ngay không?",
                    "Đã thêm vào giỏ hàng",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var cartWin = new CartCheckoutWindow();
                    cartWin.Show();
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể thêm vào giỏ hàng:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== NAVIGATION ====================
        private void btnBackHome_Click(object sender, RoutedEventArgs e)
        {
            var homeWin = new HomePage();
            homeWin.Show();
            Close();
        }

        private void btnOpenCart_Click(object sender, RoutedEventArgs e)
        {
            var cartWin = new CartCheckoutWindow();
            cartWin.Show();
            Close();
        }
    }
}
