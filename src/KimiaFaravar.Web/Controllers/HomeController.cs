using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KimiaFaravar.Web.Models;

namespace KimiaFaravar.Web.Controllers;

[Microsoft.AspNetCore.Authorization.Authorize]
public class HomeController(IConfiguration configuration) : Controller
{
    public IActionResult Index()
    {
        ViewBag.StorageProvider = configuration["Storage:Provider"] ?? "SqlServer";
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
