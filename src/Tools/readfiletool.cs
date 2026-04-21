namespace Hermes.Agent.Tools;

using Hermes.Agent.Core;

/// <summary>
/// File reading tool with offset/limit support.
/// Supports text files, images, PDFs, and Jupyter notebooks.
/// </summary>
public sealed class ReadFileTool : ITool
{
    public string Name => "read_file";
    public string Description => "Read files from the local filesystem with optional offset/limit for large files";
    public Type ParametersType => typeof(ReadFileParameters);
    
    public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (ReadFileParameters)parameters;
        return ReadFileAsync(ResolvePath(p.FilePath), p.Offset, p.Limit, ct);
    }

    /// <summary>
    /// Resolve a tool-supplied path. Absolute paths are honored as-is; relative
    /// paths are resolved against <c>HERMES_DESKTOP_WORKSPACE</c> when set so the
    /// model can reference workspace files without knowing the absolute prefix
    /// (the v2.4.0 regression: relative paths silently bound to the process CWD,
    /// which on Windows installers is typically <c>C:\Windows\System32</c>).
    /// Falls back to <see cref="Directory.GetCurrentDirectory"/> when the env
    /// var is unset, preserving prior behavior for users who haven't opted in.
    /// </summary>
    private static string ResolvePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return filePath;
        if (Path.IsPathRooted(filePath)) return filePath;

        var workspace = Environment.GetEnvironmentVariable("HERMES_DESKTOP_WORKSPACE");
        var baseDir = !string.IsNullOrWhiteSpace(workspace) && Directory.Exists(workspace)
            ? workspace
            : Directory.GetCurrentDirectory();

        // Path.GetFullPath resolves ".." segments and normalizes separators —
        // we deliberately do NOT clamp inside the workspace because tools
        // legitimately read system files (config samples, bundled assets).
        return Path.GetFullPath(Path.Combine(baseDir, filePath));
    }

    private Task<ToolResult> ReadFileAsync(string filePath, int? offset, int? limit, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Task.FromResult(ToolResult.Fail($"File not found: {filePath}"));
            }
            
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Handle special file types
            if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp")
            {
                return Task.FromResult(ToolResult.Ok($"[IMAGE] {filePath} - Binary image file"));
            }
            
            if (extension == ".pdf")
            {
                return Task.FromResult(ToolResult.Ok($"[PDF] {filePath} - PDF file (text extraction not implemented)"));
            }
            
            if (extension == ".ipynb")
            {
                return ReadNotebookAsync(filePath, ct);
            }
            
            // Read text file
            var lines = File.ReadAllLines(filePath);
            
            // Apply offset/limit
            var start = offset ?? 0;
            var count = limit ?? lines.Length - start;
            var selectedLines = lines.Skip(start).Take(count).ToArray();
            
            // Format with line numbers (cat -n style)
            var output = string.Join("\n", selectedLines.Select((line, i) => $"{start + i + 1,6}: {line}"));

            // Record read timestamp for stale file detection
            FileReadTracker.RecordRead(filePath);

            return Task.FromResult(ToolResult.Ok(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"Failed to read file: {ex.Message}", ex));
        }
    }
    
    private async Task<ToolResult> ReadNotebookAsync(string filePath, CancellationToken ct)
    {
        try
        {
            // Simple notebook reader - just extract code cells
            var content = await File.ReadAllTextAsync(filePath, ct);
            return ToolResult.Ok($"[NOTEBOOK] {filePath}\n\n{content}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to read notebook: {ex.Message}", ex);
        }
    }
}

public sealed class ReadFileParameters
{
    public required string FilePath { get; init; }
    public int? Offset { get; init; }
    public int? Limit { get; init; }
}
