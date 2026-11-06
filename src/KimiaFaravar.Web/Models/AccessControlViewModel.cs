using KimiaFaravar.Application.Security;
namespace KimiaFaravar.Web.Models;
public sealed record AccessControlViewModel(IReadOnlyCollection<RoleSummary> Roles);
