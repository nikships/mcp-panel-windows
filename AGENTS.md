# Repository Guidelines

## Purpose

MCP Panel Windows is a native Windows WinForms app for visual management of local MCP JSON configs. It was inspired by the macOS `mcp-panel` project, but this repository intentionally contains only the focused Windows implementation.

The app defaults to Factory's config at `%USERPROFILE%\.factory\mcp.json`, but users can choose any local JSON file that stores servers under a top-level `mcpServers` object.

## Architecture

- `WindowsMcpPanel/Program.cs`
  - Application entry point.
  - Handles CLI modes: `--self-test`, `--validate-factory`, and `--validate <path>`.
  - Starts `MainForm` for normal GUI usage.

- `WindowsMcpPanel/Forms/MainForm.cs`
  - Main window composition and application workflow.
  - Owns selected config path, file watcher, grid rendering, add/import box, and status messages.
  - Writes changes immediately through `ConfigStore`.

- `WindowsMcpPanel/Forms/ServerCard.cs`
  - Per-server grid card.
  - Provides inline name editing, JSON editing, save/reset/delete actions, and on/off toggle.

- `WindowsMcpPanel/Models/ServerEntry.cs`
  - In-memory server model.
  - `Enabled` is derived from the MCP config's `disabled` field. Setting `Enabled` updates `disabled` directly.

- `WindowsMcpPanel/Services/ConfigStore.cs`
  - Reads and writes MCP JSON files.
  - Preserves unrelated top-level JSON keys and replaces only `mcpServers`.
  - Missing `mcpServers` is treated as an empty server set.

- `WindowsMcpPanel/Services/SettingsStore.cs`
  - Persists the last selected config path in `%APPDATA%\McpPanelWindows\settings.json`.

- `WindowsMcpPanel/Services/ServerExtractor.cs`
  - Forgiving add/import parser.
  - Accepts `mcpServers`, `servers`, raw named entries, and bare HTTP/HTTPS URLs.
  - Normalizes copied JSON quirks: curly quotes, trailing commas, command arrays, `environment` to `env`, and stringified headers.

- `WindowsMcpPanel/Services/ServerValidator.cs`
  - Core MCP validation and display summaries.
  - Keep this behavior aligned with the original macOS app's `ServerConfig.isValid`.

- `WindowsMcpPanel/Utilities/JsonTools.cs`
  - JSON parse/pretty-print helpers built on `JavaScriptSerializer`.
  - Preserves arbitrary unknown fields as dictionaries/arrays rather than typed DTOs.

- `WindowsMcpPanel/Utilities/Palette.cs`
  - OLED dark theme colors.

- `WindowsMcpPanel/Diagnostics/SelfTest.cs`
  - Lightweight CLI validation checks for parser/validator behavior and config readability.

## Build

Use the repository build script:

```powershell
.\build.ps1
```

The script uses the .NET Framework compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and compiles every `*.cs` file under `WindowsMcpPanel`.

Output:

```text
dist\McpPanelWindows.exe
```

## Validation

Run these before considering a change done:

```powershell
.\build.ps1
$p = Start-Process -FilePath .\dist\McpPanelWindows.exe -ArgumentList '--self-test' -Wait -PassThru; if ($p.ExitCode -ne 0) { exit $p.ExitCode }
$p = Start-Process -FilePath .\dist\McpPanelWindows.exe -ArgumentList '--validate-factory' -Wait -PassThru; if ($p.ExitCode -ne 0) { exit $p.ExitCode }
```

For arbitrary config validation:

```powershell
$p = Start-Process -FilePath .\dist\McpPanelWindows.exe -ArgumentList @('--validate', 'C:\path\to\mcp.json') -Wait -PassThru
if ($p.ExitCode -ne 0) { exit $p.ExitCode }
```

Important: the executable is built as a Windows GUI app (`/target:winexe`). In scripts, use `Start-Process -Wait` for CLI checks, especially with temporary files.

## MCP Config Rules

- The app edits only the selected JSON file.
- It preserves unrelated top-level keys.
- It rewrites `mcpServers` with the current in-memory server map.
- Servers are enabled when `disabled` is absent or false.
- Toggling a server writes `disabled: true` or `disabled: false` immediately.

Validation accepts a server if it has one of:

- `type: "stdio"` plus a non-empty `command`
- `type: "http"` or `type: "sse"` plus a non-empty `url`
- non-empty `httpUrl`
- any non-empty `url`
- non-empty `command`
- `transport`
- non-empty `remotes`

## Development Notes

- Keep the app dependency-free unless there is a strong reason. The current build works without a .NET SDK.
- Prefer preserving arbitrary JSON fields over introducing strict DTOs.
- Avoid writing secrets or user MCP configs into the repo. The Factory config can contain tokens.
- `dist/` is ignored and should not be committed.
- If adding new source files, no build script change is needed as long as they are under `WindowsMcpPanel`.
