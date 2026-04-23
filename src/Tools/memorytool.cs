namespace Hermes.Agent.Tools;

using Hermes.Agent.Core;
using System.Text.Json;

/// <summary>
/// Manage persistent memories — save, list, and delete .md memory files.
/// </summary>
public sealed class MemoryTool : ITool
{
    private readonly string _memoryDir;

    public string Name => "memory";
    public string Description => "Manage persistent memories. Save, list, or delete memory files.";
    public Type ParametersType => typeof(MemoryToolParameters);

    public MemoryTool(string memoryDir)
    {
        _memoryDir = memoryDir;
        Directory.CreateDirectory(_memoryDir);
    }

    public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (MemoryToolParameters)parameters;

        return p.Action?.ToLowerInvariant() switch
        {
            "save" => SaveMemoryAsync(p.Content, ct),
            "list" => ListMemoriesAsync(ct),
            "delete" => DeleteMemoryAsync(p.Filename, ct),
            _ => Task.FromResult(ToolResult.Fail($"Unknown action: {p.Action}. Use save, list, or delete."))
        };
    }

    private async Task<ToolResult> SaveMemoryAsync(string? content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ToolResult.Fail("Content is required for save_memory.");

        var filename = $"memory_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}.md";
        var filePath = Path.Combine(_memoryDir, filename);

        // Ensure compatible YAML frontmatter so MemoryManager.ScanMemoryFilesAsync
        // can parse and surface this file. Without it, ParseFrontmatterAsync returns
        // null and the memory is silently dropped from retrieval — the v2.4.0
        // regression where agent-saved memories never resurfaced in later turns.
        // We only inject when the content does NOT already start with a fence;
        // models that produce well-formed frontmatter must not be double-wrapped.
        //
        // StripLeadingBlankLines (not full TrimStart) is important: MemoryManager
        // requires lines[0].Trim() == "---", so a content that begins with blank
        // lines followed by "---" must have those blanks removed before writing.
        var normalized = StripLeadingBlankLines(content!);
        var payload = HasFrontmatter(normalized)
            ? normalized
            : BuildDefaultFrontmatter(filename) + normalized;

        await File.WriteAllTextAsync(filePath, payload, ct);
        return ToolResult.Ok($"Memory saved: {filename}");
    }

    /// <summary>
    /// Strip BOM and any leading blank lines (including whitespace-only lines)
    /// so the resulting first line is the real content. MemoryManager reads
    /// <c>lines[0]</c> and rejects the file if that line isn't <c>---</c>.
    /// </summary>
    private static string StripLeadingBlankLines(string content)
    {
        var span = content.AsSpan();
        if (span.Length > 0 && span[0] == '﻿') span = span[1..]; // BOM

        int i = 0;
        while (i < span.Length)
        {
            // Scan to end of current line
            int lineEnd = i;
            while (lineEnd < span.Length && span[lineEnd] != '\n') lineEnd++;
            var line = span[i..lineEnd];
            bool isBlank = true;
            foreach (var c in line)
            {
                if (c is not (' ' or '\t' or '\r')) { isBlank = false; break; }
            }
            if (!isBlank) break;
            i = lineEnd < span.Length ? lineEnd + 1 : lineEnd;
        }
        return i == 0 && span.Length == content.Length
            ? content
            : span[i..].ToString();
    }

    /// <summary>True when content already opens with a YAML frontmatter fence.</summary>
    private static bool HasFrontmatter(string content)
    {
        // After StripLeadingBlankLines the content must start exactly with "---",
        // matching MemoryManager.ParseFrontmatterAsync's lines[0].Trim() == "---" check.
        return content.Length >= 3 && content[0] == '-' && content[1] == '-' && content[2] == '-';
    }

    /// <summary>
    /// Build a minimal frontmatter block whose keys (name, description, type) match
    /// the <c>MemoryFrontmatter</c> contract used by MemoryManager. Description is
    /// derived from the filename rather than guessed content, so the tool stays
    /// deterministic and never invents semantic metadata it doesn't have.
    /// </summary>
    private static string BuildDefaultFrontmatter(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        return $"---\nname: {name}\ndescription: Auto-saved by memory tool\ntype: user\n---\n";
    }

    private Task<ToolResult> ListMemoriesAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_memoryDir))
            return Task.FromResult(ToolResult.Ok("No memories found."));

        var files = Directory.GetFiles(_memoryDir, "*.md")
            .Select(Path.GetFileName)
            .OrderByDescending(f => f)
            .ToArray();

        if (files.Length == 0)
            return Task.FromResult(ToolResult.Ok("No memories found."));

        return Task.FromResult(ToolResult.Ok(string.Join("\n", files!)));
    }

    private Task<ToolResult> DeleteMemoryAsync(string? filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return Task.FromResult(ToolResult.Fail("Filename is required for delete_memory."));

        var filePath = Path.Combine(_memoryDir, filename);

        if (!File.Exists(filePath))
            return Task.FromResult(ToolResult.Fail($"Memory file not found: {filename}"));

        File.Delete(filePath);
        return Task.FromResult(ToolResult.Ok($"Memory deleted: {filename}"));
    }
}

public sealed class MemoryToolParameters
{
    public required string Action { get; init; }
    public string? Content { get; init; }
    public string? Filename { get; init; }
}
