using Microsoft.AspNetCore.Mvc;

namespace NightmareBot.Controllers;

[ApiController]
[Route("[controller]")]
public class ResultController : ControllerBase
{
    private readonly ILogger<ResultController> _logger;

    public ResultController(ILogger<ResultController> logger)
    {
        _logger = logger;
    }

    [HttpPut("{path}/{filename}.png")]
    public async Task<IActionResult> Put(string path, string filename)
    {
        var file = this.Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            return BadRequest();
        }

        string outPath = $"result/{path}";
        Directory.CreateDirectory(outPath);
        await using var outFile = new FileStream($"{outPath}/{filename}.png", FileMode.Create);
        await file.CopyToAsync(outFile);
        return Ok();
    }
}