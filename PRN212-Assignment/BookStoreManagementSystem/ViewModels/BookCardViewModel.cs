using System;
using System.Collections.Generic;
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
        public string StockStatusText => IsRentedOut ? "Đang được thuê" : (StockQuantity > 0 ? $"Còn {StockQuantity} cuốn" : "Hết hàng");
        public bool IsInStock => StockQuantity > 0 && !IsRentedOut;
        public string? Description { get; set; }
        public string BookStatus { get; set; } = "Bán";

        public bool IsForRent => string.Equals(BookStatus, "Cho thuê", StringComparison.OrdinalIgnoreCase);
        public bool IsRentedOut => string.Equals(BookStatus, "Đã thuê", StringComparison.OrdinalIgnoreCase);
        public bool IsForSale => !IsForRent && !IsRentedOut;

        public decimal RentalPrice7Days => Math.Max(10000m, Math.Round(Price * 0.15m / 1000m, 0) * 1000m);
        public decimal RentalPrice14Days => Math.Max(18000m, Math.Round(Price * 0.25m / 1000m, 0) * 1000m);
        public decimal RentalPrice30Days => Math.Max(30000m, Math.Round(Price * 0.40m / 1000m, 0) * 1000m);

        public string PrimaryPriceDisplay => IsForRent
            ? $"Thuê: {RentalPrice7Days:N0} đ/tuần"
            : FormattedPrice;

        public string SecondaryPriceDisplay => IsForRent
            ? $"Giá bìa: {FormattedPrice}"
            : (IsRentedOut ? $"Giá bìa: {FormattedPrice}" : $"Thuê từ: {RentalPrice7Days:N0} đ");

        public string PrimaryActionText => IsRentedOut
            ? "⏳ Đã Thuê"
            : (IsForRent ? "📅 Thuê Sách" : "🛒 Mua Sách");

        public string StatusBadgeColor => BookStatus switch
        {
            "Bán" => "#10B981",
            "Cho thuê" => "#F59E0B",
            "Đã thuê" => "#6B7280",
            _ => "#6366F1"
        };

        public ImageSource? CoverImage { get; set; }
        public byte[]? RawCoverImage { get; set; }
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
