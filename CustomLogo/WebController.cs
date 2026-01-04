using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using MediaBrowser.Common.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CustomLogo;

[Route("logo")]
public class WebController : ControllerBase
{
    private const string IconTransparentPattern = "icon-transparent*";
    private const string TouchIconPattern = "touchicon*";
    private const string FaviconPattern = "favicon*";
    private const string BannerDarkPattern = "banner-dark*";
    private const string BannerLightPattern = "banner-light*";
    private readonly IApplicationPaths _appPaths;

    public WebController(IApplicationPaths appPaths)
    {
        _appPaths = appPaths;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadLogo()
    {
        IFormFile? logo = Request.Form.Files["Logo"];
        IFormFile? bannerDark = Request.Form.Files["BannerDark"];
        IFormFile? bannerLight = Request.Form.Files["BannerLight"];

        try
        {
            await ReplaceFiles(logo, IconTransparentPattern, TouchIconPattern, FaviconPattern);
            await ReplaceFiles(bannerDark, BannerDarkPattern);
            await ReplaceFiles(bannerLight, BannerLightPattern);
        }
        catch (UnauthorizedAccessException)
        {
            return Content(
                "<html><head></head><body>Jellyfin does not have write access. Please check the permissions and ensure the application has sufficient rights.<br><a href='/web/#/dashboard/plugins'>Retun to Jellyfin</a></body></html>",
                "text/html");
        }

        return Content(
            "<html><head><meta http-equiv='refresh' content='0;url=/web/#/dashboard/plugins' /></head><body>Redirection...</body></html>",
            "text/html");
    }

    private async Task ReplaceFiles(IFormFile? file, params string[] patterns)
    {
        if (file is null || file.Length == 0)
            return;

        IReadOnlyCollection<string> files = patterns
                                            .SelectMany(pattern => Directory.GetFiles(_appPaths.WebPath, pattern, SearchOption.AllDirectories))
                                            .ToList();

        foreach (string filePath in files)
        {
            await using (FileStream stream = new(filePath, FileMode.Create))
                await file.CopyToAsync(stream);
        }
    }
}