using System.Collections.Generic;
using HermesDesktop.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Graphics;

namespace HermesDesktop;

public sealed partial class MainWindow : Window
{
    private static readonly IReadOnlyDictionary<string, System.Type> PageMap = new Dictionary<string, System.Type>
    {
        ["dashboard"] = typeof(DashboardPage),
        ["chat"] = typeof(ChatPage),
        ["agent"] = typeof(AgentPage),
        ["skills"] = typeof(SkillsPage),
        ["memory"] = typeof(MemoryPage),
        ["buddy"] = typeof(BuddyPage),
        ["integrations"] = typeof(IntegrationsPage),
        ["settings"] = typeof(SettingsPage),
    };

    private static readonly ResourceLoader ResourceLoader = new();

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        Title = ResourceLoader.GetString("WindowTitle");
        AppTitleBar.Title = Title;
        AppWindow.Resize(new SizeInt32(1480, 960));
        AppWindow.SetIcon("Assets/AppIcon.ico");

        ShellNavigation.SelectedItem = ChatNavItem;
        NavigateToTag("chat");
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            NavigateToTag(tag);
        }
    }

    private void NavigateToTag(string tag)
    {
        if (PageMap.TryGetValue(tag, out System.Type? pageType) && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
