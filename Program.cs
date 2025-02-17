// File: Program.cs
using DoAn.Areas.Admin.Repository;
using DoAn.Helpers;
using DoAn.Models;
using DoAn.Models.ViewModel;
using DoAn.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// Cấu hình Serilog trước khi các dịch vụ khác được thêm vào
builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration) // Đọc cấu hình từ appsettings.json (nếu có)
    .WriteTo.Console() // Ghi log ra console
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day) // Ghi log ra file, tạo file mới mỗi ngày
    .Enrich.FromLogContext()
);

// Kết nối đến cơ sở dữ liệu
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConnectionDb"));
});

// Cấu hình MailChimp từ appsettings.json
builder.Services.Configure<MailChimpSettings>(builder.Configuration.GetSection("MailChimp"));
builder.Services.AddTransient<MailChimpService>();

// Đăng ký IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Đăng ký VnPayHelper
builder.Services.AddScoped<VnPayHelper>();

// Thêm dịch vụ email (đảm bảo bạn đã tạo lớp EmailSender hoặc sử dụng dịch vụ email phù hợp)  
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Thêm các dịch vụ khác
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.IsEssential = true;
});

// Thêm Identity
builder.Services.AddIdentity<AddUserModel, IdentityRole>()
    .AddEntityFrameworkStores<DataContext>()
    .AddDefaultTokenProviders();

// Cấu hình Identity
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
    options.User.RequireUniqueEmail = true;
});

// Cấu hình cookie xác thực
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// Thêm PayPal Client (nếu cần)
builder.Services.AddSingleton(x => new PaypalClient(
    builder.Configuration["PaypalOptions:AppId"],
    builder.Configuration["PaypalOptions:AppSecret"],
    builder.Configuration["PaypalOptions:Mode"]
));

var app = builder.Build();

// Middleware
app.UseStatusCodePagesWithRedirects("/Home/Error?statuscode={0}");
app.UseSession();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Thêm middleware SerilogRequestLogging để ghi log request
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

// Cấu hình các tuyến đường
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "productDetails",
    pattern: "san-pham/{slug}",
    defaults: new { controller = "Product", action = "Details" }
);

// Seed dữ liệu nếu cần
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    SeedData.SeedingData(context);
}

app.Run();
