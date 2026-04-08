using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Hermes.Agent.Buddy;

namespace HermesDesktop.Views;

public sealed partial class BuddyPage : Page
{
    private BuddyService? _buddyService;

    public BuddyPage()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = LoadBuddyAsync();
    }

    private async System.Threading.Tasks.Task LoadBuddyAsync()
    {
        try
        {
            _buddyService = App.Services.GetRequiredService<BuddyService>();

            // Use a default user ID — the service handles deterministic generation
            var userId = Environment.UserName ?? "default";
            var buddy = await _buddyService.GetBuddyAsync(userId, CancellationToken.None);

            // Render ASCII art
            var ascii = BuddyRenderer.RenderAscii(buddy);
            AsciiArt.Text = ascii;

            // Name
            BuddyName.Text = buddy.Name ?? "Unnamed";

            // Species + Rarity
            BuddySpecies.Text = buddy.Species;
            RarityText.Text = buddy.Rarity.ToUpperInvariant();

            // Rarity badge color
            RarityBadge.Background = buddy.Rarity switch
            {
                "legendary" => new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 200, 160, 50)),
                "rare" => new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 100, 140, 200)),
                "uncommon" => new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 100, 180, 100)),
                _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 58, 58, 0))
            };

            // Shiny badge
            if (buddy.IsShiny)
            {
                ShinyBadge.Text = "SHINY";
                ShinyBadge.Visibility = Visibility.Visible;
            }

            // Stats
            StatInt.Value = buddy.Stats.Intelligence;
            StatIntVal.Text = buddy.Stats.Intelligence.ToString();
            StatEnr.Value = buddy.Stats.Energy;
            StatEnrVal.Text = buddy.Stats.Energy.ToString();
            StatCre.Value = buddy.Stats.Creativity;
            StatCreVal.Text = buddy.Stats.Creativity.ToString();
            StatFrn.Value = buddy.Stats.Friendliness;
            StatFrnVal.Text = buddy.Stats.Friendliness.ToString();

            // Personality
            BuddyPersonality.Text = buddy.Personality ?? "";

            // Details
            BuddyDetails.Text = $"Eyes: {buddy.Eyes}\n"
                               + $"Hat: {(string.IsNullOrEmpty(buddy.Hat) ? "none" : buddy.Hat)}\n"
                               + $"Total Stats: {buddy.Stats.Total}\n"
                               + $"Hatched: {buddy.HatchedAt:yyyy-MM-dd}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BuddyPage error: {ex.Message}");
            AsciiArt.Text = "Could not load buddy.";
            BuddyName.Text = "Error";
            BuddyDetails.Text = $"Error: {ex.Message}";
        }
    }
}
