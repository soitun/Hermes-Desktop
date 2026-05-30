namespace Hermes.Agent.Skills;

using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// Client for discovering and installing skills from remote sources.
/// Sources: GitHub repos, well-known URLs, and community hubs.
/// All installed skills are security-scanned before activation.
/// </summary>
public sealed class SkillsHub
{
    private readonly SkillManager _skillManager;
    private readonly HttpClient _http;
    private readonly ILogger<SkillsHub> _logger;
    private readonly string _quarantineDir;

    public SkillsHub(
        SkillManager skillManager,
        string quarantineDir,
        ILogger<SkillsHub> logger,
        HttpClient? httpClient = null)
    {
        _skillManager = skillManager;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _logger = logger;
        _quarantineDir = quarantineDir;
        Directory.CreateDirectory(quarantineDir);
    }

    /// <summary>
    /// Search for skills from a GitHub repository.
    /// Format: owner/repo or full GitHub URL.
    /// </summary>
    public async Task<List<RemoteSkill>> SearchGitHubAsync(string repoPath, CancellationToken ct)
    {
        var results = new List<RemoteSkill>();

        try
        {
            repoPath = NormalizeRepoPath(repoPath);
            _http.DefaultRequestHeaders.UserAgent.TryParseAdd("HermesDesktop/1.0");
            await SearchGitHubDirectoryAsync(repoPath, "skills", depth: 0, results, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search GitHub repo: {Repo}", repoPath);
        }

        return results;
    }

    /// <summary>
    /// Install a skill from a remote URL.
    /// Downloads to quarantine, scans, then moves to skills directory.
    /// </summary>
    public async Task<InstallResult> InstallAsync(string name, string downloadUrl, CancellationToken ct)
    {
        try
        {
            var quarantinePath = Path.Combine(_quarantineDir, $"{name}.md");
            var content = await _http.GetStringAsync(downloadUrl, ct);
            await File.WriteAllTextAsync(quarantinePath, content, ct);

            if (Agent.Security.SecretScanner.ContainsSecrets(content))
            {
                File.Delete(quarantinePath);
                _logger.LogWarning("Skill '{Name}' from {Url} failed security scan and was deleted from quarantine", name, downloadUrl);
                return new InstallResult { Success = false, Error = "Skill contains secrets - blocked for security." };
            }

            if (!TryParseSkillDocument(content, out var frontmatter, out var body, out var parseError))
            {
                File.Delete(quarantinePath);
                return new InstallResult { Success = false, Error = parseError };
            }

            var skill = await _skillManager.CreateSkillAsync(
                name,
                string.IsNullOrWhiteSpace(frontmatter.Description)
                    ? $"Installed from {downloadUrl}"
                    : frontmatter.Description,
                body,
                SplitTools(frontmatter.Tools),
                string.IsNullOrWhiteSpace(frontmatter.Model) ? null : frontmatter.Model,
                "community",
                ct);

            File.Delete(quarantinePath);

            _logger.LogInformation("Installed skill '{Name}' from {Source}", name, downloadUrl);
            return new InstallResult { Success = true, Skill = skill };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to install skill '{Name}'", name);
            return new InstallResult { Success = false, Error = ex.Message };
        }
    }

    private async Task SearchGitHubDirectoryAsync(
        string repoPath,
        string contentsPath,
        int depth,
        List<RemoteSkill> results,
        CancellationToken ct)
    {
        var apiUrl = $"https://api.github.com/repos/{repoPath}/contents/{contentsPath}";
        var response = await _http.GetAsync(apiUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub API returned {Status} for {Repo}/{Path}", response.StatusCode, repoPath, contentsPath);
            return;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString();
            var type = item.GetProperty("type").GetString();
            var downloadUrl = item.TryGetProperty("download_url", out var dlEl) ? dlEl.GetString() : null;

            if (type == "file" && name?.EndsWith(".md", StringComparison.OrdinalIgnoreCase) == true && downloadUrl is not null)
            {
                results.Add(new RemoteSkill
                {
                    Name = Path.GetFileNameWithoutExtension(name),
                    Source = $"github:{repoPath}/{contentsPath}",
                    DownloadUrl = downloadUrl
                });
            }
            else if (type == "dir" && !string.IsNullOrWhiteSpace(name) && depth == 0)
            {
                results.Add(new RemoteSkill
                {
                    Name = name,
                    Source = $"github:{repoPath}/{contentsPath}/{name}",
                    DownloadUrl = null,
                    IsCategory = true
                });
                await SearchGitHubDirectoryAsync(repoPath, $"{contentsPath}/{name}", depth + 1, results, ct);
            }
        }
    }

    private static string NormalizeRepoPath(string repoPath)
    {
        if (repoPath.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(repoPath);
            repoPath = uri.AbsolutePath.Trim('/');
        }

        var parts = repoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0]}/{parts[1]}"
            : repoPath.Trim('/');
    }

    private static bool TryParseSkillDocument(
        string content,
        out SkillFrontmatter frontmatter,
        out string body,
        out string error)
    {
        frontmatter = new SkillFrontmatter();
        body = "";
        error = "";

        if (!content.StartsWith("---", StringComparison.Ordinal))
        {
            error = "Invalid skill format - missing YAML frontmatter.";
            return false;
        }

        var endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            error = "Invalid skill format - missing closing YAML frontmatter.";
            return false;
        }

        var yaml = content[3..endIndex].Trim();
        body = content[(endIndex + 3)..].Trim();
        frontmatter = ParseYamlFrontmatter(yaml);

        if (string.IsNullOrWhiteSpace(frontmatter.Name))
        {
            error = "Invalid skill format - missing skill name.";
            return false;
        }

        return true;
    }

    private static SkillFrontmatter ParseYamlFrontmatter(string yaml)
    {
        var frontmatter = new SkillFrontmatter();

        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex < 0)
                continue;

            var key = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim().Trim('"', '\'');

            switch (key.ToLowerInvariant())
            {
                case "name":
                    frontmatter.Name = value;
                    break;
                case "description":
                    frontmatter.Description = value;
                    break;
                case "tools":
                    frontmatter.Tools = value;
                    break;
                case "model":
                    frontmatter.Model = value;
                    break;
            }
        }

        return frontmatter;
    }

    private static List<string> SplitTools(string tools) =>
        string.IsNullOrWhiteSpace(tools)
            ? new List<string>()
            : tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public sealed class RemoteSkill
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public string? DownloadUrl { get; init; }
    public bool IsCategory { get; init; }
}

public sealed class InstallResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Skill? Skill { get; init; }
}
