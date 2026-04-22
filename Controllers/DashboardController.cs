using ChillTour.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChillTour.Controllers;

[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        if (RoleConstants.BackOffice.Any(User.IsInRole))
        {
            return RedirectToAction("Index", "Admin");
        }

        return RedirectToAction("Profile", "Account");
    }

    [Authorize(Policy = PermissionConstants.ManageUsers)]
    public IActionResult Manager()
    {
        return RedirectToAction("Users", "Admin");
    }

    [Authorize(Policy = PermissionConstants.ViewBookings)]
    public IActionResult Employee()
    {
        return RedirectToAction("Orders", "Admin");
    }

    [Authorize]
    public IActionResult Customer()
    {
        return RedirectToAction("Profile", "Account");
    }
}
