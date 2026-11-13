using System;
using System.Collections.Generic;

namespace BookStoreManagementSystem.Models;

public partial class Voucher
{
    public int VoucherId { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime ExpiryAt { get; set; }

    public int MaxUsagePerUser { get; set; }

    public int TotalQuantity { get; set; }

    public int SavedCount { get; set; }

    public int UsedCount { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();
}
