using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BookStoreManagementSystem.ViewModels
{
    public class BookCardViewModel
    {
        public int BookId { get; set; }
        public int CategoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? Publisher { get; set; }
        public int? PublicationYear { get; set; }
        public string? Isbn { get; set; }
        public decimal Price { get; set; }
        public string FormattedPrice => Price.ToString("N0") + " đ";
        public int StockQuantity { get; set; }
        public string StockStatusText => StockQuantity > 0 ? $"Còn {StockQuantity} cuốn" : "Hết hàng";
        public bool IsInStock => StockQuantity > 0;
        public string? Description { get; set; }
        public string BookStatus { get; set; } = "Bán";
        
        public string StatusBadgeColor => BookStatus switch
        {
            "Bán" => "#10B981",
            "Cho thuê" => "#F59E0B",
            "Đã thuê" => "#6B7280",
            _ => "#6366F1"
        };

        public ImageSource? CoverImage { get; set; }
        public bool HasCoverImage => CoverImage != null;
        public bool HasNoCoverImage => CoverImage == null;
        public DateTime CreatedAt { get; set; }

        public static ImageSource? LoadImageSafe(byte[]? imageData)
        {
            if (imageData == null || imageData.Length < 100)
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
    }

    public class CategoryFilterItem
    {
        public int? CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public string DisplayText => $"{Name} ({Count})";
    }

    public class CartItemViewModel
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal => UnitPrice * Quantity;
        public string FormattedSubTotal => SubTotal.ToString("N0") + " đ";
        public string FormattedUnitPrice => UnitPrice.ToString("N0") + " đ";
    }
}
