using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Hermes.Agent.Skills;

namespace HermesDesktop.Views;

public sealed partial class SkillsPage : Page
{
    private List<SkillDisplayItem> _allSkills = new();
    private string _selectedCategory = "All";
    private bool _initialized;

    public SkillsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => { _initialized = true; Refresh(); };
    }

    private void Refresh()
    {
        try
        {
            var skillManager = App.Services.GetRequiredService<SkillManager>();
            var skills = skillManager.ListSkills();

            _allSkills = skills.Select(s =>
            {
                var item = new SkillDisplayItem
                {
                    Name = s.Name,
                    Description = s.Description,
                    Category = DeriveCategory(s),
                    SystemPrompt = s.SystemPrompt,
                    Tools = string.Join(", ", s.Tools),
                    ToolCount = s.Tools.Count switch
                    {
                        0 => "",
                        1 => "1 tool",
                        _ => $"{s.Tools.Count} tools"
                    }
                };
                item.ApplyCategoryColor();
                return item;
            }).OrderBy(s => s.Name).ToList();

            BuildCategoryChips();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SkillsPage error: {ex.Message}");
            _allSkills = new List<SkillDisplayItem>();
            ApplyFilter();
        }
    }

    private void BuildCategoryChips()
    {
        CategoryChips.Children.Clear();

        var categories = _allSkills
            .Select(s => s.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => (Name: g.Key, Count: g.Count()))
            .ToList();

        // "All" chip
        AddChip("All", _allSkills.Count, "All");

        foreach (var (name, count) in categories)
            AddChip(name, count, name);
    }

    private void AddChip(string label, int count, string tag)
    {
        var isSelected = tag == _selectedCategory;
        var btn = new Button
        {
            Content = count > 0 ? $"{label} ({count})" : label,
            Tag = tag,
            FontSize = 11,
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(12),
            Background = isSelected
                ? (Brush)Application.Current.Resources["AppAccentBrush"]
                : (Brush)Application.Current.Resources["AppInsetBrush"],
            Foreground = isSelected
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 17, 13))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0
        };
        btn.Click += CategoryChip_Click;
        CategoryChips.Children.Add(btn);
    }

    private void CategoryChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cat)
        {
            _selectedCategory = cat;
            BuildCategoryChips(); // rebuild to update selection highlight
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        var filtered = _allSkills.AsEnumerable();

        // Category filter
        if (_selectedCategory != "All")
            filtered = filtered.Where(s => s.Category.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase));

        // Text search
        if (!string.IsNullOrEmpty(query))
            filtered = filtered.Where(s =>
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Tools.Contains(query, StringComparison.OrdinalIgnoreCase));

        // Sort
        var sortTag = (SortSelector.SelectedItem as ComboBoxItem)?.Tag as string ?? "NameAsc";
        var sorted = sortTag switch
        {
            "NameDesc" => filtered.OrderByDescending(s => s.Name),
            "Category" => filtered.OrderBy(s => s.Category).ThenBy(s => s.Name),
            _ => filtered.OrderBy(s => s.Name)
        };

        var list = sorted.ToList();
        SkillsList.ItemsSource = list;
        SkillCountBadge.Text = $"{list.Count} skill{(list.Count == 1 ? "" : "s")}";
        if (ListHeader is not null)
            ListHeader.Text = _selectedCategory == "All" ? "All Skills" : _selectedCategory;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initialized) ApplyFilter();
    }

    private void SortSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized) ApplyFilter();
    }

    private void SkillsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkillsList.SelectedItem is SkillDisplayItem item)
        {
            PreviewTitle.Text = item.Name;
            PreviewDescription.Text = item.Description;
            PreviewContent.Text = item.SystemPrompt;

            PreviewCategoryChip.Text = item.Category;
            PreviewToolsChip.Text = string.IsNullOrEmpty(item.Tools) ? "No tools" : $"Tools: {item.Tools}";
            PreviewChips.Visibility = Visibility.Visible;
        }
    }

    private static string DeriveCategory(Skill skill)
    {
        if (!string.IsNullOrEmpty(skill.FilePath))
        {
            // Structure: skills/<category>/<skill-name>/SKILL.md
            // Go up two levels to get the category folder
            var skillDir = Path.GetDirectoryName(skill.FilePath); // <skill-name> folder
            if (skillDir is not null)
            {
                var categoryDir = Path.GetDirectoryName(skillDir); // <category> folder
                if (categoryDir is not null)
                {
                    var category = Path.GetFileName(categoryDir);
                    if (!string.IsNullOrEmpty(category) && !category.Equals("skills", StringComparison.OrdinalIgnoreCase))
                        return category;
                }
                // Fallback: if only one level deep, use the immediate folder
                var folder = Path.GetFileName(skillDir);
                if (!string.IsNullOrEmpty(folder) && !folder.Equals("skills", StringComparison.OrdinalIgnoreCase))
                    return folder;
            }
        }

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
    public string ToolCount { get; set; } = "";
    public SolidColorBrush BadgeBackground { get; set; } = new(Windows.UI.Color.FromArgb(255, 58, 58, 0));
    public SolidColorBrush BadgeForeground { get; set; } = new(Windows.UI.Color.FromArgb(255, 230, 190, 60));

    private static readonly Dictionary<string, (byte R, byte G, byte B, byte FR, byte FG, byte FB)> CategoryColors = new()
    {
        ["claude-code"]           = (0x1A, 0x3A, 0x5C, 0x60, 0xB0, 0xE0),
        ["apple"]                 = (0x1A, 0x3C, 0x1A, 0x60, 0xE0, 0x60),
        ["creative"]              = (0x3A, 0x1A, 0x3C, 0xC0, 0x80, 0xE0),
        ["autonomous-ai-agents"]  = (0x3C, 0x1A, 0x1A, 0xE0, 0x70, 0x70),
        ["note-taking"]           = (0x3A, 0x3A, 0x00, 0xE6, 0xBE, 0x3C),
        ["productivity"]          = (0x0A, 0x3A, 0x2A, 0x50, 0xE0, 0xA0),
        ["smart-home"]            = (0x1A, 0x2A, 0x3C, 0x60, 0xA0, 0xE0),
        ["data-science"]          = (0x3C, 0x2A, 0x0A, 0xE0, 0xA0, 0x50),
        ["mlops"]                 = (0x2A, 0x1A, 0x3C, 0xA0, 0x70, 0xE0),
        ["devops"]                = (0x1A, 0x30, 0x3C, 0x50, 0xC0, 0xE0),
        ["research"]              = (0x30, 0x30, 0x1A, 0xC0, 0xC0, 0x60),
        ["software-development"]  = (0x1A, 0x3C, 0x28, 0x60, 0xE0, 0x90),
        ["github"]                = (0x2A, 0x2A, 0x2A, 0xC0, 0xC0, 0xC0),
        ["media"]                 = (0x3C, 0x1A, 0x2A, 0xE0, 0x60, 0xA0),
        ["gaming"]                = (0x2A, 0x1A, 0x30, 0xB0, 0x70, 0xD0),
        ["email"]                 = (0x1A, 0x2A, 0x30, 0x60, 0xA0, 0xC0),
        ["mcp"]                   = (0x20, 0x35, 0x20, 0x80, 0xD0, 0x80),
        ["feeds"]                 = (0x35, 0x25, 0x10, 0xD0, 0x90, 0x50),
        ["diagramming"]           = (0x25, 0x20, 0x35, 0x90, 0x80, 0xD0),
        ["inference-sh"]          = (0x30, 0x20, 0x20, 0xD0, 0x80, 0x80),
        ["social-media"]          = (0x20, 0x20, 0x38, 0x80, 0x80, 0xE0),
        ["red-teaming"]           = (0x3C, 0x10, 0x10, 0xE0, 0x50, 0x50),
        ["domain"]                = (0x28, 0x28, 0x1A, 0xB0, 0xB0, 0x60),
        ["leisure"]               = (0x1A, 0x30, 0x30, 0x60, 0xC0, 0xC0),
    };

    public void ApplyCategoryColor()
    {
        if (CategoryColors.TryGetValue(Category.ToLower(), out var c))
        {
            BadgeBackground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, c.R, c.G, c.B));
            BadgeForeground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, c.FR, c.FG, c.FB));
        }
    }
}
