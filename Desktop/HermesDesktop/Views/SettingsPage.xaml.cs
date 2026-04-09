using System;
using System.Linq;
using Hermes.Agent.LLM;
using HermesDesktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly ResourceLoader ResourceLoader = new();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    public string HermesHomePath => HermesEnvironment.DisplayHermesHomePath;

    public string HermesConfigPath => HermesEnvironment.DisplayHermesConfigPath;

    public string HermesLogsPath => HermesEnvironment.DisplayHermesLogsPath;

    public string HermesWorkspacePath => HermesEnvironment.DisplayHermesWorkspacePath;

    public string TelegramStatus => HermesEnvironment.TelegramConfigured
        ? ResourceLoader.GetString("StatusDetected")
        : ResourceLoader.GetString("StatusNotDetected");

    public string DiscordStatus => HermesEnvironment.DiscordConfigured
        ? ResourceLoader.GetString("StatusDetected")
        : ResourceLoader.GetString("StatusNotDetected");

    private bool _suppressModelComboEvent;

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Pre-populate fields from current config
        var provider = HermesEnvironment.ModelProvider.ToLowerInvariant();
        var normalizedProvider = provider == "custom" ? "local" : provider;
        SelectComboByTag(ProviderCombo, normalizedProvider, fallbackIndex: 6);

        BaseUrlBox.Text = HermesEnvironment.ModelBaseUrl;
        ModelBox.Text = HermesEnvironment.DefaultModel;
        ApiKeyBox.Password = HermesEnvironment.ModelApiKey ?? "";
        AuthHeaderBox.Text = HermesEnvironment.ModelAuthHeader;
        AuthSchemeBox.Text = HermesEnvironment.ModelAuthScheme;
        AuthTokenEnvBox.Text = HermesEnvironment.ModelAuthTokenEnv ?? "";
        AuthTokenCommandBox.Text = HermesEnvironment.ModelAuthTokenCommand ?? "";
        SelectComboByTag(AuthModeCombo, HermesEnvironment.ModelAuthMode, fallbackIndex: 0);
        UpdateAuthFieldState(HermesEnvironment.ModelAuthMode);

        PopulateModelCombo(normalizedProvider);
        SelectCurrentModel(HermesEnvironment.DefaultModel);
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var providerTag = (ProviderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "local";
        PopulateModelCombo(providerTag);

        // Auto-fill base URL for known providers
        if (ModelCatalog.ProviderBaseUrls.TryGetValue(providerTag, out var defaultUrl))
        {
            BaseUrlBox.Text = defaultUrl;
        }
    }

    private void PopulateModelCombo(string provider)
    {
        _suppressModelComboEvent = true;
        ModelCombo.Items.Clear();

        var models = ModelCatalog.GetModels(provider);
        foreach (var m in models)
        {
            ModelCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{m.DisplayName}  ({ModelCatalog.FormatContextLength(m.ContextLength)})",
                Tag = m.Id
            });
        }

        if (ModelCombo.Items.Count > 0)
            ModelCombo.SelectedIndex = 0;

        _suppressModelComboEvent = false;

        // Update context label for first item
        if (models.Count > 0)
            ContextLengthLabel.Text = $"Context: {ModelCatalog.FormatContextLength(models[0].ContextLength)}";
        else
            ContextLengthLabel.Text = "Context: --";
    }

    private void SelectCurrentModel(string modelId)
    {
        for (int i = 0; i < ModelCombo.Items.Count; i++)
        {
            if (ModelCombo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), modelId, StringComparison.OrdinalIgnoreCase))
            {
                _suppressModelComboEvent = true;
                ModelCombo.SelectedIndex = i;
                _suppressModelComboEvent = false;

                var ctx = ModelCatalog.GetContextLength(modelId);
                ContextLengthLabel.Text = $"Context: {ModelCatalog.FormatContextLength(ctx)}";
                return;
            }
        }
    }

    private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressModelComboEvent) return;
        if (ModelCombo.SelectedItem is ComboBoxItem selected)
        {
            var modelId = selected.Tag?.ToString() ?? "";
            ModelBox.Text = modelId;
            var ctx = ModelCatalog.GetContextLength(modelId);
            ContextLengthLabel.Text = $"Context: {ModelCatalog.FormatContextLength(ctx)}";
        }
    }

    private void AuthModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var authMode = (AuthModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "api_key";
        UpdateAuthFieldState(authMode);
    }

    private async void SaveModelConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var providerTag = (ProviderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";
            var baseUrl = BaseUrlBox.Text.Trim();
            var model = ModelBox.Text.Trim();
            var apiKey = ApiKeyBox.Password.Trim();
            var authMode = (AuthModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "api_key";
            var authHeader = AuthHeaderBox.Text.Trim();
            var authScheme = AuthSchemeBox.Text.Trim();
            var authTokenEnv = AuthTokenEnvBox.Text.Trim();
            var authTokenCommand = AuthTokenCommandBox.Text.Trim();

            if (string.IsNullOrEmpty(model))
            {
                ModelSaveStatus.Text = "Model name is required.";
                ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
                return;
            }

            if (authMode == "oauth_proxy_env" && string.IsNullOrWhiteSpace(authTokenEnv))
            {
                ModelSaveStatus.Text = "Token env var is required for OAuth Proxy (Env Token).";
                ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
                return;
            }

            if (authMode == "oauth_proxy_command" && string.IsNullOrWhiteSpace(authTokenCommand))
            {
                ModelSaveStatus.Text = "Token command is required for OAuth Proxy (Command Token).";
                ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
                return;
            }

            await HermesEnvironment.SaveModelConfigAsync(
                providerTag,
                baseUrl,
                model,
                apiKey,
                authMode,
                authHeader,
                authScheme,
                authTokenEnv,
                authTokenCommand);
            ModelSaveStatus.Text = "Saved successfully. Restart to apply.";
            ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOnlineBrush"];
        }
        catch (Exception ex)
        {
            ModelSaveStatus.Text = $"Error: {ex.Message}";
            ModelSaveStatus.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ConnectionOfflineBrush"];
        }
    }

    private void OpenHome_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenHermesHome();
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenConfig();
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenLogs();
    }

    private void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenWorkspace();
    }

    private static void SelectComboByTag(ComboBox combo, string? tag, int fallbackIndex)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        combo.SelectedIndex = fallbackIndex;
    }

    private void UpdateAuthFieldState(string? authMode)
    {
        var mode = (authMode ?? "api_key").ToLowerInvariant();
        var usesProxyToken = mode is "oauth_proxy_env" or "oauth_proxy_command";

        ApiKeyBox.IsEnabled = mode == "api_key";
        AuthHeaderBox.IsEnabled = usesProxyToken;
        AuthSchemeBox.IsEnabled = usesProxyToken;
        AuthTokenEnvBox.IsEnabled = mode == "oauth_proxy_env";
        AuthTokenCommandBox.IsEnabled = mode == "oauth_proxy_command";
    }
}
