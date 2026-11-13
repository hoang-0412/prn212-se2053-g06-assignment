using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BookStoreManagementSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace BookStoreManagementSystem.ViewModels
{
    public class BookImageItemViewModel
    {
        public int ImageId { get; set; }
        public int BookId { get; set; }
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public string MimeType { get; set; } = "image/jpeg";
        public bool IsCover { get; set; }
        public ImageSource? ImageSource { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CoverBadgeText => IsCover ? "★ Ảnh bìa" : "Ảnh phụ";
        public string BorderColor => IsCover ? "#4F46E5" : "#CBD5E1";
    }

    public class BookCardViewModel
    {
        public int BookId { get; set; }
        public string BookCode => $"#BK-{BookId:D3}";
        public int CategoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? Publisher { get; set; }
        public string PublisherDisplay => string.IsNullOrWhiteSpace(Publisher) ? "Đang cập nhật" : Publisher;
        public int? PublicationYear { get; set; }
        public string YearDisplay => PublicationYear.HasValue ? PublicationYear.Value.ToString() : "—";
        public string? Isbn { get; set; }
        public string IsbnDisplay => string.IsNullOrWhiteSpace(Isbn) ? "—" : Isbn;
        public decimal Price { get; set; }
        public string FormattedPrice => Price.ToString("N0") + " đ";
        public int StockQuantity { get; set; }
        public string StockStatusText => StockQuantity > 0 ? $"Còn {StockQuantity} cuốn" : "Hết hàng";
        public bool IsInStock => StockQuantity > 0;
        public string StockBadgeColor => IsInStock ? "#DCFCE7" : "#FEE2E2";
        public string StockTextColor => IsInStock ? "#15803D" : "#DC2626";
        public string? Description { get; set; }
        public string BookStatus { get; set; } = "Bán";

        public bool IsForRent => string.Equals(BookStatus, "Cho thuê", StringComparison.OrdinalIgnoreCase);
        public bool IsForSale => !IsForRent;

        public decimal RentalPrice7Days => Math.Max(10000m, Math.Round(Price * 0.15m / 1000m, 0) * 1000m);
        public decimal RentalPrice14Days => Math.Max(18000m, Math.Round(Price * 0.25m / 1000m, 0) * 1000m);
        public decimal RentalPrice30Days => Math.Max(30000m, Math.Round(Price * 0.40m / 1000m, 0) * 1000m);
        public string FormattedRental7Days => $"{RentalPrice7Days:N0} đ";
        public string FormattedCreatedAt => CreatedAt == default ? "—" : CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");

        public string PrimaryPriceDisplay => IsForRent
            ? $"Thuê: {RentalPrice7Days:N0} đ/tuần"
            : FormattedPrice;

        public string SecondaryPriceDisplay => IsForRent
            ? $"Giá bìa: {FormattedPrice}"
            : $"Thuê từ: {RentalPrice7Days:N0} đ";

        public string PrimaryActionText => StockQuantity <= 0
            ? "❌ Hết Hàng"
            : (IsForRent ? "📅 Thuê Sách" : "🛒 Mua Sách");

        public string StatusBadgeColor => BookStatus switch
        {
            "Bán" => "#10B981",
            "Cho thuê" => "#F59E0B",
            _ => "#6366F1"
        };

        public ImageSource? CoverImage { get; set; }
        public byte[]? RawCoverImage { get; set; }
        public List<BookImageItemViewModel> GalleryImages { get; set; } = new();
        public int ImageCount => GalleryImages.Count;
        public bool HasGalleryImages => GalleryImages.Count > 0;
        public string ImageCountBadge => GalleryImages.Count > 0 ? $"🖼️ {GalleryImages.Count} ảnh" : "Chưa có ảnh";
        public bool HasCoverImage => CoverImage != null;
        public bool HasNoCoverImage => CoverImage == null;
        public bool IsAdminActionVisible => SessionManager.IsAdmin;
        public DateTime CreatedAt { get; set; }

        public static BookCardViewModel FromEntity(Book b)
        {
            var validImages = new List<BookImageItemViewModel>();

            if (b.BookImages != null)
            {
                foreach (var img in b.BookImages.OrderByDescending(i => i.IsCover).ThenByDescending(i => i.ImageId))
                {
                    var imgSrc = LoadImageSafe(img.ImageData);
                    if (imgSrc != null)
                    {
                        validImages.Add(new BookImageItemViewModel
                        {
                            ImageId = img.ImageId,
                            BookId = img.BookId,
                            ImageData = img.ImageData,
                            MimeType = img.MimeType,
                            IsCover = img.IsCover,
                            ImageSource = imgSrc,
                            CreatedAt = img.CreatedAt
                        });
                    }
                }
            }

            var coverItem = validImages.FirstOrDefault(i => i.IsCover) ?? validImages.FirstOrDefault();

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
                RawCoverImage = coverItem?.ImageData,
                CoverImage = coverItem?.ImageSource,
                GalleryImages = validImages,
                CreatedAt = b.CreatedAt
            };
        }

        public static ImageSource? LoadImageSafe(byte[]? imageData)
        {
            if (imageData == null || imageData.Length < 16)
            {
                return null;
            }

            try
            {
                using var stream = new MemoryStream(imageData);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static byte[] GenerateDefaultCoverPng(string title, string author, string categoryName, int seed)
        {
            try
            {
                int width = 260;
                int height = 360;

                var palettes = new (string Start, string Mid, string End)[]
                {
                    ("#312E81", "#4F46E5", "#0284C7"),
                    ("#064E3B", "#059669", "#0D9488"),
                    ("#7C2D12", "#D97706", "#F59E0B"),
                    ("#4C1D95", "#7C3AED", "#DB2777"),
                    ("#0F172A", "#1E40AF", "#3B82F6"),
                    ("#831843", "#BE185D", "#F43F5E")
                };

                var p = palettes[Math.Abs(seed) % palettes.Length];
                var c1 = (Color)ColorConverter.ConvertFromString(p.Start);
                var c2 = (Color)ColorConverter.ConvertFromString(p.Mid);
                var c3 = (Color)ColorConverter.ConvertFromString(p.End);

                var visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    var bgBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1)
                    };
                    bgBrush.GradientStops.Add(new GradientStop(c1, 0.0));
                    bgBrush.GradientStops.Add(new GradientStop(c2, 0.55));
                    bgBrush.GradientStops.Add(new GradientStop(c3, 1.0));

                    dc.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

                    // Spine & decorative border
                    var spineBrush = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0));
                    dc.DrawRectangle(spineBrush, null, new Rect(0, 0, 18, height));

                    var framePen = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.5);
                    dc.DrawRoundedRectangle(null, framePen, new Rect(26, 18, width - 40, height - 36), 8, 8);

                    var typefaceBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var typefaceSemi = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

                    // Category Header
                    var catText = new FormattedText(
                        (categoryName ?? "BOOKSTORE").ToUpperInvariant(),
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        typefaceBold,
                        11,
                        new SolidColorBrush(Color.FromArgb(220, 254, 240, 138)),
                        96);
                    catText.MaxTextWidth = width - 64;
                    catText.TextAlignment = TextAlignment.Center;
                    dc.DrawText(catText, new Point(38, 38));

                    // Center Icon Circle
                    dc.DrawEllipse(
                        new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                        new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1),
                        new Point(width / 2.0 + 6, 102),
                        28,
                        28);

                    var iconText = new FormattedText(
                        "📚",
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        typefaceBold,
                        26,
                        Brushes.White,
                        96);
                    iconText.TextAlignment = TextAlignment.Center;
                    dc.DrawText(iconText, new Point(width / 2.0 + 6, 86));

                    // Book Title
                    var titleText = new FormattedText(
                        string.IsNullOrWhiteSpace(title) ? "Book Title" : title,
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        typefaceBold,
                        17,
                        Brushes.White,
                        96);
                    titleText.MaxTextWidth = width - 64;
                    titleText.MaxTextHeight = 115;
                    titleText.Trimming = TextTrimming.WordEllipsis;
                    titleText.TextAlignment = TextAlignment.Center;
                    dc.DrawText(titleText, new Point(38, 148));

                    // Divider line
                    dc.DrawLine(
                        new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.2),
                        new Point(70, 278),
                        new Point(width - 58, 278));

                    // Author
                    var authorText = new FormattedText(
                        string.IsNullOrWhiteSpace(author) ? "Tác giả" : author,
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        typefaceSemi,
                        12.5,
                        new SolidColorBrush(Color.FromArgb(235, 224, 231, 255)),
                        96);
                    authorText.MaxTextWidth = width - 64;
                    authorText.MaxTextHeight = 40;
                    authorText.Trimming = TextTrimming.CharacterEllipsis;
                    authorText.TextAlignment = TextAlignment.Center;
                    dc.DrawText(authorText, new Point(38, 290));
                }

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                return ms.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        public static string GetMimeTypeFromExtension(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
        }

        public static bool PromptAndUploadBookImages(int bookId, string bookTitle, bool setAsPrimaryCover, out int uploadedCount)
        {
            uploadedCount = 0;

            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show(
                    "Chỉ tài khoản Quản trị viên (Admin) mới có quyền tải ảnh lên cho sách!\nVui lòng đăng nhập bằng tài khoản Admin.",
                    "Yêu cầu quyền Admin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            var dlg = new OpenFileDialog
            {
                Title = setAsPrimaryCover
                    ? $"Chọn ảnh bìa chính cho sách: {bookTitle}"
                    : $"Chọn một hoặc nhiều ảnh cho sách: {bookTitle}",
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif|All Files (*.*)|*.*",
                Multiselect = !setAsPrimaryCover
            };

            if (dlg.ShowDialog() != true || dlg.FileNames.Length == 0)
            {
                return false;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var existingImages = context.BookImages
                    .Where(i => i.BookId == bookId)
                    .ToList();

                // Dọn các dòng dữ liệu mẫu giả (< 32 bytes) nếu có
                var invalidDummies = existingImages.Where(i => i.ImageData == null || i.ImageData.Length < 32).ToList();
                if (invalidDummies.Any())
                {
                    context.BookImages.RemoveRange(invalidDummies);
                    foreach (var d in invalidDummies)
                    {
                        existingImages.Remove(d);
                    }
                }

                bool hasValidCover = existingImages.Any(i => i.IsCover && LoadImageSafe(i.ImageData) != null);

                for (int idx = 0; idx < dlg.FileNames.Length; idx++)
                {
                    var file = dlg.FileNames[idx];
                    var bytes = File.ReadAllBytes(file);
                    if (LoadImageSafe(bytes) == null)
                    {
                        MessageBox.Show($"File '{Path.GetFileName(file)}' không phải định dạng ảnh hợp lệ!", "Lỗi đọc ảnh", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    bool makeCover = (idx == 0) && (setAsPrimaryCover || !hasValidCover);
                    if (makeCover)
                    {
                        foreach (var oldImg in existingImages)
                        {
                            oldImg.IsCover = false;
                        }
                        hasValidCover = true;
                    }

                    context.BookImages.Add(new BookImage
                    {
                        BookId = bookId,
                        ImageData = bytes,
                        MimeType = GetMimeTypeFromExtension(file),
                        IsCover = makeCover,
                        CreatedAt = DateTime.UtcNow
                    });
                    uploadedCount++;
                }

                if (uploadedCount > 0)
                {
                    context.SaveChanges();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu ảnh vào CSDL:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool SetCoverImageInDatabase(int bookId, int imageId)
        {
            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show("Chỉ Admin mới có quyền thay đổi ảnh bìa!", "Yêu cầu quyền Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var images = context.BookImages.Where(i => i.BookId == bookId).ToList();
                if (!images.Any()) return false;

                foreach (var img in images)
                {
                    img.IsCover = (img.ImageId == imageId);
                }

                context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đặt ảnh bìa:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool DeleteImageFromDatabase(int bookId, int imageId)
        {
            if (!SessionManager.IsAdmin)
            {
                MessageBox.Show("Chỉ Admin mới có quyền xóa ảnh sách!", "Yêu cầu quyền Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var images = context.BookImages
                    .Where(i => i.BookId == bookId)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToList();

                var target = images.FirstOrDefault(i => i.ImageId == imageId);
                if (target == null) return false;

                bool wasCover = target.IsCover;
                context.BookImages.Remove(target);
                images.Remove(target);

                if (wasCover && images.Any())
                {
                    images.First().IsCover = true;
                }

                context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xóa ảnh:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }

    public class CategoryFilterItem
    {
        public int? CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Count { get; set; }
        public string DisplayText => $"{Name} ({Count})";
    }

    public class CartItemViewModel
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal OriginalBookPrice { get; set; }
        public int Quantity { get; set; }
        public string OrderType { get; set; } = "Mua"; // "Mua" hoặc "Thuê"
        public int RentalDays { get; set; } = 0; // 0 nếu Mua, 7/14/30 nếu Thuê
        public ImageSource? CoverImage { get; set; }
        public bool HasCoverImage => CoverImage != null;
        public bool HasNoCoverImage => CoverImage == null;
        public decimal SubTotal => UnitPrice * Quantity;
        public string FormattedSubTotal => SubTotal.ToString("N0") + " đ";
        public string FormattedUnitPrice => OrderType == "Thuê"
            ? $"{UnitPrice:N0} đ (Thuê {RentalDays} ngày)"
            : $"{UnitPrice:N0} đ / cuốn";
        public string TypeBadgeText => OrderType == "Thuê" ? $"📅 Thuê {RentalDays} ngày" : "🛍️ Mua sở hữu";
        public string TypeBadgeColor => OrderType == "Thuê" ? "#FEF3C7" : "#DCFCE7";
        public string TypeTextColor => OrderType == "Thuê" ? "#B45309" : "#15803D";
    }

    public class OrderItemDetailViewModel
    {
        public int OrderItemId { get; set; }
        public int BookId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal => UnitPrice * Quantity;
        public string FormattedUnitPrice => UnitPrice.ToString("N0") + " đ";
        public string FormattedSubTotal => SubTotal.ToString("N0") + " đ";
    }

    public class OrderViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode => $"#ORD-{OrderId:D4}";
        public int UserId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string FormattedTotalAmount => TotalAmount.ToString("N0") + " đ";
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string FormattedCreatedAt => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        public string ItemsSummary { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public List<OrderItemDetailViewModel> Items { get; set; } = new();

        public bool IsRentalOrder =>
            OrderStatus.Contains("Thuê", StringComparison.OrdinalIgnoreCase) ||
            OrderStatus.Equals("Renting", StringComparison.OrdinalIgnoreCase) ||
            OrderStatus.Equals("Returned", StringComparison.OrdinalIgnoreCase) ||
            OrderStatus.Equals("Đã trả", StringComparison.OrdinalIgnoreCase) ||
            PaymentMethod.Contains("Thuê", StringComparison.OrdinalIgnoreCase) ||
            ShippingAddress.Contains("[THUÊ", StringComparison.OrdinalIgnoreCase);

        public string OrderTypeDisplay => IsRentalOrder ? "📅 Đơn Thuê Sách" : "🛍️ Đơn Mua Sách";
        public string OrderTypeBadgeBg => IsRentalOrder ? "#FEF3C7" : "#EEF2FF";
        public string OrderTypeBadgeFg => IsRentalOrder ? "#B45309" : "#4F46E5";

        public string StatusDisplay => OrderStatus switch
        {
            "Pending" => "Chờ xác nhận",
            "Processing" => "Đang giao",
            "Completed" => "Hoàn thành",
            "Cancelled" => "Đã hủy",
            "Renting" or "Đang thuê" => "Đang thuê sách",
            "Returned" or "Đã trả" => "Đã trả sách",
            "Overdue" or "Quá hạn" => "Quá hạn trả",
            _ => OrderStatus
        };

        public string StatusBadgeColor => OrderStatus switch
        {
            "Pending" => "#F59E0B",
            "Processing" => "#3B82F6",
            "Completed" => "#10B981",
            "Cancelled" => "#EF4444",
            "Renting" or "Đang thuê" => "#8B5CF6",
            "Returned" or "Đã trả" => "#059669",
            "Overdue" or "Quá hạn" => "#DC2626",
            _ => "#64748B"
        };

        public bool CanCancel =>
            string.Equals(OrderStatus, "Pending", StringComparison.OrdinalIgnoreCase);

        public bool CanReturnBook =>
            string.Equals(OrderStatus, "Renting", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(OrderStatus, "Đang thuê", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(OrderStatus, "Overdue", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(OrderStatus, "Quá hạn", StringComparison.OrdinalIgnoreCase);
    }

    public class UserAdminViewModel
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public int ActiveRentalCount { get; set; }
        public decimal TotalSpent { get; set; }
        public string FormattedTotalSpent => $"{TotalSpent:N0} đ";
        public DateTime CreatedAt { get; set; }
        public string FormattedCreatedAt => CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
        public string RoleBadgeColor => RoleId == 2 || string.Equals(RoleName, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "#7C3AED"
            : "#0284C7";
    }

    public class CategoryStatViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int BookCount { get; set; }
        public int TotalStock { get; set; }
        public double Percentage { get; set; }
        public string PercentageText => $"{Percentage:F0}%";
    }
}
