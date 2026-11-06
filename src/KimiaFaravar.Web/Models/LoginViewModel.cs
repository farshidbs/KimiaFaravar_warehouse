using System.ComponentModel.DataAnnotations;
namespace KimiaFaravar.Web.Models;
public sealed class LoginViewModel
{
 [Required(ErrorMessage="نام کاربری الزامی است.")] public string Username { get; set; }="";
 [Required(ErrorMessage="رمز عبور الزامی است.")] [DataType(DataType.Password)] public string Password { get; set; }="";
 public string? ReturnUrl { get; set; }
}
