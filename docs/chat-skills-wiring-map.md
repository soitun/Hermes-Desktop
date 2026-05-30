# Chat and skills wiring map

Generated from the local CodeGraph pass on 2026-05-30.

## Chat page

| UI surface | Handler | Runtime path |
| --- | --- | --- |
| New Chat | `ChatPage.NewChat_Click` | `HermesChatService.ResetConversation`, replay clear, welcome render, session refresh |
| Message box Enter | `ChatPage.PromptTextBox_KeyDown` | `SendPromptAsync` unless Shift+Enter or slash palette consumes the key |
| Send button | `ChatPage.SendPrompt_Click` | `SendPromptAsync` -> `HermesChatService.StreamRuntimeAsync` -> `Agent.StreamChatAsync` |
| Stop | `ChatPage.StopGeneration_Click` | `HermesChatService.CancelStream` |
| Error retry | `ChatPage.RetryLastPrompt_Click` | restores prompt, then `SendPromptAsync` |
| Error switch model | `ChatPage.OpenModelSwitcher_Click` | focuses and opens `ModelSwitchCombo` |
| Model switcher | `ChatPage.ModelSwitchCombo_SelectionChanged` | `ChatClientFactory.SwitchProvider`, runtime status refresh |
| Permission mode | `ChatPage.PermissionModeToggle_Click` | menu items call `SetPermissionModeUi`; clear item calls `ClearRememberedPermissionsAsync` |
| Panel tabs | `ChatPage.PanelTab_Click` | `ShowPanel` toggles Sessions, Files, Tasks, Replay panels |
| Copy session ID | `ChatPage.CopySessionId_Click` | copies `HermesChatService.CurrentSessionId` to clipboard |
| Sessions panel clear | `SessionPanel.ClearChats_Click` | `TranscriptStore.DeleteAllSessionsAsync`, then `ChatPage.NewChat_Click` |
| File tree item | `FileBrowserPanel.FileTree_ItemInvoked` | preview selected file text |
| Task refresh | `TaskPanel.Refresh_Click` | reloads `TaskManager.GetOrderedTasks` |
| Replay record | `ReplayPanel.RecordToggle_Click` | raises `RecordingToggled` to `ChatPage` recorder |
| Replay play | `ReplayPanel.PlayBtn_Click` | chronological playback of captured activity |
| Replay clear | `ReplayPanel.ClearBtn_Click` | clears replay activity display |

## Skills page

| UI surface | Handler | Runtime path |
| --- | --- | --- |
| Sort selector | `SkillsPage.SortSelector_SelectionChanged` | `ApplyFilter` |
| Search box | `SkillsPage.SearchBox_TextChanged` | `ApplyFilter` |
| Category chips | `SkillsPage.CategoryChip_Click` | selects category, rebuilds chips, applies filter |
| Skill list selection | `SkillsPage.SkillsList_SelectionChanged` | preview content, metadata chips, delete enablement |
| Enable switch | `SkillsPage.SkillToggle_Toggled` | `SkillManager.SetEnabled` and persisted `.skill-toggles.json` |
| Install | `SkillsPage.InstallSkill_Click` | `ResolveSkillDownloadUrlAsync` -> `SkillsHub.InstallAsync` -> `SkillManager.CreateSkillAsync` |
| Browse | `SkillsPage.BrowseHub_Click` | `SkillsHub.SearchGitHubAsync`; clicking a result fills install fields |
| Delete | `SkillsPage.DeleteSkill_Click` | confirmation dialog -> `SkillManager.DeleteSkillAsync` |

## Verification targets

- Chat must show a user bubble, stream a real assistant response, update the session footer, and refresh the Sessions panel.
- Skills must show installed skills, support search/sort/category filters, preview selection, toggle enable state, browse repo skill files including one category level, install by raw URL or repo source, and confirm before deletion.
