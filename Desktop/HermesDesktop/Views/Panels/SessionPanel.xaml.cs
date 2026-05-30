using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HermesDesktop.Views.Panels;

public sealed class SessionListItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string TimeAgo { get; set; } = "";
    public string MessageCount { get; set; } = "";
}

public sealed partial class SessionPanel : UserControl
{
    private readonly TranscriptStore _transcriptStore;
    private readonly ObservableCollection<SessionListItem> _allSessions = new();

    public ObservableCollection<SessionListItem> Sessions { get; } = new();
    public event Action<string>? SessionSelected;
    public event Action? SessionsCleared;

    public SessionPanel()
    {
        InitializeComponent();
        _transcriptStore = App.Services.GetRequiredService<TranscriptStore>();
        SessionList.ItemsSource = Sessions;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async System.Threading.Tasks.Task RefreshAsync()
    {
        _allSessions.Clear();
        var ids = _transcriptStore.GetAllSessionIds();

        foreach (var id in ids.OrderByDescending(i => i))
        {
            try
            {
                var messages = await _transcriptStore.LoadSessionAsync(id, CancellationToken.None);
                var firstUser = messages.FirstOrDefault(m => m.Role == "user");
                var title = firstUser?.Content?.Length > 60
                    ? firstUser.Content[..60] + "..."
                    : firstUser?.Content ?? "(empty)";

                var last = messages.LastOrDefault();
                var ago = last is not null ? FormatTimeAgo(last.Timestamp) : "";

                _allSessions.Add(new SessionListItem
                {
                    Id = id,
                    Title = title,
                    TimeAgo = ago,
                    MessageCount = $"{messages.Count} msgs"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SessionPanel skipping unreadable session {id}: {ex}");
            }
        }

        ApplySessionFilter();
    }

    public event Action<string>? SessionDeleted;

    private void NewSession_Click(object sender, RoutedEventArgs e) => SessionSelected?.Invoke("");

    private async void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string sessionId) return;

        await _transcriptStore.DeleteSessionAsync(sessionId, CancellationToken.None);
        var sourceItem = _allSessions.FirstOrDefault(s => s.Id == sessionId);
        if (sourceItem is not null) _allSessions.Remove(sourceItem);

        var item = Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (item is not null) Sessions.Remove(item);

        UpdateEmptyState();

        SessionDeleted?.Invoke(sessionId);
    }

    private async void ClearChats_Click(object sender, RoutedEventArgs e)
    {
        if (_allSessions.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = "Clear all chats?",
            Content = "This deletes every saved chat transcript and replay activity from this device.",
            PrimaryButtonText = "Clear chats",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        await _transcriptStore.DeleteAllSessionsAsync(CancellationToken.None);
        _allSessions.Clear();
        Sessions.Clear();
        UpdateEmptyState();
        SessionsCleared?.Invoke();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySessionFilter();

    private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionList.SelectedItem is SessionListItem item)
            SessionSelected?.Invoke(item.Id);
    }

    private void ApplySessionFilter()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        Sessions.Clear();

        foreach (var session in _allSessions.Where(session => SessionMatchesFilter(session, query)))
            Sessions.Add(session);

        UpdateEmptyState();
    }

    internal static bool SessionMatchesFilter(SessionListItem session, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;

        return session.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               session.TimeAgo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               session.MessageCount.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateEmptyState()
    {
        ClearChatsBtn.IsEnabled = _allSessions.Count > 0;
        EmptyState.Text = _allSessions.Count == 0
            ? "No conversations yet.\nStart chatting to create your first session."
            : "No sessions match your search.";
        EmptyState.Visibility = Sessions.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string FormatTimeAgo(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return timestamp.ToLocalTime().ToString("MMM d");
    }
}
