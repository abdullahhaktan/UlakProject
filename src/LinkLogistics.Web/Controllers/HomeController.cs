using System.Diagnostics;
using LinkLogistics.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LinkLogistics.Web.Controllers;

public sealed class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
