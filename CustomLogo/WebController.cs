using System;
using System.IO;
using System.Threading.Tasks;

using MediaBrowser.Common.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CustomLogo;

[Route("logo")]
public class WebController : ControllerBase
{
    private readonly ILogger<WebController> _logger;
    private readonly string _logoDirectory;

    public WebController(
        IApplicationPaths appPaths,
        ILogger<WebController> logger)
    {
        _logger = logger;

        _logoDirectory = Path.Combine(appPaths.PluginConfigurationsPath, Constants.FolderName);

        if (!Directory.Exists(_logoDirectory))
            Directory.CreateDirectory(_logoDirectory);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadLogo()
    {
        IFormFile? logo = Request.Form.Files["Logo"];
        IFormFile? bannerDark = Request.Form.Files["BannerDark"];
        IFormFile? bannerLight = Request.Form.Files["BannerLight"];

        try
        {
            await SaveFile(logo, Constants.LogoFileName);
            await SaveFile(bannerDark, Constants.BannerDarkFileName);
            await SaveFile(bannerLight, Constants.BannerLightFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occured while saving files");
            
            return Content(
                $"<html><head></head><body>{ex.Message}</body></html>",
                "text/html");
        }

        return Content(
            "<html><head><meta http-equiv='refresh' content='0;url=/web/#/dashboard/plugins' /></head><body>Redirection...</body></html>",
            "text/html");
    }

    private async Task SaveFile(IFormFile? file, string fileName)
    {
        if (file is null || file.Length == 0)
            return;

        string filePath = Path.Combine(_logoDirectory, fileName);

        await using (FileStream stream = new(filePath, FileMode.Create))
            await file.CopyToAsync(stream);
    }
}