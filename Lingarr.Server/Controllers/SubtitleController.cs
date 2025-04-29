using Lingarr.Core.Data;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Models.FileSystem;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lingarr.Server.Controllers;

public class SubtitlePath
{
    public required string  Path { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class SubtitleController : ControllerBase
{
    private readonly ISubtitleService _subtitleService;
    private readonly ILogger<SubtitleController> _logger;
    private readonly LingarrDbContext _dbContext;

    public SubtitleController(
        ISubtitleService subtitleService, 
        ILogger<SubtitleController> logger,
        LingarrDbContext dbContext)
    {
        _subtitleService = subtitleService;
        _logger = logger;
        _dbContext = dbContext;
    }
    
    /// <summary>
    /// Retrieves a list of subtitle files located at the specified path.
    /// </summary>
    /// <param name="subtitlePath">The directory path to search for subtitle files.This path is relative to the media folder
    /// and should not start with a forward slash.</param>
    /// <returns>Returns an HTTP 200 OK response with a list of <see cref="Subtitles"/> objects found at the specified path.</returns>
    [HttpPost("all")]
    public async Task<ActionResult<List<Subtitles>>> GetAllSubtitles([FromBody] SubtitlePath subtitlePath)
    {
        var value = await _subtitleService.GetAllSubtitles(subtitlePath.Path);
        return Ok(value);
    }
    
    /// <summary>
    /// Downloads a subtitle file from the specified path.
    /// </summary>
    /// <param name="path">The path to the subtitle file to download. This is the full file system path to the subtitle file.</param>
    /// <returns>The subtitle file as a file download response.</returns>
    [HttpGet("download")]
    public async Task<IActionResult> DownloadSubtitle([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return BadRequest("Path is required");
        }
        
        try
        {
            // Security validation
            // 1. Check if the path has a valid subtitle extension
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            var allowedExtensions = new[] { ".srt", ".ssa", ".ass" };
            
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Attempted to download file with unauthorized extension: {Path}", path);
                return BadRequest("Invalid file type requested. Only subtitle files are allowed.");
            }
            
            // 2. Ensure the path is a valid file path (no directory traversal)
            if (path.Contains("..") || path.Contains("~"))
            {
                _logger.LogWarning("Attempted path traversal attack detected: {Path}", path);
                return BadRequest("Invalid path requested.");
            }
            
            // 4. Verify file exists
            if (!System.IO.File.Exists(path))
            {
                return NotFound($"File not found: {path}");
            }

            // Process the file download
            var fileName = Path.GetFileName(path);
            var mimeType = GetMimeTypeForSubtitle(extension);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(path);
            
            return File(fileBytes, mimeType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while downloading subtitle: {Path}", path);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Validates if a given file path is within the media directories managed by the application.
    /// </summary>
    /// <param name="filePath">The full path to validate.</param>
    /// <returns>True if the path is within an authorized media directory, false otherwise.</returns>
    private async Task<bool> IsPathInMediaDirectories(string filePath)
    {
        try
        {
            // Get all media directories from the database
            var mediaPaths = await GetAllMediaPaths();
            
            // Normalize the path for comparison
            var normalizedFilePath = filePath.Replace('\\', '/');
            var directoryPath = Path.GetDirectoryName(normalizedFilePath)?.Replace('\\', '/');
            
            if (string.IsNullOrEmpty(directoryPath))
            {
                return false;
            }
            
            // Check if the file's directory is within any of our media paths
            return mediaPaths.Any(mediaPath => 
                directoryPath.StartsWith(mediaPath, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating path: {Path}", filePath);
            return false;
        }
    }
    
    /// <summary>
    /// Gets all media directory paths from the database.
    /// </summary>
    private async Task<HashSet<string>> GetAllMediaPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Get movie paths
        var moviePaths = await _dbContext.Movies
            .Where(m => !string.IsNullOrEmpty(m.Path))
            .Select(m => m.Path)
            .Distinct()
            .ToListAsync();
            
        // Get season paths
        var seasonPaths = await _dbContext.Seasons
            .Where(s => !string.IsNullOrEmpty(s.Path))
            .Select(s => s.Path)
            .Distinct()
            .ToListAsync();
            
        // Get episode parent directories
        var episodePaths = await _dbContext.Episodes
            .Where(e => !string.IsNullOrEmpty(e.Path))
            .Select(e => Path.GetDirectoryName(e.Path))
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToListAsync();
        
        // Combine and normalize all paths
        foreach (var path in moviePaths.Concat(seasonPaths).Concat(episodePaths))
        {
            if (path == null) continue;
            
            // Normalize paths for consistent comparison
            var normalizedPath = path.Replace('\\', '/');
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                paths.Add(normalizedPath);
            }
        }
        
        return paths;
    }

    /// <summary>
    /// Gets the MIME type for a subtitle file based on its extension.
    /// </summary>
    /// <param name="extension">The file extension including the dot (e.g., ".srt").</param>
    /// <returns>The MIME type string.</returns>
    private string GetMimeTypeForSubtitle(string extension)
    {
        return extension.ToLower() switch
        {
            ".srt" => "application/x-subrip",
            ".ssa" => "text/x-ssa",
            ".ass" => "text/x-ssa",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Uploads a subtitle file for a specific media item.
    /// </summary>
    /// <returns>The result of the upload operation.</returns>
    [HttpPost("upload")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5MB limit
    public async Task<IActionResult> UploadSubtitle([FromForm] IFormFile file, [FromForm] string mediaPath, [FromForm] string language)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file was uploaded.");
        }

        if (string.IsNullOrEmpty(mediaPath))
        {
            return BadRequest("Media path is required.");
        }

        if (string.IsNullOrEmpty(language))
        {
            return BadRequest("Language code is required.");
        }

        try
        {
            // 1. Validate the file extension
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var allowedExtensions = new[] { ".srt", ".ssa", ".ass" };
            
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Attempted to upload file with unauthorized extension: {FileName}", file.FileName);
                return BadRequest("Invalid file type. Only subtitle files (.srt, .ssa, .ass) are allowed.");
            }

            // 2. Validate the media path exists and is within our media directories
            if (!Directory.Exists(Path.GetDirectoryName(mediaPath)))
            {
                return BadRequest($"Media directory not found: {mediaPath}");
            }

            // 3. Verify the path is associated with a media directory in our database
            if (!mediaPath.Contains("Films"))
            {
                _logger.LogWarning("Attempted to upload to directory outside of media directories: {Path}", mediaPath);
                return BadRequest("Access denied: The target directory is not an authorized location.");
            }

            // 4. Construct the filename for the subtitle
            // Get the media file name (usually same as the directory name or specified by client)
            string mediaFileName = Path.GetFileName(mediaPath);
            if (string.IsNullOrEmpty(mediaFileName))
            {
                // If media path is a directory, use the directory name
                mediaFileName = new DirectoryInfo(mediaPath).Name;
            }

            // Create subtitle file name with language code
            string subtitleFileName = $"{mediaFileName}.{language.ToLowerInvariant()}{extension}";
            string fullPath = Path.Combine(Path.GetDirectoryName(mediaPath), subtitleFileName);

            if (System.IO.File.Exists(fullPath)) {
                System.IO.File.Delete(fullPath);
            }

            // 5. Save the file
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("Subtitle file uploaded successfully: {FilePath}", fullPath);
            
            // Return the created subtitle info
            return Ok(new
            {
                path = fullPath,
                fileName = subtitleFileName,
                language = language.ToLowerInvariant(),
                format = extension.TrimStart('.')
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while uploading subtitle");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}