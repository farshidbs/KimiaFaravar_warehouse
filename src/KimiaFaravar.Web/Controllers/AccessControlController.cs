using System.Globalization;
using System.Security.Claims;
using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

[Authorize(Policy = PermissionCodes.RolesManage)]
public sealed class AccessControlController(IAccessControlQueryService roles, IUserManagementService users, IAccessHistoryService history) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var currentUserId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
        return View(new AccessControlViewModel(await roles.GetRolesAsync(ct), await users.GetUsersAsync(ct), await history.GetRecentAsync(100, ct), currentUserId));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string aliasName, string username, string password, long roleId, CancellationToken ct)
    {
        try { await users.CreateUserAsync(new(aliasName, username, password, roleId), ct); TempData["Success"] = "کاربر جدید با موفقیت ایجاد شد."; }
        catch (UserManagementException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(long userId, long roleId, CancellationToken ct)
    {
        try { await users.ChangeRoleAsync(userId, roleId, CurrentUserId(), ct); TempData["Success"] = "نقش کاربر تغییر کرد. نشست‌های قبلی او باطل شدند."; }
        catch (UserManagementException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(long userId, string newPassword, CancellationToken ct)
    {
        try { await users.ResetPasswordAsync(userId, newPassword, CurrentUserId(), ct); TempData["Success"] = "گذرواژه کاربر بازنشانی و نشست‌های قبلی او باطل شد."; }
        catch (UserManagementException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableUser(long userId, CancellationToken ct)
    {
        try { await users.DisableUserAsync(userId, CurrentUserId(), ct); TempData["Success"] = "کاربر به‌صورت منطقی حذف و غیرفعال شد."; }
        catch (UserManagementException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableUser(long userId, CancellationToken ct)
    {
        try { await users.EnableUserAsync(userId, ct); TempData["Success"] = "حساب کاربر دوباره فعال شد."; }
        catch (UserManagementException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private long CurrentUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}