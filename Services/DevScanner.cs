using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// Purge for Windows:
/// with multi-drive support (scans C:\, D:\, E:\, etc. when selected).
public static class DevScanner
{
    private sealed record GlobalToolSpec(
        string Id, string TitleFr, string TitleEn, string Icon, SafetyLevel Level, string ExplainFr, string ExplainEn, string[] Paths);

    private sealed record ArtifactRule(
        string Folder, string ProjectType, string Icon, string[] RootMarkers, SafetyLevel Level, string Reinstall);

    private static readonly GlobalToolSpec[] GlobalSpecs =
    [
        new("dev-npm", "Cache npm", "npm Cache", "nodedotjs", SafetyLevel.Safe,
            "Archive des paquets téléchargés par npm. Recréé automatiquement lors du prochain npm install.",
            "Downloaded package tarballs cached by npm. Rebuilt automatically on the next npm install.",
            ["%LOCALAPPDATA%\\npm-cache", "%APPDATA%\\npm-cache"]),
        new("dev-pnpm", "Store pnpm", "pnpm Store", "pnpm", SafetyLevel.Medium,
            "Store adressé par contenu de pnpm. Le nettoyer libère de l'espace mais les projets retéléchargeront leurs paquets.",
            "Content-addressed store used by pnpm. Cleaning frees space; projects will re-fetch packages when needed.",
            ["%LOCALAPPDATA%\\pnpm\\store", "%USERPROFILE%\\.local\\share\\pnpm\\store"]),
        new("dev-yarn", "Cache Yarn", "Yarn Cache", "yarn", SafetyLevel.Safe,
            "Archive globale des paquets Yarn. Les projets installés ne sont pas touchés.",
            "Global package archive cached by Yarn. Installed projects are untouched.",
            ["%LOCALAPPDATA%\\Yarn\\Cache"]),
        new("dev-bun", "Cache Bun", "Bun Cache", "bun", SafetyLevel.Safe,
            "Paquets mis en cache par le gestionnaire Bun.",
            "Packages cached by the Bun package manager.",
            ["%USERPROFILE%\\.bun\\install\\cache"]),
        new("dev-nuget-http", "Cache HTTP NuGet", "NuGet HTTP Cache", "dotnet", SafetyLevel.Safe,
            "Réponses et paquets temporaires téléchargés par NuGet / .NET SDK.",
            "Temporary HTTP responses and scratch packages downloaded by NuGet / .NET SDK.",
            ["%LOCALAPPDATA%\\NuGet\\v3-cache", "%LOCALAPPDATA%\\Temp\\NuGetScratch"]),
        new("dev-pip", "Cache pip (Python)", "pip Cache (Python)", "python", SafetyLevel.Safe,
            "Wheels et archives téléchargés par pip. Vos environnements virtuels restent intacts.",
            "Downloaded wheels and source archives cached by pip. Virtual environments stay intact.",
            ["%LOCALAPPDATA%\\pip\\Cache"]),
        new("dev-uv", "Cache uv (Python)", "uv Cache (Python)", "uv", SafetyLevel.Safe,
            "Cache de roues et d'environnements construit par uv.",
            "Wheel and environment cache managed by uv.",
            ["%LOCALAPPDATA%\\uv\\cache"]),
        new("dev-gradle", "Caches Gradle", "Gradle Caches", "gradle", SafetyLevel.Safe,
            "Artefacts et wrappers compilés par Gradle. Le prochain build retéléchargera le nécessaire.",
            "Compiled artifacts and wrappers cached by Gradle. Re-downloaded on next build.",
            ["%USERPROFILE%\\.gradle\\caches", "%USERPROFILE%\\.gradle\\daemon"]),
        new("dev-cargo", "Cache Cargo (Rust)", "Cargo Registry Cache", "rust", SafetyLevel.Safe,
            "Crates téléchargées depuis crates.io dans ~/.cargo/registry/cache.",
            "Crates downloaded from crates.io in ~/.cargo/registry/cache.",
            ["%USERPROFILE%\\.cargo\\registry\\cache"]),
        new("dev-go", "Cache de build Go", "Go Build Cache", "go", SafetyLevel.Safe,
            "Objets compilés par Go (go-build). Reconstruits au prochain go build.",
            "Compiled build artifacts cached by Go (go-build). Rebuilt on next go build.",
            ["%LOCALAPPDATA%\\go-build"]),
        new("dev-playwright", "Navigateurs Playwright", "Playwright Browsers", "playwright", SafetyLevel.Medium,
            "Binaires Chromium, Firefox et WebKit téléchargés pour les tests Playwright.",
            "Chromium, Firefox, and WebKit browser binaries downloaded for Playwright testing.",
            ["%LOCALAPPDATA%\\ms-playwright"]),
        new("dev-vscode", "VS Code (CachedData & VSIX)", "VS Code (CachedData & VSIX)", "electron", SafetyLevel.Safe,
            "Bytecode V8 mis en cache et archives d'extensions déjà installées de Visual Studio Code.",
            "Cached V8 bytecode and already-installed extension VSIXs for Visual Studio Code.",
            ["%APPDATA%\\Code\\CachedData", "%APPDATA%\\Code\\CachedExtensionVSIXs", "%APPDATA%\\Code\\Cache", "%APPDATA%\\Code\\GPUCache"]),
        new("dev-cursor", "Cursor (CachedData & Cache)", "Cursor (CachedData & Cache)", "cursor", SafetyLevel.Safe,
            "Cache de rendu et bytecode de l'éditeur Cursor. Vos réglages et index restent intacts.",
            "Renderer cache and V8 bytecode for Cursor. Your settings and indexes remain untouched.",
            ["%APPDATA%\\Cursor\\CachedData", "%APPDATA%\\Cursor\\CachedExtensionVSIXs", "%APPDATA%\\Cursor\\Cache", "%APPDATA%\\Cursor\\GPUCache"]),
        new("dev-jetbrains", "Caches JetBrains IDEs", "JetBrains IDE Caches", "jetbrains", SafetyLevel.Safe,
            "Index et caches locaux d'IntelliJ, Rider, WebStorm ou PyCharm.",
            "Local indexes and caches for IntelliJ, Rider, WebStorm, or PyCharm.",
            ["%LOCALAPPDATA%\\JetBrains\\*\\caches", "%LOCALAPPDATA%\\JetBrains\\*\\index"]),
        new("dev-composer", "Cache Composer (PHP)", "Composer Cache (PHP)", "composer", SafetyLevel.Safe,
            "Archives de paquets PHP mises en cache par Composer.",
            "PHP package archives cached by Composer.",
            ["%LOCALAPPDATA%\\Composer\\cache"]),
    ];

    private static readonly ArtifactRule[] ProjectRules =
    [
        new("node_modules", "Node.js", "nodedotjs", ["package.json"], SafetyLevel.Safe, "npm install / pnpm install / yarn"),
        new(".next", "Next.js", "nextdotjs", ["package.json"], SafetyLevel.Safe, "npm run build"),
        new(".nuxt", "Nuxt", "nodedotjs", ["nuxt.config.ts", "nuxt.config.js"], SafetyLevel.Safe, "npm run build"),
        new(".svelte-kit", "SvelteKit", "nodedotjs", ["package.json"], SafetyLevel.Safe, "npm run build"),
        new(".astro", "Astro", "nodedotjs", ["package.json"], SafetyLevel.Safe, "npm run build"),
        new(".turbo", "Turborepo", "turbo", ["package.json"], SafetyLevel.Safe, "npx turbo build"),
        new("target", "Rust", "rust", ["Cargo.toml"], SafetyLevel.Safe, "cargo build"),
        new(".venv", "Python venv", "python", ["pyproject.toml", "requirements.txt"], SafetyLevel.Medium, "uv venv / python -m venv .venv"),
        new("venv", "Python venv", "python", ["pyproject.toml", "requirements.txt"], SafetyLevel.Medium, "python -m venv venv"),
        new(".pytest_cache", "Pytest", "python", ["pyproject.toml", "requirements.txt"], SafetyLevel.Safe, "pytest"),
        new(".gradle", "Gradle", "gradle", ["build.gradle", "build.gradle.kts"], SafetyLevel.Safe, "gradlew build"),
    ];

    public static async Task<List<DevItem>> ScanAsync(
        Action<DevItem>? onItem, ISet<string> excluded, int staleDays,
        IReadOnlyList<string> customRoots, IReadOnlyList<string> activeDrives, CancellationToken token,
        bool globalCachesOnly = false)
    {
        var list = new List<DevItem>();
        var scope = new PathScope(activeDrives, excluded);
        await Task.Run(() =>
        {
            bool OnActiveDrive(string p) =>
                scope.CanDelete(p);

            void Emit(DevItem item)
            {
                if (!item.Paths.All(OnActiveDrive)) return;
                lock (list) list.Add(item);
                onItem?.Invoke(item);
            }

            foreach (var spec in GlobalSpecs)
            {
                token.ThrowIfCancellationRequested();
                var paths = spec.Paths.SelectMany(PathExpander.Expand)
                    .Where(p => OnActiveDrive(p) && !excluded.Contains(p) && !DeletionSafetyPolicy.IsProtected(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (paths.Count == 0) continue;
                long bytes = 0;
                var newest = DateTime.MinValue;
                foreach (var p in paths)
                {
                    var (b, m) = FolderSizing.Measure(p, token);
                    bytes += b;
                    if (m > newest) newest = m;
                }
                if (bytes < 64 * 1024) continue;
                Emit(new DevItem
                {
                    Id = spec.Id, Title = spec.TitleFr, TitleEn = spec.TitleEn, Subtitle = paths[0],
                    Explanation = spec.ExplainFr, ExplanationEn = spec.ExplainEn,
                    Paths = paths, Bytes = bytes, LastModified = newest, Level = spec.Level,
                    Section = DevSectionKind.GlobalCache, Icon = spec.Icon,
                });
            }

            if (!globalCachesOnly)
            {
                ScanAndroidAvds(Emit, excluded, token, scope);
                ScanProjects(Emit, excluded, staleDays, customRoots, activeDrives, token);
                ScanOrphanWorktrees(Emit, excluded, token, scope);
            }
        }, token);
        return list.OrderByDescending(i => i.Bytes).ToList();
    }

    private static void ScanAndroidAvds(Action<DevItem> emit, ISet<string> excluded, CancellationToken token, PathScope scope)
    {
        var avdRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "avd");
        if (!Directory.Exists(avdRoot)) return;
        foreach (var dir in Directory.EnumerateDirectories(avdRoot, "*.avd"))
        {
            token.ThrowIfCancellationRequested();
            if (!scope.CanDelete(dir) || File.Exists(Path.Combine(dir, "hardware-qemu.ini.lock"))) continue;
            var (bytes, mod) = FolderSizing.Measure(dir, token);
            if (bytes < 1024 * 1024) continue;
            var name = Path.GetFileNameWithoutExtension(dir).Replace('_', ' ');
            var ini = Path.Combine(avdRoot, Path.GetFileNameWithoutExtension(dir) + ".ini");
            var paths = File.Exists(ini) ? new List<string> { dir, ini } : new List<string> { dir };
            emit(new DevItem
            {
                Id = $"avd:{name}", Title = $"AVD : {name}", Subtitle = dir,
                Explanation = "Image système et données de l'émulateur Android. À vérifier avant suppression.",
                ExplanationEn = "Android Emulator system image and userdata. Check before removing.",
                Paths = paths, Bytes = bytes, LastModified = mod, Level = SafetyLevel.Medium,
                Section = DevSectionKind.AndroidAvd, Icon = "android",
            });
        }
    }

    private static void ScanProjects(
        Action<DevItem> emit, ISet<string> excluded, int staleDays,
        IReadOnlyList<string> extraRoots, IReadOnlyList<string> activeDrives, CancellationToken token)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(home, "source"), Path.Combine(home, "repos"), Path.Combine(home, "projects"),
            Path.Combine(home, "Documents", "GitHub"),
        };
        string[] devRootNames = ["DEVS", "dev", "devs", "Projects", "Repos", "Source", "GitHub", "Workspace", "Code"];
        foreach (var drive in activeDrives)
        {
            foreach (var name in devRootNames)
            {
                var candidate = Path.Combine(drive, name);
                if (Directory.Exists(candidate)) roots.Add(candidate);
            }
        }
        foreach (var r in extraRoots) if (Directory.Exists(r)) roots.Add(r);

        var cutoff = staleDays <= 0 ? DateTime.MaxValue : DateTime.Now.AddDays(-staleDays);
        var currentDir = AppContext.BaseDirectory.TrimEnd('\\');

        var scope = new PathScope(activeDrives, excluded);
        foreach (var root in roots.Where(r => Directory.Exists(r) && scope.Allows(r) && !PathExpander.IsReparsePoint(r)))
        {
            WalkProjectDirs(root, 0, 3, projectDir =>
            {
                token.ThrowIfCancellationRequested();
                if (!scope.Allows(projectDir) || PathScope.IsWithin(currentDir, projectDir)) return;
                foreach (var rule in ProjectRules)
                {
                    var artifactPath = Path.Combine(projectDir, rule.Folder);
                    if (!Directory.Exists(artifactPath) || !scope.CanDelete(artifactPath) || PathExpander.IsReparsePoint(artifactPath))
                        continue;
                    if (!rule.RootMarkers.Any(m => File.Exists(Path.Combine(projectDir, m))))
                        continue;
                    var (bytes, mod) = FolderSizing.Measure(artifactPath, token);
                    if (bytes < 512 * 1024 || (staleDays > 0 && mod > cutoff)) continue;
                    var projName = Path.GetFileName(projectDir);
                    emit(new DevItem
                    {
                        Id = $"proj:{artifactPath}",
                        Title = $"{projName} — {rule.Folder}",
                        Subtitle = $"{rule.ProjectType} · {projectDir}",
                        Explanation = $"Artefact de build ({rule.Folder}). Reconstructible via : {rule.Reinstall}",
                        ExplanationEn = $"Build artifact ({rule.Folder}). Rebuild with: {rule.Reinstall}",
                        Paths = [artifactPath], Bytes = bytes, LastModified = mod, Level = rule.Level,
                        Section = DevSectionKind.ProjectArtifact, Icon = rule.Icon,
                        ProjectRoot = projectDir, ReinstallHint = rule.Reinstall,
                    });
                }
            });
        }
    }

    private static void WalkProjectDirs(string dir, int depth, int maxDepth, Action<string> visit)
    {
        if (depth > maxDepth) return;
        visit(dir);
        if (depth == maxDepth) return;
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (name.StartsWith('.') || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("target", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
                    || PathExpander.IsReparsePoint(sub)) continue;
                WalkProjectDirs(sub, depth + 1, maxDepth, visit);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
    }

    private static void ScanOrphanWorktrees(Action<DevItem> emit, ISet<string> excluded, CancellationToken token, PathScope scope)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots = [Path.Combine(home, ".cursor", "worktrees"), Path.Combine(home, ".codex", "worktrees"),
            Path.Combine(home, ".claude", "worktrees")];
        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    token.ThrowIfCancellationRequested();
                    if (!scope.CanDelete(dir)) continue;
                    var gitState = GitWorktreeInspector.Inspect(dir);
                    var (bytes, mod) = FolderSizing.Measure(dir, token);
                    if (bytes < 512 * 1024) continue;
                    emit(new DevItem
                    {
                        Id = $"wt:{dir}", Title = $"Worktree : {Path.GetFileName(dir)}", Subtitle = dir,
                        Explanation = "Worktree Git potentiellement actif. Les modifications non enregistrées ou non vérifiables sont protégées.",
                        ExplanationEn = "Potentially active Git worktree. Uncommitted or unverifiable changes are protected.",
                        Paths = [dir], Bytes = bytes, LastModified = mod, Level = SafetyLevel.Medium,
                        Section = DevSectionKind.OrphanWorktree, Icon = "git",
                        GitState = gitState,
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
    }
}
