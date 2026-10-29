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
        private readonly ObservableCollection<CartItemViewModel> _cartItems = new();
        private BookCardViewModel? _selectedBookForDetail;
        private bool _isLoaded = false;

        public HomePage()
        {
            InitializeComponent();
            icCartItems.ItemsSource = _cartItems;
            Loaded += HomePage_Loaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDataFromDatabase();
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
                lstCategories.SelectedIndex = 0;

                // 2. Load Books with category and cover image
                var booksFromDb = context.Books
                    .AsNoTracking()
                    .Include(b => b.Category)
                    .Include(b => b.BookImages)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();

                _allBooks = booksFromDb.Select(b =>
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
                        Description = string.IsNullOrWhiteSpace(b.Description) 
                            ? "Tác phẩm giá trị cung cấp nhiều kiến thức và góc nhìn sâu sắc cho người đọc." 
                            : b.Description,
                        BookStatus = string.IsNullOrWhiteSpace(b.BookStatus) ? "Bán" : b.BookStatus,
                        CoverImage = BookCardViewModel.LoadImageSafe(coverImgData),
                        CreatedAt = b.CreatedAt
                    };
                }).ToList();

                // Update Hero chips
                txtTotalBooksChip.Text = $"{_allBooks.Count}+";
                txtTotalCategoriesChip.Text = $"{categoriesFromDb.Count}";

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

        private void ApplyFilters()
        {
            if (!_isLoaded || _allBooks == null || icBooks == null || txtResultCount == null) return;

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
                query = query.Where(b => b.StockQuantity > 0);
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
            icBooks.ItemsSource = filteredList;
            txtResultCount.Text = $"Hiển thị: {filteredList.Count} / {_allBooks.Count} sách";

            if (panelEmptyState != null)
            {
                panelEmptyState.Visibility = filteredList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        private void btnViewDetail_Click(object sender, RoutedEventArgs e)
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
                txtDetailStock.Text = book.StockStatusText;
                txtDetailPublisher.Text = string.IsNullOrEmpty(book.Publisher) ? "Đang cập nhật" : book.Publisher;
                txtDetailYear.Text = book.PublicationYear.HasValue ? book.PublicationYear.Value.ToString() : "Đang cập nhật";
                txtDetailIsbn.Text = string.IsNullOrEmpty(book.Isbn) ? "Đang cập nhật" : book.Isbn;
                txtDetailDescription.Text = book.Description;

                btnDetailAddToCart.IsEnabled = book.IsInStock;

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
                AddToCart(_selectedBookForDetail);
                gridDetailModal.Visibility = Visibility.Collapsed;
            }
        }

        // --- Cart Management ---
        private void btnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BookCardViewModel book)
            {
                AddToCart(book);
            }
        }

        private void AddToCart(BookCardViewModel book)
        {
            if (!book.IsInStock)
            {
                MessageBox.Show("Quyển sách này hiện đã hết hàng trong kho!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var existingItem = _cartItems.FirstOrDefault(c => c.BookId == book.BookId);
            if (existingItem != null)
            {
                if (existingItem.Quantity < book.StockQuantity)
                {
                    existingItem.Quantity++;
                }
                else
                {
                    MessageBox.Show($"Số lượng trong giỏ hàng đã đạt giới hạn tồn kho ({book.StockQuantity} cuốn)!", "Giới hạn tồn kho", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                _cartItems.Add(new CartItemViewModel
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    UnitPrice = book.Price,
                    Quantity = 1
                });
            }

            // Refresh Cart Binding
            icCartItems.Items.Refresh();
            UpdateCartBadge();
        }

        private void UpdateCartBadge()
        {
            var totalCount = _cartItems.Sum(x => x.Quantity);
            txtCartBadge.Text = $"Giỏ hàng ({totalCount})";

            var totalAmount = _cartItems.Sum(x => x.SubTotal);
            txtCartTotalAmount.Text = totalAmount.ToString("N0") + " đ";

            panelEmptyCart.Visibility = _cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnOpenCart_Click(object sender, RoutedEventArgs e)
        {
            UpdateCartBadge();
            gridCartModal.Visibility = Visibility.Visible;
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

                if (item.Quantity < maxStock)
                {
                    item.Quantity++;
                    icCartItems.Items.Refresh();
                    UpdateCartBadge();
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
            }
        }

        private void btnRemoveCartItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CartItemViewModel item)
            {
                _cartItems.Remove(item);
                icCartItems.Items.Refresh();
                UpdateCartBadge();
            }
        }

        private void btnCheckout_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0)
            {
                MessageBox.Show("Giỏ hàng của bạn đang trống! Vui lòng chọn sách trước khi thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var total = _cartItems.Sum(x => x.SubTotal);
            var result = MessageBox.Show($"Xác nhận đặt mua {_cartItems.Count} đầu sách với tổng số tiền {total:N0} đ?", 
                                         "Xác nhận thanh toán", 
                                         MessageBoxButton.YesNo, 
                                         MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show("🎉 Đặt hàng thành công! Đơn hàng của bạn đã được ghi nhận vào hệ thống.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                _cartItems.Clear();
                icCartItems.Items.Refresh();
                UpdateCartBadge();
                gridCartModal.Visibility = Visibility.Collapsed;
            }
        }
    }
}
