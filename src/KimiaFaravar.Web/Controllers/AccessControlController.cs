using KimiaFaravar.Application.Security;
using KimiaFaravar.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimiaFaravar.Web.Controllers;

[Authorize(Policy = PermissionCodes.RolesManage)]
public sealed class AccessControlController(IAccessControlQueryService service) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(new AccessControlViewModel(await service.GetRolesAsync(ct)));
}
