using System.ComponentModel.DataAnnotations;
namespace KimiaFaravar.Web.Models;
public sealed class ChangePasswordViewModel
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(8)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}