using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MediaBrowser.Common.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CustomLogo;

public class LogoCopyService : IHostedService
{
    private const string IconTransparentPattern = "icon-transparent*";
    private const string TouchIconPattern = "touchicon*";
    private const string FaviconPattern = "favicon*";
    private const string BannerDarkPattern = "banner-dark*";
    private const string BannerLightPattern = "banner-light*";


    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<LogoCopyService> _logger;
    private readonly string _logoDirectory;

    public LogoCopyService(ILogger<LogoCopyService> logger, IApplicationPaths appPaths)
    {
        _logger = logger;
        _appPaths = appPaths;
        _logoDirectory = Path.Combine(_appPaths.PluginConfigurationsPath, Constants.FolderName);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        try
        {
            CopyLogosToWebPath();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CustomLogo: Failed to copy logos to web path");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void CopyLogosToWebPath()
    {
        if (!Directory.Exists(_logoDirectory))
        {
            _logger.LogInformation("CustomLogo: No custom logo directory found, skipping copy");
            return;
        }

        string webPath = _appPaths.WebPath;
        if (string.IsNullOrEmpty(webPath) || !Directory.Exists(webPath))
        {
            _logger.LogWarning("CustomLogo: Web path not found: {WebPath}", webPath);
            return;
        }

        string iconPath = Path.Combine(_logoDirectory, Constants.LogoFileName);
        string darkBannerPath = Path.Combine(_logoDirectory, Constants.BannerDarkFileName);
        string lightBannerPath = Path.Combine(_logoDirectory, Constants.BannerLightFileName);

        ReplaceFiles(iconPath, IconTransparentPattern, TouchIconPattern, FaviconPattern);
        ReplaceFiles(darkBannerPath, BannerDarkPattern);
        ReplaceFiles(lightBannerPath, BannerLightPattern);
    }

    private void ReplaceFiles(string pathToFile, params string[] patterns)
    {
        IReadOnlyCollection<string> filesToOverwrite = patterns
                                                       .SelectMany(pattern => Directory.GetFiles(_appPaths.WebPath, pattern, SearchOption.AllDirectories))
                                                       .ToList();

        _logger.LogDebug("Replacing following Files: {Files}", string.Join("\r\n", filesToOverwrite));

        foreach (string destinationFile in filesToOverwrite)
        {
            try
            {
                File.Copy(pathToFile, destinationFile, true);
                _logger.LogInformation("Copied {Source} to {Target}", pathToFile, destinationFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured while replacing file {FileName}", destinationFile);
            }
        }
    }
}