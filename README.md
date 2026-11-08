<p align="center">
  <img src="./Assets/readme/hero.svg" width="100%" alt="Purge — Declutter your Windows PC safely. Native WinUI 3 multi-drive cache, developer artifact, and leftover cleaner.">
</p>

<p align="center">
  <strong>English</strong> · <a href="./README.fr.md">Français</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64%20%26%20ARM64)-6B4DE6?style=flat-square" alt="Platform: Windows 10/11">
  <img src="https://img.shields.io/badge/UI-WinUI%203%20%C2%B7%20.NET%2010-2E2D2B?style=flat-square" alt="UI: WinUI 3 .NET 10">
  <img src="https://img.shields.io/badge/Safety-Recycle%20Bin%20First-1F7A35?style=flat-square" alt="Safety: Recycle Bin First">
  <img src="https://img.shields.io/badge/Privacy-100%25%20Offline-2E2D2B?style=flat-square" alt="Privacy: 100% Offline">
  <a href="./LICENSE"><img src="https://img.shields.io/badge/License-MIT-ECEAE6?style=flat-square" alt="License: MIT"></a>
</p>

---

## Real Interface Preview

<p align="center">
  <img src="./Assets/readme/screenshot-overview-dark.png" width="100%" alt="Purge Overview in Dark Mode showing multi-drive storage breakdown and safe cleanup actions">
</p>

<p align="center">
  <img src="./Assets/readme/screenshot-overview-light.png" width="49%" alt="Purge Overview in Light Mode">
  <img src="./Assets/readme/screenshot-devtools-dark.png" width="49%" alt="Purge Dev Tools View showing npm, NuGet, and VS Code caches">
</p>

<p align="center">
  <img src="./Assets/readme/screenshot-treemap-dark.png" width="49%" alt="Purge Everything Else squarified treemap disk map">
  <img src="./Assets/readme/screenshot-settings-dark.png" width="49%" alt="Purge Settings View with System, Light, Dark themes and bilingual switcher">
</p>

---

## What is Purge?

**Purge** is a fast, native **WinUI 3** disk and developer cache cleaner for Windows. It inspects your local drives (`C:`, `D:`, `E:`, or all drives combined) and breaks down reclaimable space into five clear categories—plus an interactive squarified treemap of everything else on your PC.

Every item displays a plain-language explanation of what it is and what happens after you clean it. Nothing is permanently erased behind your back: **all cleaned files are moved to the Windows Recycle Bin first**.

---

## Why Purge is Different

<p align="center">
  <img src="./Assets/readme/architecture.svg" width="100%" alt="Purge 3-stage safety architecture: Multi-Drive Parallel Scan, Allowlist and Safety Guard, and Recycle Bin Shell API">
</p>

| Principle | How Purge Enforces It |
| :--- | :--- |
| **Recycle Bin by Default** | Uses native Windows Shell `SHFileOperationW` (`FOF_ALLOWUNDO`) so cleaned caches, build folders, and leftovers can be restored at any time. |
| **Strict Allowlist Policy** | Only verified cache locations in [`windows-cache-catalog.json`](./Resources/windows-cache-catalog.json) are eligible for one-click safe cleaning. Browser passwords, cookies, sessions, and SSH/GPG keys are never touched. |
| **Multi-Drive Scanning** | Scan all fixed and removable volumes (`All drives`) at once or filter by an individual drive (`C:`, `D:`, `E:`). |
| **Developer-First Intelligence** | Finds global package caches (`npm`, `pnpm`, `Yarn`, `Bun`, `NuGet`, `Cargo`, `pip`, `Gradle`, `Maven`, `Go`, `JetBrains`, `VS Code`, `Cursor`) and stale project build folders (`node_modules`, `target`, `.next`, `dist`, `bin/obj`, `.venv`, Android AVDs, and orphaned AI git worktrees). |
| **Cloud & Reparse-Point Safe** | Never traverses NTFS junctions/symlinks during sizing and detects OneDrive cloud-only files (`FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS`) with one-click local cache eviction (`attrib +U -P`). |
| **100% Offline & Bilingual** | Zero telemetry, zero network requests, instant **English / Français** switching, and adaptive **Dark / Light / System** themes. |

---

## Five Focused Modules + Squarified Treemap

1. **Overview & Everything Else Treemap** — High-contrast Okabe-Ito 7-segment storage bar across your drives, paired with a read-only Bruls–Huizing–van Wijk squarified treemap that classifies the rest of your disk into *Managed in Apps*, *Your Files*, *Left Alone by Purge*, and *Managed by Windows*.
2. **App Caches** — Browser caches (Chrome, Edge, Brave, Opera, Firefox), DirectX & GPU shader caches (NVIDIA, AMD, Intel), Windows Temp & WER reports, Discord, Slack, Teams, Figma, Notion, Spotify, Steam, Adobe Media Cache, and staged Electron updaters.
3. **Dev Tools** — Global toolchain caches, stale project build artifacts filterable by age (`All`, `> 30d`, `> 90d`, `> 180d`), Android Studio AVDs, and orphaned AI agent worktrees (`.claude/worktrees`, `.cursor/worktrees`, `.codex/worktrees`) with dirty-git-status protection.
4. **Large Files, AI Models & Duplicates** — Multi-threaded scanner (`>= 5 MB`) with category filters (*Videos*, *Archives*, *Disk Images*, *Virtual Machines*, *AI Models* like Ollama/LM Studio/Hugging Face `.gguf`/`.safetensors`, *OneDrive*, and two-stage partial+SHA-256 *Duplicates* with **Keep One of Each**).
5. **Uninstaller, Orphaned Leftovers & CLI Tools** — Inspects installed Win32/Registry apps alongside their `%APPDATA%` / `%LOCALAPPDATA%` data, detects orphaned `AppData` folders left behind by uninstalled apps, and lists standalone CLI binaries in `.cargo\bin`, `go\bin`, `.dotnet\tools`, and `scoop\shims`.

---

## Build & Run from Source

### Prerequisites

- **Windows 10** (build 19041+) or **Windows 11** (x64 or ARM64)
- **.NET 10 SDK** (`10.0.100` or later) with Windows App SDK workload

### Quick Start

```powershell
# Clone the repository
git clone https://github.com/stvekoulo/purge.git
cd purge

# Restore dependencies and build (x64)
dotnet build PurgeWin.csproj -p:Platform=x64

# Launch Purge
dotnet run --project PurgeWin.csproj -p:Platform=x64
```

### Command-Line Flags

| Flag | Description |
| :--- | :--- |
| *(none)* | Launches the full WinUI 3 desktop interface. |
| `--scheduled-clean` | Headless background mode registered by Windows Task Scheduler (`PurgeScheduledClean`): scans only verified `Safe` caches and moves them to the Recycle Bin. |
| `--selftest` | Runs an automated end-to-end verification pass across all scanners, tabs, themes, and languages, then exits with code `0`. |

---

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + R` | Scan current category |
| `Ctrl + Shift + R` | Scan all categories across active drives |
| `Ctrl + 1` … `Ctrl + 5` | Jump between *Overview*, *App Caches*, *Dev Tools*, *Large Files*, and *Uninstaller* |

---

## Project Structure

```text
purge/
├── Assets/                  # AppIcon (.ico/.png), Onest font, Windows theme preview cards
├── Controls/                # Custom pill button (PurgeButton) with Primary/Secondary/Destructive roles
├── Models/                  # Safety levels, CacheItem, DevModels, LargeFileModels, TreemapModels, DriveModels
├── Resources/               # Strict allowlist catalog (windows-cache-catalog.json)
├── Services/                # Multi-drive scanners, DeletionSafetyPolicy, native RecycleBin (SHFileOperationW)
├── Theme/                   # Warm graphite & alabaster palette (Colors.xaml, Styles.xaml, ThemeBrushes.cs)
├── Utilities/               # Bilingual localization (EN/FR), ByteFormatter, ExplorerReveal, SizeComparisons
├── ViewModels/              # Observable PurgeStore state container (Drives, Filters, Safe Cleaning)
└── Views/                   # WinUI 3 views (MainWindow, Overview, AppCaches, DevTools, LargeFiles, Uninstaller, Settings, About)
```

---

## License

Released under the [MIT License](./LICENSE).
