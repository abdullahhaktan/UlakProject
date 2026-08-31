using System.Diagnostics;
using Ulak.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Web.Controllers;

public sealed class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
