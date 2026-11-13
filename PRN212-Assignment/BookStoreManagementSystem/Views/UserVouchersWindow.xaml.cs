using System;
using System.Linq;
using System.Windows;
using BookStoreManagementSystem.Models;
using BookStoreManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BookStoreManagementSystem;

public partial class UserVouchersWindow : Window
{
    public UserVouchersWindow()
    {
        InitializeComponent();
        Loaded += UserVouchersWindow_Loaded;
    }

    private void UserVouchersWindow_Loaded(object sender, RoutedEventArgs e)
    {
        EnsureUserSession();
        LoadSavedVouchers();
    }

    private void EnsureUserSession()
    {
        if (SessionManager.CurrentUser != null) return;

        try
        {
            using var context = new BookStoreDbContext();
            SessionManager.CurrentUser = context.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.RoleId == 1)
                ?? context.Users.Include(u => u.Role).FirstOrDefault();
        }
        catch
        {
        }
    }

    private void LoadSavedVouchers()
    {
        if (SessionManager.CurrentUser == null)
        {
            dgUserVouchers.ItemsSource = null;
            txtVoucherCount.Text = "0 voucher";
            return;
        }

        try
        {
            using var context = new BookStoreDbContext();
            var vouchers = context.UserVouchers
                .AsNoTracking()
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == SessionManager.CurrentUser.UserId)
                .OrderByDescending(uv => uv.SavedAt)
                .Select(uv => new VoucherWalletViewModel
                {
                    VoucherId = uv.VoucherId,
                    Name = uv.Voucher.Name,
                    Code = uv.Voucher.Code,
                    DiscountType = uv.Voucher.DiscountType,
                    DiscountValue = uv.Voucher.DiscountValue,
                    ExpiryAt = uv.Voucher.ExpiryAt,
                    UsageCount = uv.UsageCount,
                    MaxUsagePerUser = uv.Voucher.MaxUsagePerUser
                })
                .ToList();

            dgUserVouchers.ItemsSource = vouchers;
            txtVoucherCount.Text = $"{vouchers.Count} voucher";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể tải voucher của bạn:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void btnSaveVoucher_Click(object sender, RoutedEventArgs e)
    {
        var code = txtVoucherCode.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show("Vui lòng nhập mã voucher.", "Thiếu mã voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SessionManager.CurrentUser == null) return;

        try
        {
            using var context = new BookStoreDbContext();
            var now = DateTime.UtcNow;
            var voucher = context.Vouchers.FirstOrDefault(v => v.Code == code);

            if (voucher == null || !voucher.IsActive || voucher.StartAt > now || voucher.ExpiryAt < now)
            {
                MessageBox.Show("Mã voucher không tồn tại, chưa bắt đầu hoặc đã hết hạn.", "Voucher không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (voucher.SavedCount >= voucher.TotalQuantity)
            {
                MessageBox.Show("Voucher đã hết số lượng có thể lưu.", "Voucher đã hết lượt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userId = SessionManager.CurrentUser.UserId;
            var alreadySaved = context.UserVouchers.Any(uv => uv.UserId == userId && uv.VoucherId == voucher.VoucherId);
            if (alreadySaved)
            {
                MessageBox.Show("Voucher này đã có trong kho voucher của bạn.", "Đã lưu voucher", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            context.UserVouchers.Add(new UserVoucher
            {
                UserId = userId,
                VoucherId = voucher.VoucherId,
                SavedAt = now
            });
            voucher.SavedCount++;
            context.SaveChanges();

            txtVoucherCode.Clear();
            LoadSavedVouchers();
            MessageBox.Show($"Đã lưu voucher '{voucher.Code}'. Bạn có thể chọn voucher này khi thanh toán.", "Lưu voucher thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể lưu voucher:\n{ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void btnRefresh_Click(object sender, RoutedEventArgs e) => LoadSavedVouchers();

    private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
}
