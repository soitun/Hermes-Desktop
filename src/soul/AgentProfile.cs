namespace Hermes.Agent.Soul;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

/// <summary>
/// A saved agent configuration — soul + model + tools.
/// Users can create multiple agents and switch between them.
/// </summary>
public sealed class AgentProfile
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("soulContent")]
    public required string SoulContent { get; set; }

    [JsonPropertyName("soulTemplateName")]
    public string? SoulTemplateName { get; set; }

    [JsonPropertyName("preferredModel")]
    public string? PreferredModel { get; set; }

    [JsonPropertyName("preferredProvider")]
    public string? PreferredProvider { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Manages multiple agent profiles on disk.
/// Profiles stored as JSON in ~/.hermes-cs/agents/{name}.json
/// </summary>
public sealed class AgentProfileManager
{
    private readonly string _profilesDir;
    private readonly SoulService _soulService;
    private readonly ILogger<AgentProfileManager> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgentProfileManager(string profilesDir, SoulService soulService, ILogger<AgentProfileManager> logger)
    {
        _profilesDir = profilesDir;
        _soulService = soulService;
        _logger = logger;
        Directory.CreateDirectory(profilesDir);
    }

    /// <summary>List all saved agent profiles.</summary>
    public List<AgentProfile> ListProfiles()
    {
        var profiles = new List<AgentProfile>();
        if (!Directory.Exists(_profilesDir)) return profiles;

        foreach (var file in Directory.EnumerateFiles(_profilesDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<AgentProfile>(json, JsonOpts);
                if (profile is not null) profiles.Add(profile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load profile: {File}", file);
            }
        }

        return profiles.OrderBy(p => p.Name).ToList();
    }

    /// <summary>Save or update a profile.</summary>
    public async Task SaveProfileAsync(AgentProfile profile)
    {
        var filename = SanitizeName(profile.Name) + ".json";
        var path = Path.Combine(_profilesDir, filename);
        var json = JsonSerializer.Serialize(profile, JsonOpts);
        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Saved agent profile: {Name}", profile.Name);
    }

    /// <summary>Delete a profile.</summary>
    public void DeleteProfile(string name)
    {
        var filename = SanitizeName(name) + ".json";
        var path = Path.Combine(_profilesDir, filename);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("Deleted agent profile: {Name}", name);
        }
    }

    /// <summary>Activate a profile — copies its soul to SOUL.md.</summary>
    public async Task ActivateProfileAsync(AgentProfile profile)
    {
        // Deactivate all others
        foreach (var p in ListProfiles())
        {
            if (p.IsActive && p.Name != profile.Name)
            {
                p.IsActive = false;
                await SaveProfileAsync(p);
            }
        }

        // Activate this one
        profile.IsActive = true;
        await SaveProfileAsync(profile);

        // Apply soul content
        await _soulService.SaveFileAsync(SoulFileType.Soul, profile.SoulContent);
        _logger.LogInformation("Activated agent profile: {Name}", profile.Name);
    }

    /// <summary>Get the currently active profile name, or null.</summary>
    public string? GetActiveProfileName()
    {
        return ListProfiles().FirstOrDefault(p => p.IsActive)?.Name;
    }

    private static string SanitizeName(string name)
    {
        var sanitized = name.ToLowerInvariant().Replace(' ', '-');
        foreach (var c in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(c, '_');
        return sanitized;
    }
}
