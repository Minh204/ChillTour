using ChillTour.Data;
using ChillTour.Services.Payments;
using ChillTour.Services.Notifications;
using ChillTour.Services.Auth;
using ChillTour.Services.Mail;
using ChillTour.Security;
using ChillTour.Services.Reports;
using ChillTour.Services.Contracts;
using ChillTour.Services.Chatbot;
using ChillTour.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using QuestPDF.Infrastructure;

namespace ChillTour
{
    public class Program
    {
        public static void Main(string[] args)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddSignalR();
            builder.Services.AddDbContext<ChillTourDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.Configure<VnPayOptions>(builder.Configuration.GetSection(VnPayOptions.SectionName));
            builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));
            builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.SectionName));
            builder.Services.Configure<GeminiChatbotOptions>(builder.Configuration.GetSection(GeminiChatbotOptions.SectionName));
            var googleAuthOptions = builder.Configuration.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>() ?? new GoogleAuthOptions();
            var isGoogleAuthConfigured =
                !string.IsNullOrWhiteSpace(googleAuthOptions.ClientId) &&
                !string.IsNullOrWhiteSpace(googleAuthOptions.ClientSecret);
            builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<DbSeeder>();
            builder.Services.AddScoped<IVnPayService, VnPayService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<IContractService, ContractService>();
            builder.Services.AddHttpClient<IChatbotService, GeminiChatbotService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(45);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseProxy = false
                });
            builder.Services.AddHostedService<BookingBalanceReminderService>();
            var authenticationBuilder = builder.Services.AddAuthentication(AuthSchemeConstants.Application)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.Cookie.Name = "ChillTour.Auth";
                    options.SlidingExpiration = true;
                })
                .AddCookie(AuthSchemeConstants.External, options =>
                {
                    options.Cookie.Name = "ChillTour.ExternalAuth";
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                    options.SlidingExpiration = false;
                });

            if (isGoogleAuthConfigured)
            {
                authenticationBuilder.AddGoogle(AuthSchemeConstants.Google, options =>
                {
                    options.ClientId = googleAuthOptions.ClientId;
                    options.ClientSecret = googleAuthOptions.ClientSecret;
                    options.CallbackPath = "/signin-google";
                    options.SignInScheme = AuthSchemeConstants.External;
                    options.SaveTokens = false;
                });
            }
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(PermissionConstants.AccessAdmin, policy => policy.RequireRole(RoleConstants.BackOffice));
                options.AddPolicy(PermissionConstants.ManageUsers, policy => policy.RequireRole(RoleConstants.ManageUsers));
                options.AddPolicy(PermissionConstants.ViewReports, policy => policy.RequireRole(RoleConstants.ViewReports));
                options.AddPolicy(PermissionConstants.ManageTours, policy => policy.RequireRole(RoleConstants.ManageTours));
                options.AddPolicy(PermissionConstants.ViewBookings, policy => policy.RequireRole(RoleConstants.ViewBookings));
                options.AddPolicy(PermissionConstants.ManageBookings, policy => policy.RequireRole(RoleConstants.ManageBookings));
                options.AddPolicy(PermissionConstants.ManageFinance, policy => policy.RequireRole(RoleConstants.ManageFinance));
                options.AddPolicy(PermissionConstants.ManagePromotions, policy => policy.RequireRole(RoleConstants.ManagePromotions));
                options.AddPolicy(PermissionConstants.ManageContent, policy => policy.RequireRole(RoleConstants.ManageContent));
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
                seeder.SeedAsync().GetAwaiter().GetResult();
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapHub<NotificationHub>("/hubs/notifications");

            app.Run();
        }
    }
}
