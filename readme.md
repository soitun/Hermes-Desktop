# Hermes Desktop

<p align="center">
  <img src="docs/logo.png" alt="Hermes Desktop Logo" width="128" />
</p>

![Hermes Agent Banner](docs/screenshots/banner.png)

A native Windows desktop AI agent built with WinUI 3 and .NET 10 — featuring a soul identity system, 94 skills, multi-agent profiles, and 6 messaging platform integrations.

## Screenshots

| Dashboard | Chat |
|-----------|------|
| ![Dashboard](docs/screenshots/Screenshot%202026-04-05%20123632.png) | ![Chat](docs/screenshots/Screenshot%202026-04-05%20123637.png) |

| Agent | Skills |
|-------|--------|
| ![Agent](docs/screenshots/Screenshot%202026-04-05%20123642.png) | ![Skills](docs/screenshots/Screenshot%202026-04-05%20123700.png) |

| Memory | Integrations |
|--------|-------------|
| ![Memory](docs/screenshots/Screenshot%202026-04-05%20123706.png) | ![Integrations](docs/screenshots/Screenshot%202026-04-05%20123711.png) |

| Settings |
|----------|
| ![Settings](docs/screenshots/Screenshot%202026-04-05%20123716.png) |

## Features

### Soul Identity System

Hermes Desktop uses a persistent identity stack inspired by Claude Code's CLAUDE.md and the OpenClaw agent architecture:

- **SOUL.md** — Defines who the agent is: personality, values, working style, philosophical grounding
- **USER.md** — Stores information about you: expertise, preferences, how you work
- **AGENTS.md** — Project-specific rules, coding conventions, architecture patterns
- **Mistakes Journal** — Learned corrections from past errors (auto-extracted from transcripts)
- **Habits Journal** — Reinforced good patterns (auto-extracted via dream consolidation)
- **12 Built-in Soul Templates** — Default, Creative, Teacher, Researcher, Minimalist, Pair Programmer, DevOps Engineer, Security Analyst, Startup CTO, Mentor, Claude Soul, Nous Hermes

The soul context is injected as Layer 0 in a 6-layer prompt architecture for optimal cache efficiency.

### Multi-Agent Profiles

Create and switch between different agent configurations:

- Save named profiles pairing a soul document with a description
- One-click activation swaps the active soul identity
- Use different agents for different tasks (code review, creative writing, teaching, etc.)

### 94 Skills (74 Hermes + 20 Claude Code)

Markdown-based custom capabilities with YAML frontmatter, organized by category:

| Category | Skills |
|----------|--------|
| **Claude Code** | simplify, commit, commit-push-pr, code-review, plan, TDD, systematic-debugging, pdf, docx, xlsx, pptx, frontend-design, mcp-builder, skill-creator, algorithmic-art, canvas-design, webapp-testing, security-audit, refactor, documentation |
| **Software Development** | code-review, plan, requesting-code-review, subagent-driven-development, systematic-debugging, test-driven-development, writing-plans |
| **GitHub** | codebase-inspection, github-auth, github-code-review, github-issues, github-pr-workflow, github-repo-management |
| **Research** | arxiv, blogwatcher, polymarket, research-paper-writing |
| **MLOps** | cloud, evaluation, huggingface-hub, inference, models, research, training, vector-databases |
| **Productivity** | google-workspace, linear, nano-pdf, notion, ocr-and-documents, powerpoint |
| **Creative** | ascii-art, ascii-video, excalidraw, songwriting-and-ai-music |
| **Media** | gif-search, heartmula, songsee, youtube-content |
| **And more...** | Apple, autonomous AI agents, data science, devops, email, gaming, MCP, note-taking, red-teaming, smart home, social media |

### 13 Built-in Tools

| Tool | Description |
|------|-------------|
| `bash` | Execute shell commands with timeout and background support |
| `read_file` | Read file contents with offset/limit pagination |
| `write_file` | Create or overwrite files with read-before-write enforcement |
| `edit_file` | Precise string replacement with uniqueness validation |
| `glob` | Fast pattern-based file search sorted by modification time |
| `grep` | Content search powered by ripgrep with regex support |
| `web_search` | Search the web and return structured results |
| `web_fetch` | Fetch and extract content from URLs |
| `agent` | Spawn sub-agents for parallel task execution |
| `todo_write` | Task list management with status tracking |
| `ask_user` | Prompt the user for input or confirmation |
| `schedule_cron` | Schedule recurring tasks with cron expressions |
| `terminal` | Interactive terminal session management |

### 8 LLM Providers

- **Nous** (Hermes models)
- **OpenAI** (GPT-4o, o1, o3)
- **Anthropic** (Claude Sonnet, Opus)
- **Qwen** (Qwen 3.5, QwQ)
- **DeepSeek** (V3, R1)
- **MiniMax** (MiniMax-M2.7)
- **OpenRouter** (any model via router)
- **Local** (Ollama, LM Studio, any OpenAI-compatible endpoint)

### Desktop Application (8 Pages)

| Page | Description |
|------|-------------|
| **Dashboard** | KPI stat cards, platform/service status badges, model config, recent activity feed |
| **Chat** | Full agent chat with tool loop, thinking indicators, permission dialogs, right-side panels (Sessions, Files, Tasks, Replay) |
| **Agent** | Three tabs — **Agents** (create/manage profiles), **Identity** (edit SOUL.md, USER.md, view journals), **Souls** (browse 12 templates, preview, apply) |
| **Skills** | Searchable skill library with category filter chips, colored badges, sort options, card-based layout, full system prompt preview |
| **Memory** | Memory file browser with type badges and content preview, plus project rules (AGENTS.md) editor |
| **Buddy** | Companion display with ASCII art, stats, personality |
| **Integrations** | Configure 6 messaging platforms with the messaging gateway |
| **Settings** | Model provider configuration |

### UI Design System

- Dark theme with Hermes gold/caramel gradient accent (`#C68E17` → `#D4A017` → `#FFD700`)
- Mica backdrop with custom NavigationView theming
- 4 global button styles: Default (gold gradient), Ghost, Secondary, Danger
- Surface cards, inset cards, and consistent typography scale
- Resizable split panels in Chat

### 6 Messaging Integrations

- Telegram
- Discord
- Slack
- WhatsApp
- Matrix
- Webhook (generic HTTP)

### Agent Capabilities

- **Full Tool Loop** — Agent.ChatAsync with 13 tools, permissions, soul injection, memory, and activity tracking
- **6-Layer Prompt Architecture** — Soul (L0) → System Prompt (L1) → Session State (L2) → Context (L3) → Recent Turns (L4) → User Message (L5)
- **Claude Code-Quality System Prompt** — Comprehensive guidelines covering tool usage, coding best practices, git workflow, communication style, safety constraints
- **MCP Server Integration** — Connect external tool servers via Model Context Protocol
- **Permission System** — Rule DSL with 5 modes (Default, Plan, Auto, Bypass, AcceptEdits) and a WinUI dialog for user approval
- **Context Runtime** — Persistent memory, token budget management, automatic summarization
- **Dream Consolidation** — AutoDreamService extracts mistakes, habits, and user profile signals from transcripts
- **Security** — SSRF protection, secret scanning, shell command analysis
- **Credential Pool** — Provider key rotation across multiple API keys
- **Session Persistence** — JSONL transcript-first storage with crash recovery

## Quick Start

### Prerequisites

- Windows 10 (1809+) or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Windows App SDK 1.7](https://learn.microsoft.com/windows/apps/windows-app-sdk/)

### Build and Run

```bash
# Clone the repo
git clone https://github.com/RedWoodOG/Hermes-Desktop.git
cd Hermes-Desktop

# Build the solution
dotnet build

# Build and register the desktop app
cd Desktop/HermesDesktop
dotnet build
```

Then register and launch the MSIX package:

```powershell
cd bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64
Add-AppxPackage -Register AppxManifest.xml
# Launch from Start menu or shell:AppsFolder
```

## Configuration

Create `%LOCALAPPDATA%\hermes\config.yaml`:

```yaml
llm:
  provider: openai
  model: minimax-m2.7:cloud
  baseUrl: http://localhost:11434/v1
  apiKey: ""

# Or use a cloud provider
# llm:
#   provider: anthropic
#   model: claude-sonnet-4-20250514
#   apiKey: sk-your-key-here

messaging:
  telegram:
    botToken: ""
  discord:
    botToken: ""

security:
  ssrf:
    enabled: true
  secretScanning:
    enabled: true
```

## Project Structure

```
Hermes.CS/
├── src/                          # Core agent library (Hermes.Core)
│   ├── Core/                     # Agent, SystemPrompts, models, interfaces
│   ├── Tools/                    # 13 tool implementations
│   ├── LLM/                     # LLM client abstraction (8 providers)
│   ├── soul/                    # Soul system (SoulService, SoulRegistry,
│   │                            #   SoulExtractor, AgentProfileManager)
│   ├── agents/                  # Agent teams, coordination
│   ├── coordinator/             # Multi-agent orchestration
│   ├── memory/                  # Persistent memory system
│   ├── permissions/             # Permission rules and modes
│   ├── skills/                  # Skills system (SkillManager)
│   ├── tasks/                   # Task management
│   ├── transcript/              # Session persistence (JSONL)
│   ├── buddy/                   # Companion buddy system
│   ├── dream/                   # Auto-consolidation + soul extraction
│   ├── context/                 # Context runtime (PromptBuilder, budget)
│   ├── security/                # SSRF, secret scanning, shell analysis
│   └── Hermes.Core.csproj
├── Desktop/
│   └── HermesDesktop/           # WinUI 3 desktop application (canonical)
│       ├── Models/              # View models (ChatMessageItem)
│       ├── Services/            # HermesChatService, HermesEnvironment
│       ├── Views/               # 8 full pages + panel controls
│       │   ├── DashboardPage    # KPI cards, status badges, activity feed
│       │   ├── ChatPage         # Agent chat + right panels
│       │   ├── AgentPage        # Identity, souls browser, agent profiles
│       │   ├── SkillsPage       # Searchable skill library with categories
│       │   ├── MemoryPage       # Memory browser + project rules
│       │   ├── BuddyPage        # Companion display
│       │   ├── IntegrationsPage # 6 messaging platform configs
│       │   ├── SettingsPage     # Model provider settings
│       │   └── Panels/          # Sessions, Files, Tasks, Replay
│       ├── Controls/            # CodeBlockView, PermissionDialog, ApprovalCard
│       └── HermesDesktop.csproj
├── skills/                      # 94 skill definitions (Markdown + YAML)
│   ├── claude-code/             # 20 Claude Code skills
│   ├── software-development/    # 8 dev workflow skills
│   ├── github/                  # 6 GitHub skills
│   ├── mlops/                   # 8 ML operations skills
│   ├── souls/                   # 12 soul templates
│   └── ...                      # 29 total categories
├── docs/                        # Architecture documentation
└── HermesDesktop.slnx
```

## Tech Stack

- **.NET 10** — latest .NET runtime
- **WinUI 3** — Windows App SDK 1.7, Mica backdrop
- **C# 13** — primary constructors, collection expressions, pattern matching
- **System.Text.Json** — high-performance JSON serialization
- **YamlDotNet** — configuration parsing

## Based On

This project is based on the [NousResearch Hermes](https://github.com/NousResearch) agent architecture. Hermes Desktop is a native Windows implementation of the Hermes agent design, bringing agentic AI capabilities to the desktop with a modern WinUI 3 interface. https://github.com/NousResearch/hermes-agent — this is my way of showing appreciation to the team building it, just a fork from their vision. Please support them and give them love and use the product they worked so hard to create.

## License

MIT
