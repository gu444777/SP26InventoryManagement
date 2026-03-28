using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Repositories.Interfaces;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.Services.Interfaces;
using SP26InventoryManagement.ViewModels;
using SP26InventoryManagement.Views;
using System.Windows;

namespace SP26InventoryManagement
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Services = ConfigureServices();

            // 🔥 TEST MODE (đơn giản nhất)
            // var window = Services.GetRequiredService<SupplierView>();
            //MainWindow = window;
            //ShutdownMode = ShutdownMode.OnMainWindowClose;
            //window.Show();
            //return;

            // 👇 code cũ giữ nguyên bên dưới (không chạy khi test)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!ShowLoginAndOpenStartWindow())
            {
                Shutdown();
            }
        }

        public void NavigateToLoginAfterLogout(Window sourceWindow)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            sourceWindow.Hide();

            bool loginSucceeded = ShowLoginAndOpenStartWindow();
            if (!loginSucceeded)
            {
                sourceWindow.Close();
                Shutdown();
                return;
            }

            sourceWindow.Close();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (Services is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnExit(e);
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection in appsettings.json.");

            services.AddSingleton(configuration);
            services.AddSingleton<CurrentUserContext>();

            services.AddDbContext<Sp26inventoryManagementDbContext>(
                options => options.UseSqlServer(connectionString),
                contextLifetime: ServiceLifetime.Transient,
                optionsLifetime: ServiceLifetime.Singleton);

            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IRoleRepository, RoleRepository>();
            services.AddTransient<IUserRoleRepository, UserRoleRepository>();
            services.AddTransient<IAuditLogRepository, AuditLogRepository>();

            services.AddTransient<IAuthService, AuthService>();
            services.AddTransient<IUserManagementService, UserManagementService>();
            services.AddTransient<ISessionValidationService, SessionValidationService>();
            services.AddTransient<IAuditLogService, AuditLogService>();
            services.AddTransient<IIssueService, IssueService>();
            services.AddTransient<ITransferService, TransferService>();
            services.AddTransient<IReceiptService, ReceiptService>();
            services.AddTransient<IStockSnapshotService, StockSnapshotService>();
            services.AddTransient<IStockLedgerService, StockLedgerService>();
            services.AddTransient<IExpiryAlertService, ExpiryAlertService>();
            services.AddTransient<IGrossProfitReportService, GrossProfitReportService>();
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddSingleton<IMessageService, MessageService>();
            services.AddSingleton<IUserDialogService, UserDialogService>();

            services.AddTransient<LoginViewModel>();
            services.AddTransient<IssueManagementViewModel>();
            services.AddTransient<TransferManagementViewModel>();
            services.AddTransient<ReceiptManagementViewModel>();
            services.AddTransient<StockSnapshotViewModel>();
            services.AddTransient<StockLedgerViewModel>();
            services.AddTransient<ExpiryAlertViewModel>();
            services.AddTransient<GrossProfitReportViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<AdminUserManagementViewModel>();
            services.AddTransient<StaffWarehouseAssignmentViewModel>();
            services.AddTransient<CreateUserViewModel>();
            services.AddTransient<ChangePasswordViewModel>();

            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<IssueStaffWindow>();
            services.AddTransient<IssueManagerWindow>();
            services.AddTransient<TransferWindow>();
            services.AddTransient<ReceiptStaffWindow>();
            services.AddTransient<ReceiptManagerWindow>();
            services.AddTransient<StockSnapshotWindow>();
            services.AddTransient<StockLedgerWindow>();
            services.AddTransient<ExpiryAlertWindow>();
            services.AddTransient<GrossProfitReportWindow>();
            services.AddTransient<AdminUserManagementWindow>();
            services.AddTransient<StaffWarehouseAssignmentWindow>();
            services.AddTransient<CreateUserWindow>();
            services.AddTransient<ChangePasswordWindow>();
            services.AddTransient<ISupplierService, SupplierService>();
            services.AddTransient<ICustomerService, CustomerService>();

            services.AddTransient<SupplierViewModel>();
            services.AddTransient<CustomerViewModel>();

            //an
            services.AddTransient<WarehouseService>();
            services.AddTransient<ProductService>();
            services.AddTransient<IAdjustmentRepository, AdjustmentRepository>();
            services.AddTransient<IWarehouseRepository, WarehouseRepository>();
            services.AddTransient<IProductRepository, ProductRepository>();
            services.AddTransient<ICategoryRepository, CategoryRepository>();
            services.AddTransient<WarehouseViewModel>();
            services.AddTransient<ProductViewModel>();
            services.AddTransient<CategoryViewModel>();
            services.AddTransient<AdjustmentViewModel>();
            services.AddTransient<WarehouseView>();
            services.AddTransient<ProductView>();
            services.AddTransient<CategoryView>();
            services.AddTransient<AdjustmentView>();

            //tesss
            services.AddTransient<SupplierView>();
            services.AddTransient<CustomerView>();

            return services.BuildServiceProvider();
        }

        private bool ShowLoginAndOpenStartWindow()
        {
            var loginWindow = Services.GetRequiredService<LoginWindow>();
            bool? loginResult = loginWindow.ShowDialog();

            if (loginResult != true)
            {
                return false;
            }

            CurrentUserContext currentUser = Services.GetRequiredService<CurrentUserContext>();
            if (!currentUser.IsAuthenticated)
            {
                MessageBox.Show(
                    "Unable to establish authenticated session. Please try again.",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            try
            {
                Window startWindow = Services.GetRequiredService<MainWindow>();

                MainWindow = startWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                startWindow.Show();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open start window.\n\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }

}
