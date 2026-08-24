using Microsoft.AspNetCore.Mvc;

namespace growcery.Controllers;

[ApiController]
[Route("api")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        return Ok(new { name = "Growcery API", status = "ok" });
    }

}
