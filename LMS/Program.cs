using LMS.Data;
using LMS.Data.Seeding;
using LMS.Domain.Entities.Users;
using LMS.Extensions;
using LMS.Filters;
using LMS.Hubs;
using LMS.Middleware;
using LMS.Services;
using LMS.Services.Background;
using LMS.Services.Interfaces;
using LMS.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Capture any unhandled exceptions during startup for debugging deployment issues
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var logPath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "startup-crash.log");
    try
    {
        var logsDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logsDir) && !Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);
        
        File.AppendAllText(logPath, 
            $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FATAL Unhandled Exception:\n{args.ExceptionObject}\n\n");
    }
    catch { /* Ignore logging errors */ }
};

// Configure Serilog with error handling
try
{
    // Ensure logs directory exists
    var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    if (!Directory.Exists(logsDir))
    {
        Directory.CreateDirectory(logsDir);
    }

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logsDir, "lms-.txt"), 
            rollingInterval: RollingInterval.Day,
            shared: true,
            retainedFileCountLimit: 30)
        .CreateLogger();
}
catch (Exception ex)
{
    // Fallback to console-only logging if file logging fails
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateLogger();
    
    Log.Warning(ex, "Failed to initialize file logging, using console only");
}

try
{
    Log.Information("بدء تشغيل منصة LMS - Starting LMS Platform");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    ConfigureServices(builder.Services, builder.Configuration);

    var app = builder.Build();

    // Validate database connection on startup
    await ValidateDatabaseConnectionAsync(app);

    // Configure the HTTP request pipeline
    ConfigurePipeline(app);

    // Seed the database (in development, when explicitly enabled, or when DB has no users - e.g. first deploy)
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableDatabaseSeeding", false))
    {
        await SeedDatabaseAsync(app);
    }
    else
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await context.Users.AnyAsync())
            {
                Log.Information("Database has no users; running one-time seed for initial deploy.");
                await SeedDatabaseAsync(app);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not check or run one-time seed for empty database.");
        }
    }

    Log.Information("Application started successfully");
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "فشل تشغيل التطبيق - Application failed to start");
    
    // Write detailed diagnostics to file in case Serilog isn't working
    try
    {
        var crashLogPath = Path.Combine(Directory.GetCurrentDirectory(), "logs", "startup-crash.log");
        var logsDir = Path.GetDirectoryName(crashLogPath);
        if (!string.IsNullOrEmpty(logsDir) && !Directory.Exists(logsDir))
            Directory.CreateDirectory(logsDir);
        
        var diagnostics = new System.Text.StringBuilder();
        diagnostics.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] FATAL Application Startup Failed:");
        diagnostics.AppendLine($"Exception Type: {ex.GetType().FullName}");
        diagnostics.AppendLine($"Message: {ex.Message}");
        diagnostics.AppendLine($"Stack Trace:\n{ex.StackTrace}");
        
        // Inner exception chain
        var innerEx = ex.InnerException;
        var depth = 0;
        while (innerEx != null && depth < 5)
        {
            diagnostics.AppendLine($"Inner Exception [{depth}]: {innerEx.GetType().FullName}");
            diagnostics.AppendLine($"Inner Message [{depth}]: {innerEx.Message}");
            diagnostics.AppendLine($"Inner Stack [{depth}]:\n{innerEx.StackTrace}");
            innerEx = innerEx.InnerException;
            depth++;
        }
        
        // Environment diagnostics
        diagnostics.AppendLine("\n=== Environment Diagnostics ===");
        diagnostics.AppendLine($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Not Set"}");
        diagnostics.AppendLine($"OS: {Environment.OSVersion}");
        diagnostics.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        diagnostics.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
        diagnostics.AppendLine($".NET Version: {Environment.Version}");
        diagnostics.AppendLine($"Current Directory: {Directory.GetCurrentDirectory()}");
        diagnostics.AppendLine($"Machine Name: {Environment.MachineName}");
        diagnostics.AppendLine($"Processor Count: {Environment.ProcessorCount}");
        diagnostics.AppendLine();
        
        File.AppendAllText(crashLogPath, diagnostics.ToString());
    }
    catch { /* Ignore logging errors */ }
    
    throw; // Re-throw to ensure the application stops
}
finally
{
    Log.CloseAndFlush();
}

// Configure Services
void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // Database Context with Enterprise Configuration
    var connectionString = configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    
    services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            // Increase command timeout for complex queries (60 seconds)
            sqlOptions.CommandTimeout(60);
            
            // Enable retry on failure for transient errors (higher count/delay to avoid "max retries exceeded" on slow or busy SQL Server)
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 6,
                maxRetryDelay: TimeSpan.FromSeconds(15),
                errorNumbersToAdd: null);
        });
        
        // Enable query splitting for better performance with multiple includes
        //options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        
        // Enable sensitive data logging only in development
        #if DEBUG
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        #endif
    });
    
    services.AddDatabaseDeveloperPageExceptionFilter();

    // Identity with Roles - استخدام ApplicationUser بدلاً من IdentityUser
    services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password settings (min length 4 to allow seeded admin 'hz52' credential)
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 4;
        options.Password.RequiredUniqueChars = 1;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.User.RequireUniqueEmail = true;

        // Sign-in settings
        options.SignIn.RequireConfirmedEmail = false; // Change to true in production
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

    // Configure Settings from appsettings.json
    services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
    services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
    services.Configure<StorageSettings>(configuration.GetSection("StorageSettings"));
    services.Configure<PaginationSettings>(configuration.GetSection("PaginationSettings"));
    services.Configure<LMS.Settings.StripeSettings>(configuration.GetSection("PaymentSettings:Stripe"));

    // Configure request size limits for file uploads (especially PDFs)
    services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartHeadersLengthLimit = int.MaxValue;
        options.MultipartBoundaryLengthLimit = int.MaxValue;
    });

    // Register Application Services
    services.AddScoped<ICurrentUserService, CurrentUserService>();
    services.AddScoped<IDateTimeService, DateTimeService>();
    services.AddScoped<ISlugService, SlugService>();
    services.AddScoped<IEmailService, EmailService>();
    services.AddScoped<ISmsService, SmsService>();
    services.AddScoped<IFileStorageService, LocalFileStorageService>();
    
    // Register Payment Gateway Services (All gateways for Egypt & Gulf markets)
    services.AddPaymentGatewayServices();
    services.AddPaymentBackgroundServices();
    
    // Register Shopping Cart Service
    services.AddScoped<IShoppingCartService, ShoppingCartService>();
    
    // Register Recommendation Service
    services.AddScoped<IRecommendationService, RecommendationService>();
    
    // Register Learning Analytics Service
    services.AddScoped<ILearningAnalyticsService, LearningAnalyticsService>();
    
    // Register Student Services (NEW - Complete Service Layer)
    services.AddScoped<IStudentAssessmentService, StudentAssessmentService>();
    services.AddScoped<IStudentCourseService, StudentCourseService>();
    services.AddScoped<IStudentProfileService, StudentProfileService>();
    services.AddScoped<IGamificationService, GamificationService>();
    
    // Register Instructor Student Service (Enterprise-level with caching and optimization)
    services.AddScoped<IStudentService, StudentService>();

    // Register Instructor Profile Services (Enterprise-Level Implementation)
    services.AddScoped<IInstructorProfileService, InstructorProfileService>();
    services.AddScoped<IDropdownConfigService, DropdownConfigService>();
    
    // Register Enterprise Notification Services (Enhanced)
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IInstructorNotificationService, InstructorNotificationService>();
    services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();
    services.AddScoped<IPushSubscriptionStore, PushSubscriptionStore>();
    services.AddScoped<IWebPushNotificationService, WebPushNotificationService>();
    
    // Add SignalR for real-time notifications
    services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    });
    
    // Register Advanced Learning Services (NEW)
    services.AddScoped<ISpacedRepetitionService, SpacedRepetitionService>();
    services.AddScoped<ICollaborativeLearningService, CollaborativeLearningService>();
    services.AddScoped<ISmartTutorService, SmartTutorService>();
    services.AddScoped<IVideoPlayerService, VideoPlayerService>();
    
    // Enterprise Features - Course Builder, Analytics, Learning Paths
    services.AddScoped<ICourseBuilderService, CourseBuilderService>();
    services.AddScoped<IAdvancedAnalyticsService, AdvancedAnalyticsService>();
    services.AddScoped<ILearningPathService, LearningPathService>();
    
    // Register PDF Generation Service
    services.AddScoped<IPdfGenerationService, PdfGenerationService>();
    
    // Register Export Service (Unified CSV, Excel, PDF exports)
    services.AddScoped<IExportService, ExportService>();
    
    // Register Platform Settings Service
    services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
    services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();
    
    // Register Earnings Availability Service
    services.AddScoped<IEarningsAvailabilityService, EarningsAvailabilityService>();
    services.AddHostedService<EarningsAvailabilityBackgroundService>();
    
    // Register Book Services (NEW - Book Selling Feature)
    services.AddScoped<IBookService, BookService>();
    
    // Register Live Session Services (Enterprise Live Sessions Enhancement)
    services.AddScoped<ILiveSessionService, LiveSessionService>();
    services.AddHostedService<AttendanceTrackingService>();
    
    // Register Assessment Services (Quizzes & Assignments)
    services.AddScoped<IQuizService, QuizService>();
    services.AddScoped<IAssignmentService, AssignmentService>();
    services.AddScoped<IProctoringService, ProctoringService>();
    
    // Register Financial Reports Service
    services.AddScoped<IFinancialReportsService, FinancialReportsService>();
    
    // Register Finance Services (Enrollment, Invoices, Coupons, etc.)
    services.AddFinanceServices(configuration);
    
    // Register Enterprise Services (Health, Monitoring, Data Integrity)
    services.AddEnterpriseServices();
    
    // Register Enterprise Video Security Services
    services.AddEnterpriseVideoSecurityServices(configuration);
    
    // Register Database Seeder
    services.AddScoped<DatabaseSeeder>();

    // Add MVC and Razor Pages with Role Authorization Filter
    services.AddControllersWithViews(options =>
    {
        // Add global role authorization filter for better error handling
        options.Filters.Add<RoleAuthorizationFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to use camelCase (default) and be case-insensitive
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = false;
        // Allow string enum values to be converted to enum types
        // Use null naming policy to accept exact enum names (Video, Text, etc.)
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(
            namingPolicy: null, 
            allowIntegerValues: true));
    });
    services.AddRazorPages();
    
    // Register the RoleAuthorizationFilter
    services.AddScoped<RoleAuthorizationFilter>();
    
    // Register Instructor Validation Filter
    services.AddScoped<InstructorValidationFilter>();

    // Add HttpContextAccessor for accessing HttpContext in services
    services.AddHttpContextAccessor();

    // Configure Anti-Forgery tokens for secure forms (header used by AJAX e.g. video progress)
    services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "LMS.Antiforgery";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.HeaderName = "X-CSRF-TOKEN"; // AJAX sends token in this header
        options.FormFieldName = "__RequestVerificationToken";
    });

    // Data Protection: persist keys so login/registration work after deploy and across restarts
    var keysPath = Path.Combine(Directory.GetCurrentDirectory(), "DataProtection-Keys");
    try
    {
        if (!Directory.Exists(keysPath))
            Directory.CreateDirectory(keysPath);
        services.AddDataProtection()
            .SetApplicationName("LMS")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Data Protection key persistence could not use {Path}; using default key ring", keysPath);
    }

    // Configure Cookie Policy
    services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "LMS.Auth";
    });

    // Add CORS if needed for API
    services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Add Response Caching
    services.AddResponseCaching();

    // Add Memory Cache
    services.AddMemoryCache();

    // Add Distributed Cache (in-memory by default; replace with AddStackExchangeRedisCache for multi-server)
    services.AddDistributedMemoryCache();

    // Add Session support for error handling
    services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });
}

// Configure Pipeline
void ConfigurePipeline(WebApplication app)
{
    // Minimal startup check endpoint - bypasses all middleware for diagnostics
    app.Map("/startup-check", () => Results.Ok(new
    {
        status = "alive",
        time = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName,
        version = "1.0.0"
    }));
    
    // Enterprise Exception Handling and Logging Middleware
    app.UseEnterpriseMiddleware();
    
    // Legacy Exception Handling Middleware (for compatibility)
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        // Exception handling is already done by ExceptionHandlingMiddleware
        // Removed UseExceptionHandler to prevent duplicate handling and redirect loops
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = static ctx =>
        {
            var path = ctx.Context.Request.Path.Value ?? "";
            if (path.Equals("/sw.js", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers.Pragma = "no-cache";
            }
        }
    });

    // Cookie Policy - important for proper cookie handling
    app.UseCookiePolicy(new CookiePolicyOptions
    {
        MinimumSameSitePolicy = SameSiteMode.Lax,
        HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always,
        Secure = CookieSecurePolicy.SameAsRequest
    });

    // Handle status code pages (404, 403, etc.)
    // Note: 400 errors from controllers returning BadRequest() will still work
    // This only handles responses that already have a 4xx status code set before reaching the controller
    app.UseStatusCodePages(async context =>
    {
        var response = context.HttpContext.Response;
        var request = context.HttpContext.Request;
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        
        // Skip if response has already started (can't redirect)
        if (response.HasStarted)
        {
            logger.LogDebug("Response already started, skipping status code handling for {StatusCode}", response.StatusCode);
            return;
        }

        // Skip if this is an AJAX/API request (let the client handle the error)
        var acceptHeader = request.Headers["Accept"].ToString();
        var isApiRequest = request.Path.StartsWithSegments("/api") || 
                          (acceptHeader.Contains("application/json") && !acceptHeader.Contains("text/html"));
        
        if (isApiRequest)
        {
            logger.LogDebug("API request with status code {StatusCode}, not redirecting", response.StatusCode);
            return;
        }
        
        var isGetRequest = request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase);
        
        logger.LogWarning(
            "Status Code {StatusCode} detected: Path={Path}, Method={Method}, User={User}, Query={Query}",
            response.StatusCode,
            request.Path,
            request.Method,
            context.HttpContext.User?.Identity?.Name ?? "Anonymous",
            request.QueryString);

        // For GET requests with 400 errors, don't redirect - just let the response complete
        // This prevents redirect loops and allows the page to render with whatever content it has
        if (response.StatusCode == 400 && isGetRequest)
        {
            logger.LogWarning("400 error on GET request to {Path} - not redirecting to prevent loops", request.Path);
            return;
        }
        
        // Handle specific status codes
        if (response.StatusCode == 400)
        {
            // For POST/PUT/DELETE, redirect to error page
            response.Redirect("/Error/Error400");
        }
        else if (response.StatusCode == 401)
        {
            // Unauthorized - redirect to login
            var returnUrl = Uri.EscapeDataString(request.Path + request.QueryString);
            response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        }
        else if (response.StatusCode == 403)
        {
            response.Redirect("/Error/Error403");
        }
        else if (response.StatusCode == 404)
        {
            response.Redirect("/Error/Error404");
        }
        else if (response.StatusCode == 405)
        {
            response.Redirect("/Error/Error405");
        }
        else if (response.StatusCode == 408)
        {
            response.Redirect("/Error/Error408");
        }
        else if (response.StatusCode == 429)
        {
            response.Redirect("/Error/Error429");
        }
        else if (response.StatusCode == 502)
        {
            response.Redirect("/Error/Error502");
        }
        else if (response.StatusCode == 503)
        {
            response.Redirect("/Error/Error503");
        }
        else if (response.StatusCode == 504)
        {
            response.Redirect("/Error/Error504");
        }
        else if (response.StatusCode == 505)
        {
            response.Redirect("/Error/Error505");
        }
        else if (response.StatusCode >= 400 && response.StatusCode < 500)
        {
            if (!isGetRequest)
            {
                response.Redirect($"/Error/Index?statusCode={response.StatusCode}");
            }
        }
        else if (response.StatusCode >= 500)
        {
            response.Redirect("/Error/Error500");
        }
    });

    // Add Response Caching
    app.UseResponseCaching();

    app.UseRouting();

    // Add Session middleware
    app.UseSession();

    // CORS
    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    // Payment Rate Limiting Middleware
    app.UsePaymentRateLimiting();

    // Instructor Rate Limiting Middleware
    app.UseInstructorRateLimiting();

    // Request Localization for Arabic/English support
    var supportedCultures = new[] { "ar", "en" };
    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture("ar")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    // Cookie provider first (user's choice), then default
    localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());

    app.UseRequestLocalization(localizationOptions);

    // Map Routes
    // Explicit route for announcement details first - ensures /Announcements/Details/{id} always hits root controller
    app.MapControllerRoute(
        name: "public-announcement-details",
        pattern: "Announcements/Details/{id:int}",
        new { controller = "Announcements", action = "Details", area = (string?)null });
    // Public landing announcement view (anonymous) - /Announcements/ViewLanding/{id}
    app.MapControllerRoute(
        name: "public-announcement-view-landing",
        pattern: "Announcements/ViewLanding/{id:int}",
        new { controller = "Announcements", action = "ViewLanding", area = (string?)null });

    // System announcements in Student dashboard: /Student/Announcements -> root AnnouncementsController
    app.MapControllerRoute(
        name: "student-system-announcements",
        pattern: "Student/Announcements/{action=Index}/{id?}",
        new { controller = "Announcements", area = (string?)null });
    // System announcements in Instructor dashboard: /Instructor/PlatformAnnouncements -> root AnnouncementsController
    app.MapControllerRoute(
        name: "instructor-system-announcements",
        pattern: "Instructor/PlatformAnnouncements/{action=Index}/{id?}",
        new { controller = "Announcements", area = (string?)null });

    // Root URL "/" -> Home/Index: when landing enabled + anonymous shows landing; else redirects to Login or area dashboard
    app.MapControllerRoute(
        name: "root",
        pattern: "",
        new { controller = "Home", action = "Index" });

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}");

    app.MapRazorPages();
    
    // Map SignalR Hubs for real-time notifications
    app.MapHub<NotificationHub>("/hubs/notifications");
    
    // Map SignalR Hub for live class sessions
    app.MapHub<LiveClassHub>("/hubs/liveclass");
    
    // Enterprise Health Check Endpoints
    app.MapEnterpriseHealthChecks();
}

// Validate Database Connection
async Task ValidateDatabaseConnectionAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        Log.Information("Validating database connection...");
        
        // Use a timeout for database connection check to prevent hanging
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var canConnect = await dbContext.Database.CanConnectAsync(cts.Token);
        
        if (canConnect)
        {
            Log.Information("Database connection validated successfully");
            
            // Test a simple query to ensure the database is fully operational
            try
            {
                var quizCount = await dbContext.Quizzes.CountAsync(cts.Token);
                Log.Information("Database is operational. Current quiz count: {QuizCount}", quizCount);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Database connection validated but query test failed. This may indicate schema issues.");
            }
        }
        else
        {
            Log.Warning("Database connection validation failed - cannot connect to database. " +
                "Application will continue starting. Health checks will report database status.");
        }
    }
    catch (OperationCanceledException)
    {
        Log.Warning("Database connection validation timed out after 30 seconds. " +
            "Application will continue starting. Health checks will report database status.");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database connection validation failed during startup. " +
            "Application will continue starting. Health checks will report database status.");
    }
}

// Seed Database
async Task SeedDatabaseAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        
        Log.Information("Starting database seeding...");
        await seeder.SeedAsync();
        Log.Information("Database seeding completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding the database. The application will continue but some data may be missing.");
        // Don't throw - let the application continue even if seeding fails
    }
}
