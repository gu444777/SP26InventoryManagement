using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement
{
    public partial class App : Application
    {
        private const string AdminRoleCode = "ADMIN";

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
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddSingleton<IMessageService, MessageService>();
            services.AddSingleton<IUserDialogService, UserDialogService>();

            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<AdminUserManagementViewModel>();
            services.AddTransient<CreateUserViewModel>();
            services.AddTransient<ChangePasswordViewModel>();

            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<AdminUserManagementWindow>();
            services.AddTransient<CreateUserWindow>();
            services.AddTransient<ChangePasswordWindow>();

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
                Window startWindow = currentUser.IsInRole(AdminRoleCode)
                    ? Services.GetRequiredService<AdminUserManagementWindow>()
                    : Services.GetRequiredService<MainWindow>();

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
