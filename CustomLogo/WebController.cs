using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MediaBrowser.Common.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CustomLogo;

/// <summary>
/// Web controller for handling custom logo upload and serving.
/// </summary>
[ApiController]
public partial class WebController : ControllerBase
{
    private const string ImageContentType = "image/png";
    private const string HtmlContentType = "text/html";

    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<WebController> _logger;
    private readonly ILogoCopyService _logoCopyService;
    private readonly string _logoDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebController" /> class.
    /// </summary>
    /// <param name="appPaths">Application paths service.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="logoCopyService">Logo copy service for applying changes to web path.</param>
    public WebController(
        IApplicationPaths appPaths,
        ILogger<WebController> logger,
        ILogoCopyService logoCopyService)
    {
        _logger = logger;
        _appPaths = appPaths;
        _logoCopyService = logoCopyService;

        _logoDirectory = Path.Combine(appPaths.PluginConfigurationsPath, Constants.FolderName);

        EnsureLogoDirectoryExists();
    }

    /// <summary>
    /// Ensures the logo directory exists, creating it if necessary.
    /// </summary>
    private void EnsureLogoDirectoryExists()
    {
        if (Directory.Exists(_logoDirectory))
            return;

        _logger.LogInformation("Creating logo directory at: {LogoDirectory}", _logoDirectory);
        Directory.CreateDirectory(_logoDirectory);
    }

    // ==================== UPLOAD ENDPOINT ====================

    /// <summary>
    /// Uploads custom logo files (icon, dark banner, and light banner).
    /// </summary>
    /// <returns>HTML response with redirect or error message.</returns>
    [HttpPost("logo/upload")]
    public async Task<IActionResult> UploadLogo()
    {
        IFormFile? logo = Request.Form.Files["Logo"];
        IFormFile? bannerDark = Request.Form.Files["BannerDark"];
        IFormFile? bannerLight = Request.Form.Files["BannerLight"];

        try
        {
            await SaveFileAsync(logo, Constants.LogoFileName);
            await SaveFileAsync(bannerDark, Constants.BannerDarkFileName);
            await SaveFileAsync(bannerLight, Constants.BannerLightFileName);

            _logger.LogInformation("Successfully uploaded logo files, applying changes to web path");
            await _logoCopyService.StartAsync(CancellationToken.None);

            return CreateRedirectResponse();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access while uploading logo files");
            return CreateErrorResponse("Jellyfin does not have write access to the plugin data directory. Please check the permissions.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while uploading logo files");
            return CreateErrorResponse($"An error occurred: {ex.Message}");
        }
    }

    // ==================== INTERCEPT ORIGINAL LOGO PATHS ====================

    /// <summary>
    /// Intercepts requests for icon-transparent.png and serves custom logo if available.
    /// </summary>
    /// <returns>Custom or original logo.</returns>
    [HttpGet("web/assets/img/icon-transparent.png")]
    public IActionResult GetIconTransparent()
    {
        return ServeLogoOrFallback(Constants.LogoFileName, "assets/img/icon-transparent.png");
    }

    /// <summary>
    /// Intercepts requests for banner-dark.png and serves custom banner if available.
    /// </summary>
    /// <returns>Custom or original banner.</returns>
    [HttpGet("web/assets/img/banner-dark.png")]
    public IActionResult GetBannerDarkOriginal()
    {
        return ServeLogoOrFallback(Constants.BannerDarkFileName, "assets/img/banner-dark.png");
    }

    /// <summary>
    /// Intercepts requests for banner-light.png and serves custom banner if available.
    /// </summary>
    /// <returns>Custom or original banner.</returns>
    [HttpGet("web/assets/img/banner-light.png")]
    public IActionResult GetBannerLightOriginal()
    {
        return ServeLogoOrFallback(Constants.BannerLightFileName, "assets/img/banner-light.png");
    }

    // ==================== INTERCEPT HASHED LOGO PATHS (Webpack bundles) ====================

    /// <summary>
    /// Intercepts requests for hashed icon-transparent files (e.g., icon-transparent.baba78f2a106d9baee83.png).
    /// </summary>
    /// <param name="hash">The webpack hash in the filename.</param>
    /// <returns>Custom or original logo.</returns>
    [HttpGet("web/icon-transparent.{hash}.png")]
    public IActionResult GetIconTransparentHashed(string hash)
    {
        if (IsValidHash(hash))
            return ServeLogoOrFallback(Constants.LogoFileName, $"icon-transparent.{hash}.png");

        _logger.LogWarning("Invalid hash format for icon-transparent: {Hash}", hash);
        return BadRequest();
    }

    /// <summary>
    /// Intercepts requests for hashed banner-dark files.
    /// </summary>
    /// <param name="hash">The webpack hash in the filename.</param>
    /// <returns>Custom or original banner.</returns>
    [HttpGet("web/banner-dark.{hash}.png")]
    public IActionResult GetBannerDarkHashed(string hash)
    {
        if (IsValidHash(hash))
            return ServeLogoOrFallback(Constants.BannerDarkFileName, $"banner-dark.{hash}.png");

        _logger.LogWarning("Invalid hash format for banner-dark: {Hash}", hash);
        return BadRequest();
    }

    /// <summary>
    /// Intercepts requests for hashed banner-light files.
    /// </summary>
    /// <param name="hash">The webpack hash in the filename.</param>
    /// <returns>Custom or original banner.</returns>
    [HttpGet("web/banner-light.{hash}.png")]
    public IActionResult GetBannerLightHashed(string hash)
    {
        if (IsValidHash(hash))
            return ServeLogoOrFallback(Constants.BannerLightFileName, $"banner-light.{hash}.png");

        _logger.LogWarning("Invalid hash format for banner-light: {Hash}", hash);
        return BadRequest();
    }

    // ==================== DIRECT LOGO ENDPOINTS ====================

    /// <summary>
    /// Gets the custom icon directly.
    /// </summary>
    /// <returns>Icon image or not found.</returns>
    [HttpGet("logo/icon")]
    public IActionResult GetIcon()
    {
        return GetFile(Constants.LogoFileName);
    }

    /// <summary>
    /// Gets the custom dark banner directly.
    /// </summary>
    /// <returns>Banner image or not found.</returns>
    [HttpGet("logo/banner-dark")]
    public IActionResult GetBannerDark()
    {
        return GetFile(Constants.BannerDarkFileName);
    }

    /// <summary>
    /// Gets the custom light banner directly.
    /// </summary>
    /// <returns>Banner image or not found.</returns>
    [HttpGet("logo/banner-light")]
    public IActionResult GetBannerLight()
    {
        return GetFile(Constants.BannerLightFileName);
    }

    /// <summary>
    /// Gets the logo status (which logos are set).
    /// </summary>
    /// <returns>JSON with logo status.</returns>
    [HttpGet("logo/status")]
    public IActionResult GetStatus()
    {
        var status = new
                     {
                         iconSet = FileExists(Constants.LogoFileName),
                         bannerDarkSet = FileExists(Constants.BannerDarkFileName),
                         bannerLightSet = FileExists(Constants.BannerLightFileName)
                     };

        return Ok(status);
    }

    // ==================== DELETE ENDPOINTS ====================

    /// <summary>
    /// Deletes the custom icon.
    /// </summary>
    /// <returns>OK or not found.</returns>
    [HttpDelete("logo/icon")]
    public async Task<IActionResult> DeleteIcon()
    {
        IActionResult result = DeleteFile(Constants.LogoFileName);

        if (result is OkResult)
            await _logoCopyService.StartAsync(CancellationToken.None);

        return result;
    }

    /// <summary>
    /// Deletes the custom dark banner.
    /// </summary>
    /// <returns>OK or not found.</returns>
    [HttpDelete("logo/banner-dark")]
    public async Task<IActionResult> DeleteBannerDark()
    {
        IActionResult result = DeleteFile(Constants.BannerDarkFileName);

        if (result is OkResult)
            await _logoCopyService.StartAsync(CancellationToken.None);

        return result;
    }

    /// <summary>
    /// Deletes the custom light banner.
    /// </summary>
    /// <returns>OK or not found.</returns>
    [HttpDelete("logo/banner-light")]
    public async Task<IActionResult> DeleteBannerLight()
    {
        IActionResult result = DeleteFile(Constants.BannerLightFileName);

        if (result is OkResult)
            await _logoCopyService.StartAsync(CancellationToken.None);

        return result;
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Validates that a hash string only contains safe alphanumeric characters.
    /// </summary>
    /// <param name="hash">The hash to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    private static bool IsValidHash(string hash)
    {
        return !string.IsNullOrEmpty(hash) &&
               // Only allow alphanumeric characters (webpack hashes are hex)
               HashRegex().IsMatch(hash);
    }

    /// <summary>
    /// Serves a custom logo if available, otherwise falls back to the original.
    /// </summary>
    /// <param name="customFileName">The custom logo filename.</param>
    /// <param name="originalRelativePath">The original logo relative path.</param>
    /// <returns>Image file result or not found.</returns>
    private IActionResult ServeLogoOrFallback(string customFileName, string originalRelativePath)
    {
        string customPath = Path.Combine(_logoDirectory, customFileName);

        // If custom logo exists, serve it
        if (System.IO.File.Exists(customPath))
        {
            _logger.LogDebug("Serving custom logo: {CustomPath}", customPath);
            return ServeImageFile(customPath);
        }

        // Otherwise, serve the original from the web path
        string originalPath = Path.Combine(_appPaths.WebPath, originalRelativePath);

        if (System.IO.File.Exists(originalPath))
        {
            _logger.LogDebug("Serving original logo: {OriginalPath}", originalPath);
            return ServeImageFile(originalPath);
        }

        _logger.LogWarning("Logo not found - Custom: {CustomPath}, Original: {OriginalPath}", customPath, originalPath);
        return NotFound();
    }

    /// <summary>
    /// Serves an image file from the specified path.
    /// </summary>
    /// <param name="filePath">The file path to serve.</param>
    /// <returns>Image file result.</returns>
    private IActionResult ServeImageFile(string filePath)
    {
        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(filePath);
            return File(bytes, ImageContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading image file: {FilePath}", filePath);
            return NotFound();
        }
    }

    /// <summary>
    /// Gets a logo file from the logo directory.
    /// </summary>
    /// <param name="fileName">The logo filename.</param>
    /// <returns>Image file result or not found.</returns>
    private IActionResult GetFile(string fileName)
    {
        string path = Path.Combine(_logoDirectory, fileName);

        if (!System.IO.File.Exists(path))
        {
            _logger.LogDebug("Logo file not found: {Path}", path);
            return NotFound();
        }

        return ServeImageFile(path);
    }

    /// <summary>
    /// Deletes a logo file from the logo directory.
    /// </summary>
    /// <param name="fileName">The logo filename to delete.</param>
    /// <returns>OK or not found.</returns>
    private IActionResult DeleteFile(string fileName)
    {
        string path = Path.Combine(_logoDirectory, fileName);

        if (!System.IO.File.Exists(path))
            return NotFound();

        try
        {
            System.IO.File.Delete(path);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logo file: {Path}", path);
            return StatusCode(500, "Error deleting file");
        }
    }

    /// <summary>
    /// Checks if a logo file exists in the logo directory.
    /// </summary>
    /// <param name="fileName">The logo filename to check.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    private bool FileExists(string fileName)
    {
        string path = Path.Combine(_logoDirectory, fileName);
        return System.IO.File.Exists(path);
    }

    /// <summary>
    /// Saves an uploaded file to the logo directory.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="fileName">The filename to save as.</param>
    private async Task SaveFileAsync(IFormFile? file, string fileName)
    {
        if (file is null || file.Length == 0)
            return;

        string filePath = Path.Combine(_logoDirectory, fileName);

        await using (FileStream stream = new(filePath, FileMode.Create))
            await file.CopyToAsync(stream);
    }

    /// <summary>
    /// Creates an HTML redirect response to the Jellyfin dashboard.
    /// </summary>
    /// <returns>HTML content result with redirect.</returns>
    private IActionResult CreateRedirectResponse()
    {
        const string html = "<html><head><meta http-equiv='refresh' content='0;url=/web/#/dashboard/plugins' /></head>" +
                            "<body>Redirection...</body></html>";

        return Content(html, HtmlContentType);
    }

    /// <summary>
    /// Creates an HTML error response with a message and link back to the dashboard.
    /// </summary>
    /// <param name="errorMessage">The error message to display.</param>
    /// <returns>HTML content result with error message.</returns>
    private IActionResult CreateErrorResponse(string errorMessage)
    {
        string html = $"<html><head></head><body>{errorMessage}<br></body></html>";
        return Content(html, HtmlContentType);
    }

    [GeneratedRegex("^[a-zA-Z0-9]+$")]
    private static partial Regex HashRegex();
}