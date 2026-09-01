using System.Security.Claims;
using Ulak.Web.Api;
using Ulak.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly UlakApiClient _api;

    public AccountController(UlakApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var auth = await _api.LoginAsync(model.Phone.Trim(), model.Password, ct);
        if (auth is null)
        {
            model.Error = "InvalidCredentials";
            return View(model);
        }

        if (!string.Equals(auth.User.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            model.Error = "OpsOnly";
            return View(model);
        }

        await SignInWithTokensAsync(auth);

        return Url.IsLocalUrl(model.ReturnUrl)
            ? Redirect(model.ReturnUrl!)
            : RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult SignUp() => View(new SignUpViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignUp(SignUpViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var auth = await _api.SignUpAsync(
                new Ulak.Shared.Auth.SignUpRequest(
                    model.CompanyName.Trim(), model.AdminName.Trim(), model.Phone.Trim(), model.Password),
                ct);

            await SignInWithTokensAsync(auth);
            return RedirectToAction("Index", "Dashboard");
        }
        catch (ApiException ex)
        {
            model.Error = ex.StatusCode == 409
                ? "Bu telefon numarası zaten kayıtlı."
                : ex.Message;
            return View(model);
        }
    }

    private async Task SignInWithTokensAsync(Ulak.Shared.Auth.AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.Id.ToString()),
            new(ClaimTypes.Name, auth.User.Name),
            new(ClaimTypes.MobilePhone, auth.User.Phone),
            new(ClaimTypes.Role, auth.User.Role),
        };
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var props = new AuthenticationProperties { IsPersistent = true };
        props.StoreTokens(
        [
            new AuthenticationToken { Name = TokenNames.AccessToken, Value = auth.AccessToken },
            new AuthenticationToken { Name = TokenNames.RefreshToken, Value = auth.RefreshToken },
            new AuthenticationToken
            {
                Name = TokenNames.ExpiresAt,
                Value = DateTimeOffset.UtcNow.AddSeconds(auth.ExpiresInSeconds).ToString("o"),
            },
        ]);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult SetLanguage(string culture, string? returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
