using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Hermes.Agent.Skills;

namespace HermesDesktop.Views;

public sealed partial class SkillsPage : Page
{
    private List<SkillDisplayItem> _allSkills = new();

    public SkillsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        try
        {
            var skillManager = App.Services.GetRequiredService<SkillManager>();
            var skills = skillManager.ListSkills();

            _allSkills = skills.Select(s => new SkillDisplayItem
            {
                Name = s.Name,
                Description = s.Description,
                Category = DeriveCategory(s),
                SystemPrompt = s.SystemPrompt,
                Tools = string.Join(", ", s.Tools)
            }).OrderBy(s => s.Name).ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SkillsPage error: {ex.Message}");
            _allSkills = new List<SkillDisplayItem>();
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(query)
            ? _allSkills
            : _allSkills.Where(s =>
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Tools.Contains(query, StringComparison.OrdinalIgnoreCase)
            ).ToList();

        SkillsList.ItemsSource = filtered;
        SkillCountBadge.Text = $"{filtered.Count} skill{(filtered.Count == 1 ? "" : "s")}";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void SkillsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkillsList.SelectedItem is SkillDisplayItem item)
        {
            PreviewTitle.Text = item.Name;
            PreviewDescription.Text = $"{item.Description}\nTools: {item.Tools}";
            PreviewContent.Text = item.SystemPrompt;
        }
    }

    private static string DeriveCategory(Skill skill)
    {
        // Derive category from the skill's file path directory or tool set
        if (!string.IsNullOrEmpty(skill.FilePath))
        {
            var dir = Path.GetDirectoryName(skill.FilePath);
            if (dir is not null)
            {
                var folder = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(folder) && !folder.Equals("skills", StringComparison.OrdinalIgnoreCase))
                    return folder;
            }
        }

        // Fall back to inferring from tools
        var tools = string.Join(" ", skill.Tools).ToLower();
        if (tools.Contains("bash") || tools.Contains("terminal")) return "automation";
        if (tools.Contains("read_file") && tools.Contains("write_file")) return "code";
        if (tools.Contains("grep") || tools.Contains("glob")) return "analysis";
        return "general";
    }
}

public sealed class SkillDisplayItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string Tools { get; set; } = "";
}
