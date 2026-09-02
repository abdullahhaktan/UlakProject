using System.Globalization;
using Ulak.Web.Api;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

var apiOptions = builder.Configuration.GetSection(ApiClientOptions.SectionName).Get<ApiClientOptions>()
                 ?? new ApiClientOptions();
var supportedCultures = builder.Configuration.GetSection("Localization:SupportedCultures").Get<string[]>()
                        ?? ["tr", "en"];
var defaultCulture = builder.Configuration["Localization:DefaultCulture"] ?? "tr";

// --- localization (TR / EN / ES via .resx) ---
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.DefaultRequestCulture = new RequestCulture(defaultCulture);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

// --- auth ---
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "Ulak.Panel";
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

// --- typed API client ---
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient(nameof(BearerTokenHandler), client => client.BaseAddress = new Uri(apiOptions.BaseUrl));
builder.Services
    .AddHttpClient<UlakApiClient>(client => client.BaseAddress = new Uri(apiOptions.BaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
