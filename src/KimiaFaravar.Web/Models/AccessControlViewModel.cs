using KimiaFaravar.Application.Security;
namespace KimiaFaravar.Web.Models;
public sealed record AccessControlViewModel(IReadOnlyCollection<RoleSummary> Roles, IReadOnlyCollection<UserSummary> Users,
    IReadOnlyCollection<UserAccessHistory> AccessHistory, long CurrentUserId);