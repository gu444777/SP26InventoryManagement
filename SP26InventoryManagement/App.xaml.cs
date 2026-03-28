using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SP26InventoryManagement;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Repositories.Interfaces;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.ViewModels;
using SP26InventoryManagement.Views;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


namespace SP26InventoryManagement
{
    public partial class App :  Application
{
    public IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Services = ConfigureServices();

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

        // 1. Configuration
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        string connectionString = "Server=DESKTOP-3AA9AGC\\SQLEXPRESS;Database=SP26InventoryManagementDB;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddSingleton(configuration);
        services.AddSingleton<CurrentUserContext>();

        // 2. DbContext
        services.AddDbContext<Sp26inventoryManagementDbContext>(options =>
            options.UseSqlServer(connectionString));

        // 3. Repositories
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IRoleRepository, RoleRepository>();
        services.AddTransient<IUserRoleRepository, UserRoleRepository>();
        services.AddTransient<IAuditLogRepository, AuditLogRepository>();
        services.AddTransient<IWarehouseRepository, WarehouseRepository>();
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<ICategoryRepository, CategoryRepository>(); // Đã đăng ký

        // 4. Services
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IUserManagementService, UserManagementService>();
        services.AddTransient<ISessionValidationService, SessionValidationService>();
        services.AddTransient<IAuditLogService, AuditLogService>();
        services.AddTransient<IIssueService, IssueService>();
        services.AddTransient<ISupplierService, SupplierService>();
        services.AddTransient<WarehouseService>();
        services.AddTransient<ProductService>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IUserDialogService, UserDialogService>();

        // 5. ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<IssueManagementViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AdminUserManagementViewModel>();
        services.AddTransient<CreateUserViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<WarehouseViewModel>();
        services.AddTransient<SupplierViewModel>();
        services.AddTransient<ProductViewModel>();
        services.AddTransient<CategoryViewModel>(); // Đã đăng ký

        // 6. Windows/Views (Quan trọng: Đã bổ sung CategoryView)
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<IssueStaffWindow>();
        services.AddTransient<IssueManagerWindow>();
        services.AddTransient<AdminUserManagementWindow>();
        services.AddTransient<CreateUserWindow>();
        services.AddTransient<ChangePasswordWindow>();
        services.AddTransient<WarehouseView>();
        services.AddTransient<SupplierView>();
        services.AddTransient<ProductView>();
        services.AddTransient<CategoryView>();
            services.AddTransient<IAdjustmentRepository, AdjustmentRepository>();
            services.AddTransient<AdjustmentViewModel>();
            services.AddTransient<AdjustmentView>();



            return services.BuildServiceProvider();
    }

    private bool ShowLoginAndOpenStartWindow()
    {
        var loginWindow = Services.GetRequiredService<LoginWindow>();
        bool? loginResult = loginWindow.ShowDialog();

        if (loginResult != true) return false;

        CurrentUserContext currentUser = Services.GetRequiredService<CurrentUserContext>();
        if (!currentUser.IsAuthenticated)
        {
            MessageBox.Show("Authentication failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        try
        {
            var startWindow =  Services.GetRequiredService<MainWindow>();
            var mainViewModel =  Services.GetRequiredService<MainWindowViewModel>();

            startWindow.DataContext = mainViewModel;
            this.MainWindow = startWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            startWindow.Show();
            return true;
        }
            catch(Exception ex)
            {
            MessageBox.Show($"Error initializing main window: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }
}
}