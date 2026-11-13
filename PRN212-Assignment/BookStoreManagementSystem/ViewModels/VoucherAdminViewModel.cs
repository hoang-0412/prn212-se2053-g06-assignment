using System;

namespace BookStoreManagementSystem.ViewModels;

public class VoucherAdminViewModel
{
    public int VoucherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime ExpiryAt { get; set; }
    public int MaxUsagePerUser { get; set; }
    public int TotalQuantity { get; set; }
    public int SavedCount { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; }
    public string DiscountDisplay => DiscountType == "Percent" ? $"{DiscountValue:N0}%" : $"{DiscountValue:N0} đ";
    public string QuantityDisplay => $"{SavedCount} / {TotalQuantity}";
}
