using System;

namespace BookStoreManagementSystem.Models;

public partial class UserVoucher
{
    public int UserVoucherId { get; set; }

    public int UserId { get; set; }

    public int VoucherId { get; set; }

    public int UsageCount { get; set; }

    public DateTime SavedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Voucher Voucher { get; set; } = null!;
}
