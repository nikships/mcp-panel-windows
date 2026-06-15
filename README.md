# MCP Panel Windows

A native Windows version of [mcp-panel](https://github.com/nikships/mcp-panel) focused on the core MCP config-management workflow.

This app is intentionally small: it edits local `mcp.json` files directly, with no backend service and no cross-platform shell. Factory's `~/.factory/mcp.json` is the default, but any standard JSON config with an `mcpServers` object can be selected from the UI.

## Features

- OLED-style dark Windows UI
- Grid view of MCP servers
- In-window JSON editing per server
- Add servers from:
  - `mcpServers` wrappers
  - `servers` wrappers
  - raw named server entries
  - bare HTTP/HTTPS URLs
- Enable/disable servers in real time using each server's `disabled` field
- File picker for any local MCP JSON config
- Live reload when the selected config changes on disk
- Validation logic mirrored from the macOS app's core server model

## Validation Support

A server is considered valid when it has one of:

- `type: "stdio"` and a non-empty `command`
- `type: "http"` or `type: "sse"` and a non-empty `url`
- non-empty `httpUrl`
- any non-empty `url`
- non-empty `command`
- `transport`
- non-empty `remotes`

The importer also preserves unknown fields and normalizes common pasted formats such as command arrays, `environment` to `env`, stringified headers, curly quotes, trailing commas, and `servers`/`mcpServers` wrappers.

## Build

This repository builds with the Windows .NET Framework compiler included on standard Windows installs:

```powershell
.\build.ps1
```

The output is written to:

```text
dist\McpPanelWindows.exe
```

## CLI Checks

```powershell
.\dist\McpPanelWindows.exe --self-test
.\dist\McpPanelWindows.exe --validate-factory
.\dist\McpPanelWindows.exe --validate C:\path\to\mcp.json
```

Because the executable is built as a Windows GUI app, use `Start-Process -Wait` in scripts when validating temporary files.
