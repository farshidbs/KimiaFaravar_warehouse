using System.Globalization;
using System.Security.Claims;
using KimiaFaravar.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace KimiaFaravar.Web.Security;

public sealed class SessionCookieEvents(ISessionValidator validator) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var token = context.Principal?.FindFirstValue("session_token");
        var valid = long.TryParse(userIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            && token is not null && await validator.IsActiveAsync(userId, token, context.HttpContext.RequestAborted);
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
