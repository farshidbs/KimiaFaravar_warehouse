using System.Security.Claims;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace KimiaFaravar.Web.Controllers;
public sealed class AccountController(KimiaFaravar.Application.Security.IAuthenticationService authentication):Controller
{
 [AllowAnonymous,HttpGet] public IActionResult Login(string? returnUrl=null)=>View(new LoginViewModel{ReturnUrl=returnUrl});
 [AllowAnonymous,HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Login(LoginViewModel model,CancellationToken ct)
 { if(!ModelState.IsValid)return View(model); var user=await authentication.AuthenticateAsync(model.Username,model.Password,ct); if(user is null){ModelState.AddModelError("","نام کاربری یا رمز عبور نادرست است.");return View(model);} var claims=new List<Claim>{new(ClaimTypes.NameIdentifier,user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),new(ClaimTypes.Name,user.AliasName),new(ClaimTypes.Role,user.RoleCode),new("session_token",user.SessionToken)}; claims.AddRange(user.Permissions.Select(x=>new Claim("permission",x))); await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme))); return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl)?model.ReturnUrl!:"/"); }
 [Authorize,HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Logout(CancellationToken ct){var token=User.FindFirstValue("session_token");if(token is not null)await authentication.RevokeSessionAsync(token,ct);await HttpContext.SignOutAsync();return RedirectToAction(nameof(Login));}
 [AllowAnonymous] public IActionResult AccessDenied()=>View();
}
