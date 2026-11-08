using System.Globalization;
using System.Security.Claims;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

public sealed class AccountController(KimiaFaravar.Application.Security.IAuthenticationService authentication, IUserManagementService users) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await authentication.AuthenticateAsync(model.Username, model.Password, ct);
        if (user is null) { ModelState.AddModelError("", "نام کاربری یا گذرواژه نادرست است."); return View(model); }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.AliasName), new(ClaimTypes.Role, user.RoleCode), new("session_token", user.SessionToken)
        };
        claims.AddRange(user.Permissions.Select(permission => new Claim("permission", permission)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");
    }

    [Authorize, HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await users.ChangeOwnPasswordAsync(CurrentUserId(), model.CurrentPassword, model.NewPassword, ct);
            await HttpContext.SignOutAsync();
            TempData["PasswordChanged"] = "گذرواژه تغییر کرد. لطفاً با گذرواژه جدید وارد شوید.";
            return RedirectToAction(nameof(Login));
        }
        catch (UserManagementException ex) { ModelState.AddModelError("", ex.Message); return View(model); }
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = User.FindFirstValue("session_token");
        if (token is not null) await authentication.RevokeSessionAsync(token, ct);
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous] public IActionResult AccessDenied() => View();
    private long CurrentUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}