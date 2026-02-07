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

/// <summary>
/// Background service that copies custom logos to the web path on startup.
/// </summary>
public class LogoCopyService : IHostedService, ILogoCopyService
{
    private const string IconTransparentPattern = "icon-transparent*";
    private const string TouchIconPattern = "touchicon*";
    private const string FaviconPattern = "favicon*";
    private const string BannerDarkPattern = "banner-dark*";
    private const string BannerLightPattern = "banner-light*";

    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<LogoCopyService> _logger;
    private readonly string _logoDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoCopyService" /> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="appPaths">Application paths service.</param>
    public LogoCopyService(ILogger<LogoCopyService> logger, IApplicationPaths appPaths)
    {
        _logger = logger;
        _appPaths = appPaths;
        _logoDirectory = Path.Combine(_appPaths.PluginConfigurationsPath, Constants.FolderName);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CustomLogo service executing logo copy operation");

        await Task.CompletedTask;

        try
        {
            CopyLogosToWebPath();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CustomLogo service failed to copy logos to web path");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Copies custom logos from plugin directory to the web path.
    /// </summary>
    private void CopyLogosToWebPath()
    {
        if (!ValidateLogoDirectory())
            return;

        if (!ValidateWebPath())
            return;

        ReplaceFiles(Constants.LogoFileName, IconTransparentPattern, TouchIconPattern, FaviconPattern);
        ReplaceFiles(Constants.BannerDarkFileName, BannerDarkPattern);
        ReplaceFiles(Constants.BannerLightFileName, BannerLightPattern);

        _logger.LogInformation("Logo copy operation completed");
    }

    /// <summary>
    /// Validates that the logo directory exists.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    private bool ValidateLogoDirectory()
    {
        if (Directory.Exists(_logoDirectory))
            return true;

        _logger.LogInformation("Custom logo directory not found at {LogoDirectory}, skipping copy operation",
            _logoDirectory);

        return false;
    }

    /// <summary>
    /// Validates that the web path exists.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    private bool ValidateWebPath()
    {
        string webPath = _appPaths.WebPath;

        return !string.IsNullOrEmpty(webPath) && Directory.Exists(webPath);
    }

    /// <summary>
    /// Copies a logo file to all matching pattern files in the web path.
    /// </summary>
    /// <param name="logoFileName">The source logo filename.</param>
    /// <param name="patterns">The patterns to match for destination files.</param>
    private void ReplaceFiles(string logoFileName, params string[] patterns)
    {
        string sourcePath = Path.Combine(_logoDirectory, logoFileName);

        if (!File.Exists(sourcePath))
            return;

        _logger.LogInformation("Processing logo file: {LogoFileName}", logoFileName);

        IReadOnlyCollection<string> destinationFiles = GetDestinationFiles(patterns);

        if (destinationFiles.Count == 0)
        {
            _logger.LogWarning("No destination files found for patterns: {Patterns}",
                string.Join(", ", patterns));
            return;
        }

        CopyFileToDestinations(sourcePath, destinationFiles);
    }

    /// <summary>
    /// Gets all destination files matching the specified patterns.
    /// </summary>
    /// <param name="patterns">The file patterns to search for.</param>
    /// <returns>Collection of matching file paths.</returns>
    private IReadOnlyCollection<string> GetDestinationFiles(params string[] patterns)
    {
        return patterns
               .SelectMany(pattern => Directory.GetFiles(_appPaths.WebPath, pattern, SearchOption.AllDirectories))
               .ToList();
    }

    /// <summary>
    /// Copies the source file to all destination files.
    /// </summary>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="destinationFiles">The destination file paths.</param>
    private void CopyFileToDestinations(string sourcePath, IReadOnlyCollection<string> destinationFiles)
    {
        foreach (string destinationFile in destinationFiles)
        {
            try
            {
                File.Copy(sourcePath, destinationFile, true);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access while copying to {DestinationFile}",
                    destinationFile);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error while copying to {DestinationFile}",
                    destinationFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while copying to {DestinationFile}",
                    destinationFile);
            }
        }
    }
}