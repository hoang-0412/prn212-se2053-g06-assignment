using System;

namespace BookStoreManagementSystem.ViewModels;

public class VoucherWalletViewModel
{
    public int VoucherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ExpiryAt { get; set; }
    public int UsageCount { get; set; }
    public int MaxUsagePerUser { get; set; }
    public string DisplayText => DiscountType == "Percent"
        ? $"{Code} - {DiscountValue:N0}% (còn {MaxUsagePerUser - UsageCount} lượt)"
        : $"{Code} - giảm {DiscountValue:N0} đ (còn {MaxUsagePerUser - UsageCount} lượt)";
}
