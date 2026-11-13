using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BookStoreManagementSystem.Models;

namespace BookStoreManagementSystem
{
    public partial class StoreHeaderControl : UserControl
    {
        public static readonly DependencyProperty ActiveNavProperty =
            DependencyProperty.Register(
                nameof(ActiveNav),
                typeof(string),
                typeof(StoreHeaderControl),
                new PropertyMetadata("Home", OnActiveNavChanged));

        public string ActiveNav
        {
            get => (string)GetValue(ActiveNavProperty);
            set => SetValue(ActiveNavProperty, value);
        }

        public event EventHandler? HomeClicked;
        public event EventHandler? CategoriesClicked;
        public event EventHandler? RentalsClicked;
        public event EventHandler? FeaturedClicked;
        public event EventHandler? PromotionsClicked;
        public event EventHandler? SearchClicked;
        public event EventHandler? CartClicked;

        public StoreHeaderControl()
        {
            InitializeComponent();
            Loaded += StoreHeaderControl_Loaded;
        }

        private static void OnActiveNavChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StoreHeaderControl ctrl)
            {
                ctrl.SetActiveNav(e.NewValue as string ?? "");
            }
        }

        private void StoreHeaderControl_Loaded(object sender, RoutedEventArgs e)
        {
            SetActiveNav(ActiveNav);
            RefreshAuthAndCart();
        }

        public void SetActiveNav(string navKey)
        {
            var activeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
            var inactiveColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));

            btnNavHome.Foreground = navKey == "Home" ? activeColor : inactiveColor;
            btnNavHome.FontWeight = navKey == "Home" ? FontWeights.Bold : FontWeights.SemiBold;
            indNavHome.Visibility = navKey == "Home" ? Visibility.Visible : Visibility.Collapsed;

            btnNavCategories.Foreground = navKey == "Categories" ? activeColor : inactiveColor;
            btnNavCategories.FontWeight = navKey == "Categories" ? FontWeights.Bold : FontWeights.SemiBold;
            indNavCategories.Visibility = navKey == "Categories" ? Visibility.Visible : Visibility.Collapsed;

            btnNavRentals.Foreground = navKey == "Rentals" ? activeColor : inactiveColor;
            btnNavRentals.FontWeight = navKey == "Rentals" ? FontWeights.Bold : FontWeights.SemiBold;
            indNavRentals.Visibility = navKey == "Rentals" ? Visibility.Visible : Visibility.Collapsed;

            btnNavPromotions.Foreground = navKey == "Promotions" ? activeColor : inactiveColor;
            btnNavPromotions.FontWeight = navKey == "Promotions" ? FontWeights.Bold : FontWeights.SemiBold;
            indNavPromotions.Visibility = navKey == "Promotions" ? Visibility.Visible : Visibility.Collapsed;
        }

        public void RefreshAuthAndCart()
        {
            bool isLoggedIn = SessionManager.IsLoggedIn;
            txtDropdownUserName.Text = SessionManager.DisplayName;

            if (btnUserMenu.Template != null)
            {
                btnUserMenu.ApplyTemplate();
                if (btnUserMenu.Template.FindName("txtCurrentUserName", btnUserMenu) is TextBlock nameBlock)
                {
                    nameBlock.Text = SessionManager.DisplayName;
                }
            }

            gridUserMenu.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
            btnOpenAdmin.Visibility = SessionManager.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnAuthAction.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;

            RefreshCartBadgeFromDb();
        }

        private void btnUserMenu_Click(object sender, RoutedEventArgs e)
        {
            popupUserMenu.IsOpen = !popupUserMenu.IsOpen;
        }

        public void SetCartCount(int count)
        {
            txtCartBadge.Text = count.ToString();
            badgeCartCount.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void RefreshCartBadgeFromDb()
        {
            if (SessionManager.CurrentUser == null)
            {
                SetCartCount(0);
                return;
            }

            try
            {
                using var context = new BookStoreDbContext();
                var count = context.CartItems
                    .Where(ci => ci.Cart.UserId == SessionManager.CurrentUser.UserId)
                    .Sum(ci => (int?)ci.Quantity) ?? 0;
                SetCartCount(count);
            }
            catch
            {
                // Ignore if DB unavailable in designer
            }
        }

        private void NavigateToWindow(Window nextWindow)
        {
            var host = Window.GetWindow(this);
            nextWindow.Show();
            host?.Close();
        }

        private void btnNavHome_Click(object sender, RoutedEventArgs e)
        {
            if (HomeClicked != null)
            {
                HomeClicked.Invoke(this, EventArgs.Empty);
                return;
            }
            if (Window.GetWindow(this) is HomePage) return;
            NavigateToWindow(new HomePage());
        }

        private void btnNavCategories_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesClicked != null)
            {
                CategoriesClicked.Invoke(this, EventArgs.Empty);
                return;
            }
            if (Window.GetWindow(this) is BookListWindow) return;
            NavigateToWindow(new BookListWindow());
        }

        private void btnNavRentals_Click(object sender, RoutedEventArgs e)
        {
            if (RentalsClicked != null)
            {
                RentalsClicked.Invoke(this, EventArgs.Empty);
                return;
            }
            var home = new HomePage();
            NavigateToWindow(home);
        }

        private void btnNavFeatured_Click(object sender, RoutedEventArgs e)
        {
            if (FeaturedClicked != null)
            {
                FeaturedClicked.Invoke(this, EventArgs.Empty);
                return;
            }
            var home = new HomePage();
            NavigateToWindow(home);
        }

        private void btnNavPromotions_Click(object sender, RoutedEventArgs e)
        {
            if (PromotionsClicked != null)
            {
                PromotionsClicked.Invoke(this, EventArgs.Empty);
                return;
            }
            MessageBox.Show(
                "🎁 ƯU ĐÃI ĐANG ÁP DỤNG TẠI G07 BOOKSTORE:\n\n" +
                "• Mã BOOK10: Giảm 10% cho mọi đơn hàng\n" +
                "• Mã SINHVIEN: Giảm 15% cho đơn thuê sách\n" +
                "• Mã VIP50K: Giảm trực tiếp 50.000 đ cho đơn từ 300.000 đ\n" +
                "• Mã FREESHIP: Miễn phí giao hàng toàn quốc\n\n" +
                "Nhập mã trực tiếp tại màn hình Giỏ Hàng & Thanh Toán!",
                "Chương Trình Khuyến Mãi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void btnToggleSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchClicked != null)
            {
                SearchClicked.Invoke(this, EventArgs.Empty);
                return;
            }
            if (Window.GetWindow(this) is BookListWindow) return;
            NavigateToWindow(new BookListWindow());
        }

        private void btnOpenCart_Click(object sender, RoutedEventArgs e)
        {
            if (CartClicked != null)
            {
                CartClicked.Invoke(this, EventArgs.Empty);
                return;
            }
            if (Window.GetWindow(this) is CartCheckoutWindow) return;
            NavigateToWindow(new CartCheckoutWindow());
        }

        private void btnOpenMyOrders_Click(object sender, RoutedEventArgs e)
        {
            popupUserMenu.IsOpen = false;
            if (Window.GetWindow(this) is CustomerOrdersWindow) return;
            NavigateToWindow(new CustomerOrdersWindow());
        }

        private void btnOpenProfile_Click(object sender, RoutedEventArgs e)
        {
            popupUserMenu.IsOpen = false;
            if (Window.GetWindow(this) is UserProfileWindow) return;
            NavigateToWindow(new UserProfileWindow("Home"));
        }

        private void btnOpenAdmin_Click(object sender, RoutedEventArgs e)
        {
            popupUserMenu.IsOpen = false;
            if (Window.GetWindow(this) is AdminDashboardWindow) return;
            NavigateToWindow(new AdminDashboardWindow());
        }

        private void btnAuthAction_Click(object sender, RoutedEventArgs e)
        {
            popupUserMenu.IsOpen = false;
            SessionManager.Logout();
            NavigateToWindow(new LoginWindow());
        }
    }
}
