using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImGuiNET;
using Editor.State;
using Engine.Assets;
using OpenTK.Graphics.OpenGL4;
using Editor.UI;
using SysVec2 = System.Numerics.Vector2;
using AssetRecord = Engine.Assets.AssetDatabase.AssetRecord;
using Engine.Rendering;
using Editor.Icons;

namespace Editor.Panels
{
    public static class AssetsPanel
    {
        // ====== UI State général ======
        private static string _currentDir = "";           // relatif à AssetsRoot; "" = racine
        private static string _search = "";
        private static ViewMode _viewMode = ViewMode.Grid;
        private static SortMode _sort = SortMode.NameAsc;
        private static float _iconSize = 48f;

        private enum ViewMode { Grid, List }
        private enum SortMode { NameAsc, NameDesc, TypeAsc, TypeDesc }

        // ====== Sélections ======
        private static readonly HashSet<Guid> _selAssets = new();     // fichiers (assets)
        private static readonly HashSet<string> _selFolders = new(StringComparer.OrdinalIgnoreCase); // dossiers (relatifs)
        private static string _lastClickedKey = ""; // "asset:{guid}" ou "folder:{rel}"

        // Rectangle de sélection (Grid & List)
        private static bool _isRectSelecting = false;
        private static SysVec2 _rectStart;
        private static SysVec2 _rectEnd;

        // Bounds écran des tuiles/rangées (MAJ à chaque frame de draw)
        // key = "asset:{guid}" ou "folder:{rel}"
        private static readonly Dictionary<string, (SysVec2 tl, SysVec2 br)> _itemBounds = new();

        // Ordre d’affichage courant (folders puis assets) pour SHIFT-range
        private static readonly List<string> _displayOrder = new();

        // ====== DnD ======
        private const float DragThreshold = 4f;
        private static string _pendingClickKey = "";     // pour différencier click vs drag
        private static bool _pendingClickArmed = false;

        // ====== Création / Rename popups ======
        private enum NewKind { None, Folder, Material, SkyboxMaterial }
        private static NewKind _newKind = NewKind.None;
        private static string _newName = "";
        private static string _newTargetRel = "";
        private static bool _newPopupJustOpened = false;
        private const string NEW_POPUP_NAME = "Create##NewItemPopup";

        private enum RenameKind { None, Asset, Folder }
        private static RenameKind _renameKind = RenameKind.None;
        private static string _renameName = "";
        private static Guid _renameAssetGuid = Guid.Empty;
        private static string _renameFolderRel = "";
        private static bool _renameJustOpened = false;
        private const string RENAME_POPUP_NAME = "Rename##RenamePopup";

        // ====== FS watcher / refresh ======
        private static FileSystemWatcher? _watcher;
        private static bool _pendingRefresh;
        private static DateTime _lastFsEvent;
        private const int DebounceMs = 200;

    // ====== External OS drag & drop queue ======
    // We enqueue absolute paths dropped on the window, and import them next Draw.
    private static readonly Queue<string> _externalImportQueue = new();
    private static readonly object _externalImportLock = new();

        // ============= PUBLIC =============
        public static void Draw()
        {
            EnsureWatcher();

            if (_pendingRefresh && (DateTime.UtcNow - _lastFsEvent).TotalMilliseconds > DebounceMs)
            {
                _pendingRefresh = false;
                RefreshNow();
            }

            // Process any external imports before drawing contents, so items appear immediately
            TryProcessExternalImports();

            ImGui.Begin("Assets");

            DrawRenamePopup();
            DrawToolbar();

            // Splitter: tree à gauche / content à droite
            var avail = ImGui.GetContentRegionAvail();
            float leftW = MathF.Max(180f, avail.X * 0.22f);


            BeginChildCompat("##LeftTree", new SysVec2(leftW, 0), true);
            DrawFolderTree();
            ImGui.EndChild();

            ImGui.SameLine();

            bool rightChildOpen = BeginChildCompat("##RightContent", new SysVec2(0, 0), false);
            if (rightChildOpen)
            {
                DrawBreadcrumb();
                ImGui.Separator();
                DrawContent();
            }
            ImGui.EndChild();

            // popups de création
            DrawNewPopup();

            // F2 rename (si focus fenêtre)
            var io = ImGui.GetIO();
            if (!io.WantTextInput &&
                ImGui.IsWindowFocused(ImGuiFocusedFlags.RootWindow | ImGuiFocusedFlags.ChildWindows) &&
                ImGui.IsKeyPressed(ImGuiKey.F2))
            {
                if (_selAssets.Count == 1 && _selFolders.Count == 0)
                {
                    var g = _selAssets.First();
                    if (AssetDatabase.TryGet(g, out var rec))
                        OpenRenameAsset(g, Path.GetFileNameWithoutExtension(rec.Path));
                }
                else if (_selFolders.Count == 1 && _selAssets.Count == 0)
                {
                    var rel = _selFolders.First();
                    var leaf = rel.Contains('/') ? rel.Split('/').Last() : rel;
                    OpenRenameFolder(rel, leaf);
                }
                else if (_selAssets.Count == 0 && _selFolders.Count == 0 && !string.IsNullOrEmpty(_currentDir))
                {
                    var leaf = _currentDir.Split('/', '\\').Last();
                    OpenRenameFolder(_currentDir, leaf);
                }
            }

            ImGui.End();
        }

        // Called from Program on OS-level file drop (GameWindow.FileDrop)
        public static void EnqueueExternalImport(IEnumerable<string> absolutePaths)
        {
            if (absolutePaths == null) return;
            lock (_externalImportLock)
            {
                foreach (var p in absolutePaths)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        _externalImportQueue.Enqueue(p);
                }
            }
        }

        private static void TryProcessExternalImports()
        {
            List<string> toImport = new();
            lock (_externalImportLock)
            {
                while (_externalImportQueue.Count > 0)
                    toImport.Add(_externalImportQueue.Dequeue());
            }

            if (toImport.Count == 0) return;

            // Defer the actual import work to run after the ImGui frame (safe to call ForceRender there).
            var items = new List<string>(toImport);
            Editor.Utils.DeferredActions.Enqueue(() =>
            {
                // Show progress popup for imports (one step per file)
                ProgressManager.StepTracker? tracker = null;
                try { tracker = new ProgressManager.StepTracker("Importing Files", items.Count); } catch { tracker = null; }

                string relBase = _currentDir;
                string destBase = string.IsNullOrEmpty(relBase) ? AssetDatabase.AssetsRoot : Path.Combine(AssetDatabase.AssetsRoot, relBase);
                Directory.CreateDirectory(destBase);

                var importedPaths = new List<string>();
                int i = 0;
                foreach (var abs in items)
                {
                    try
                    {
                        i++;
                        tracker?.NextStep($"Importing {Path.GetFileName(abs)} ({i}/{items.Count})...");
                        if (Directory.Exists(abs)) ImportFolderRecursive(abs, destBase, importedPaths);
                        else if (File.Exists(abs)) ImportSingleFile(abs, destBase, importedPaths);
                    }
                    catch { }
                }

                // Refresh DB and update selection
                RefreshNow();
                _selAssets.Clear(); _selFolders.Clear();
                foreach (var p in importedPaths) if (AssetDatabase.TryGetByPath(p, out var rec)) _selAssets.Add(rec.Guid);

                try { tracker?.Complete($"Imported {items.Count} file(s)"); } catch { }
            });
        }

        private static void ImportFolderRecursive(string srcFolderAbs, string destBase, List<string> importedPaths)
        {
            // Create a unique target folder name under destBase
            string name = Path.GetFileName(srcFolderAbs.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string target = Path.Combine(destBase, SanitizeFileName(name));
            string candidate = target;
            int i = 1;
            while (Directory.Exists(candidate))
                candidate = target + "_" + (i++).ToString();
            target = candidate;
            Directory.CreateDirectory(target);

            // Copy all files (skip .meta) using our safe move/copy helpers to ensure unique names.
            foreach (var file in Directory.EnumerateFiles(srcFolderAbs, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(AssetDatabase.MetaExt, StringComparison.OrdinalIgnoreCase))
                    continue; // don't import meta from outside

                try
                {
                    // Compute relative path inside the src folder
                    string rel = Path.GetRelativePath(srcFolderAbs, file);
                    string destDir = Path.Combine(target, Path.GetDirectoryName(rel) ?? "");
                    Directory.CreateDirectory(destDir);

                    // Place file with unique name in destDir
                    string fileName = Path.GetFileName(file);
                    string destAbs = EnsureUniquePath(destDir, fileName);
                    File.Copy(file, destAbs, overwrite: false);
                    importedPaths.Add(destAbs);
                }
                catch { }
            }

            // After copy, refresh once at the end by caller; here we can optionally collect GUIDs by mapping paths
            // We'll add GUIDs post-refresh in TryProcessExternalImports via AssetDatabase
        }

    private static void ImportSingleFile(string srcAbs, string destDir, List<string> importedPaths)
        {
            // Skip importing .meta from outside
            if (srcAbs.EndsWith(AssetDatabase.MetaExt, StringComparison.OrdinalIgnoreCase)) return;

            Directory.CreateDirectory(destDir);
            string fileName = Path.GetFileName(srcAbs);
            string destAbs = EnsureUniquePath(destDir, fileName);
            try
            {
                File.Copy(srcAbs, destAbs, overwrite: false);
            }
            catch
            {
                // fallback: try overwrite behavior safeguarding meta
                try { SafeMoveOrCopyDelete(srcAbs, destAbs); } catch { }
            }

            // Resolve after refresh by path
            importedPaths.Add(destAbs);
            Console.WriteLine($"[Assets] Copied imported file to: {destAbs}");

            // If the imported file is HDR/EXR and the user enabled auto PMREM, run the importer
            try
            {
                var ext = Path.GetExtension(destAbs)?.ToLowerInvariant() ?? "";
                if (ext == ".hdr" || ext == ".exr")
                {
                    Console.WriteLine($"[Assets] Detected HDR/EXR file: {destAbs}");
                    if (!Editor.State.EditorSettings.AutoGeneratePMREMOnImport)
                    {
                        Console.WriteLine("[Assets] Auto PMREM generation is disabled in Editor settings. Skipping PMREM generation.");
                    }
                    else
                    {
                        // Build out folder under Assets/Generated/Env/<basename>
                        var baseName = Path.GetFileNameWithoutExtension(destAbs);
                        var outRel = Path.Combine("Generated", "Env", baseName).Replace(Path.DirectorySeparatorChar, '/');

                        // Prefer configured cmgen path if available
                        var cmgen = Editor.State.EditorSettings.CmgenPath;
                        var cmgenDisplay = string.IsNullOrEmpty(cmgen) ? "(system PATH)" : cmgen;
                        Console.WriteLine($"[Assets] Auto PMREM enabled — invoking cmgen {cmgenDisplay} for {baseName} -> Assets/{outRel}");

                        var args = new string[] {
                            "--cmgen", cmgen,
                            "--input", destAbs,
                            "--out", outRel,
                            "--size", "512"
                        };

                        Console.WriteLine("[Assets] Starting PMREM generation (running asynchronously)...");

                        // Show a progress popup immediately
                        var pmremTitle = $"Generating PMREM: {baseName}";
                        Editor.UI.ProgressManager.Show(pmremTitle, "Running cmgen...");
                        try { Editor.UI.ProgressManager.ForceRender(); } catch { }

                        // Run cmgen in background so UI stays responsive
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                var ret = Editor.Tools.PMREMImporter.RunFromArgs(args);
                                if (ret != 0)
                                {
                                    Console.WriteLine($"[Assets] PMREM generation failed for {destAbs} (exit {ret})");
                                    Editor.UI.ProgressManager.Update(1.0f, "PMREM failed");
                                }
                                else
                                {
                                    Console.WriteLine($"[Assets] PMREM generation completed successfully. Outputs placed under Assets/{outRel}");
                                    Editor.UI.ProgressManager.Update(1.0f, "PMREM generation complete");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Assets] PMREM generation threw exception: {ex.Message}");
                                Editor.UI.ProgressManager.Update(1.0f, "PMREM failed");
                            }
                            finally
                            {
                                // Give user a short moment to read the final message, then hide
                                try { System.Threading.Thread.Sleep(600); } catch { }
                                Editor.UI.ProgressManager.Hide();
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Assets] Auto PMREM import failed: " + ex.Message);
            }
        }

        // ============= Toolbar =============
        private static void DrawToolbar()
        {
            if (IconManager.IconButton("refresh", "Refresh Assets")) RefreshNow();
            ImGui.SameLine();

            if (IconManager.IconButton("material", "New Material"))
            {
                _newKind = NewKind.Material;
                _newName = "NewMaterial";
                _newTargetRel = _currentDir;
                _newPopupJustOpened = true;
            }
            ImGui.SameLine();
            if (IconManager.IconButton("sphere", "New Skybox Material"))
            {
                _newKind = NewKind.SkyboxMaterial;
                _newName = "NewSkyboxMaterial";
                _newTargetRel = _currentDir;
                _newPopupJustOpened = true;
            }
            ImGui.SameLine();
            if (IconManager.IconButton("folder", "New Folder"))
            {
                _newKind = NewKind.Folder;
                _newName = "NewFolder";
                _newTargetRel = _currentDir;
                _newPopupJustOpened = true;
            }

            ImGui.SameLine();
            // Grid/List view toggle avec icônes
            if (IconManager.IconButton("grid_view", "Grid View")) 
                _viewMode = ViewMode.Grid;
            ImGui.SameLine();
            if (IconManager.IconButton("list_view", "List View"))
                _viewMode = ViewMode.List;

            ImGui.SameLine();
            ImGui.TextDisabled("Icon:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            ImGui.SliderFloat("##IconSize", ref _iconSize, 48f, 160f, "%.0f");

            ImGui.SameLine();
            ImGui.TextDisabled("Sort:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(140);
            if (ImGui.BeginCombo("##Sort", _sort.ToString()))
            {
                foreach (var s in Enum.GetValues(typeof(SortMode)).Cast<SortMode>())
                {
                    bool sel = s == _sort;
                    if (ImGui.Selectable(s.ToString(), sel)) _sort = s;
                    if (sel) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(240);
            ImGui.InputTextWithHint("##search", "Search name or type...", ref _search, 128);

            ImGui.SameLine();
            ImGui.TextDisabled(AssetDatabase.AssetsRoot);
        }

        // ============= Tree (gauche) =============
        private static void DrawFolderTree()
        {
            var dirs = EnumerateAllDirsRelative(AssetDatabase.AssetsRoot);

            bool openRoot = ImGui.TreeNodeEx("Assets",
                ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanFullWidth);
            if (ImGui.IsItemClicked())
            {
                _currentDir = "";
                ClearSelection();
            }
            if (openRoot)
            {
                DrawTreeChildren("", dirs);
                ImGui.TreePop();
            }
        }

        // Compat: utilisé par MaterialInspector / MaterialAssetInspector.
        // Récupère le premier GUID depuis le payload multi-asset.
        public unsafe static bool TryConsumeDraggedAsset(out Guid guid)
        {
            guid = Guid.Empty;

            // On lit d'abord le payload multi (nouveau format).
            var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
            if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
            {
                unsafe
                {
                    var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
                    guid = new Guid(span.Slice(0, 16)); // premier GUID
                }
                return true;
            }

            // (Optionnel) fallback si tu veux aussi supporter un ancien "ASSET" unitaire :
            // var legacy = ImGui.AcceptDragDropPayload("ASSET");
            // if (legacy.NativePtr != IntPtr.Zero) { /* si tu remets un buffer statique */ }

            return false;
        }

        private static void DrawTreeChildren(string parentRel, SortedSet<string> allDirs)
        {
            var children = allDirs.Where(d => IsDirectChild(parentRel, d))
                                  .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);

            foreach (var rel in children)
            {
                string name = rel.Contains('/') ? rel.Split('/').Last() : rel;
                ImGui.PushID($"tree::{rel}");

                var flags = ImGuiTreeNodeFlags.SpanFullWidth;
                bool open = ImGui.TreeNodeEx(name, flags);

                // DnD cible : accepter drop sur le nœud du tree
                if (ImGui.BeginDragDropTarget())
                {
                    if (TryAcceptAssetPayload(out var assetGuids))
                    {
                        MoveAssetsToFolder(assetGuids, rel);
                        RefreshNow();
                    }
                    if (TryAcceptFolderPayload(out var folderRels))
                    {
                        MoveFoldersToFolder(folderRels, rel);
                        RefreshNow();
                    }
                    ImGui.EndDragDropTarget();
                }

                if (ImGui.IsItemClicked())
                {
                    _currentDir = rel;
                    ClearSelection();
                }

                if (open)
                {
                    DrawTreeChildren(rel, allDirs);
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
        }

        // ============= Breadcrumb =============
        private static void DrawBreadcrumb()
        {
            ImGui.PushID("breadcrumb");

            if (ImGui.SmallButton("Assets"))
            {
                _currentDir = "";
                ClearSelection();
            }

            var parts = _currentDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string acc = "";
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                acc = string.IsNullOrEmpty(acc) ? p : acc + "/" + p;

                ImGui.SameLine();
                ImGui.TextDisabled(">");
                ImGui.SameLine();

                ImGui.PushID(i);
                if (ImGui.SmallButton(p))
                {
                    _currentDir = acc;
                    ClearSelection();
                }
                ImGui.PopID();
            }

            ImGui.PopID();
        }

        // ============= Content (droite) =============
        private static void DrawContent()
        {
            // Build dataset with thread-safe snapshot
            var childDirs = ListChildDirsFs(_currentDir); // dossiers directs
            
            // Create a snapshot to avoid race conditions during import
            List<AssetRecord> allAssets;
            try
            {
                allAssets = AssetDatabase.All().ToList();
            }
            catch (InvalidOperationException)
            {
                // Collection was modified during enumeration - skip this frame
                return;
            }
            
            var filesInDir = FilterByDirectory(allAssets, _currentDir);
            var filtered = ApplySearch(filesInDir, _search);
            filtered = SortAssets(filtered.ToList(), _sort);

            // Menu contextuel dans le vide
            if (ImGui.BeginPopupContextWindow("ContentContextMenu",
                ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (ImGui.MenuItem("New Material"))
                {
                    _newKind = NewKind.Material;
                    _newName = "NewMaterial";
                    _newTargetRel = _currentDir;
                    _newPopupJustOpened = true;
                }
                if (ImGui.MenuItem("New Folder"))
                {
                    _newKind = NewKind.Folder;
                    _newName = "NewFolder";
                    _newTargetRel = _currentDir;
                    _newPopupJustOpened = true;
                }
                ImGui.EndPopup();
            }

            // Reset caches pour cette frame
            _itemBounds.Clear();
            _displayOrder.Clear();

            if (_viewMode == ViewMode.Grid) DrawGrid(childDirs, filtered);
            else DrawList(childDirs, filtered);

            // Delete raccourci
            if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
                ImGui.IsKeyPressed(ImGuiKey.Delete) &&
                (_selAssets.Count > 0 || _selFolders.Count > 0))
            {
                DeleteSelection();
            }

            // Clic dans le vide → clear
            ImGui.Dummy(new SysVec2(0, 8));
            if (ImGui.IsWindowHovered() &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                !ImGui.IsAnyItemHovered())
            {
                ClearSelection();
            }

            // Rectangle de sélection (remplacement)
            HandleSelectionRectangle();

            // Status
            ImGui.Separator();
            ImGui.TextDisabled($"{filtered.Count} assets | {_selFolders.Count} folders | {_selAssets.Count} selected");
        }

        // ===== Grid =====
        private static void DrawGrid(List<string> childDirs, List<AssetRecord> files)
        {
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float startX = ImGui.GetCursorPosX();
            float startY = ImGui.GetCursorPosY();

            float tileW = MathF.Max(72f, _iconSize + 32f);
            float labelH = ImGui.GetTextLineHeightWithSpacing() * 1.7f;
            float tileH = _iconSize + labelH + 14f;

            float regionW = ImGui.GetContentRegionAvail().X;
            int cols = Math.Max(1, (int)MathF.Floor((regionW + spacing) / (tileW + spacing)));
            if (cols > 16) cols = 16;

            int col = 0, row = 0;

            // folders
            foreach (var rel in childDirs)
            {
                var pos = new SysVec2(startX + col * (tileW + spacing), startY + row * (tileH + spacing));
                ImGui.SetCursorPos(pos);
                DrawFolderTileGrid(rel, tileW, tileH);

                _displayOrder.Add(KeyFolder(rel));
                if (++col >= cols) { col = 0; row++; }
            }

            // assets
            foreach (var a in files)
            {
                var pos = new SysVec2(startX + col * (tileW + spacing), startY + row * (tileH + spacing));
                ImGui.SetCursorPos(pos);
                DrawAssetTileGrid(a, tileW, tileH);

                _displayOrder.Add(KeyAsset(a.Guid));
                if (++col >= cols) { col = 0; row++; }
            }

            ImGui.SetCursorPos(new SysVec2(startX, startY + (row + (col > 0 ? 1 : 0)) * (tileH + spacing)));
        }

        private static void DrawFolderTileGrid(string relDir, float tileW, float tileH)
        {
            ImGui.PushID($"folder::{relDir}");

            var tl = ImGui.GetCursorScreenPos();
            var br = new SysVec2(tl.X + tileW, tl.Y + tileH);
            _itemBounds[KeyFolder(relDir)] = (tl, br);

            var dl = ImGui.GetWindowDrawList();

            // Hitbox
            ImGui.InvisibleButton("##hit", new SysVec2(tileW, tileH));
            bool hovered = ImGui.IsItemHovered();
            bool dbl = hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
            bool selected = _selFolders.Contains(relDir);

            // DnD cible
            bool dropHovered = false;
            if (ImGui.BeginDragDropTarget())
            {
                if (TryAcceptAssetPayload(out var assetGuids))
                {
                    MoveAssetsToFolder(assetGuids, relDir);
                    RefreshNow();
                }
                if (TryAcceptFolderPayload(out var folderRels))
                {
                    MoveFoldersToFolder(folderRels, relDir);
                    RefreshNow();
                }
                ImGui.EndDragDropTarget();
                dropHovered = true;
            }

            // Encadré / hover / sélection / feedback drop
            DrawTileFrame(dl, tl, br, hovered, selected, dropHovered);

            // Icône
            var iconTL = new SysVec2(tl.X + 10, tl.Y + 10);
            var iconBR = new SysVec2(iconTL.X + _iconSize, iconTL.Y + _iconSize);
            // Ancien placeholder :
            // dl.AddRectFilled(iconTL, iconBR, 0xFF3A3A3A, 6);
            // dl.AddText(new SysVec2(iconTL.X + 8, iconTL.Y + 8), 0xFFFFFFFF, "📁");

            // Nouveau : utiliser l'icône "folder"
            var tex = IconManager.GetIconTexture("folder", (int)_iconSize);
            if (tex != nint.Zero)
            {
                dl.AddImage(tex, iconTL, iconBR, new SysVec2(0,0), new SysVec2(1,1), 0xFFFFFFFF);
            }
            else
            {
                // petit fallback au cas où
                dl.AddRectFilled(iconTL, iconBR, 0xFF3A3A3A, 6);
                dl.AddText(new SysVec2(iconTL.X + 8, iconTL.Y + 8), 0xFFFFFFFF, "[folder]");
            }

            // Label
            var name = relDir.Contains('/') ? relDir.Split('/').Last() : relDir;
            string shown = FitTextEllipsis(name, tileW - 14f);
            var textPos = new SysVec2(tl.X + 7, br.Y - ImGui.GetTextLineHeight() - 6);
            dl.AddText(textPos, 0xCCFFFFFF, shown);

            // Gestion click vs drag
            if (ImGui.IsItemActivated())
            {
                _pendingClickArmed = true;
                _pendingClickKey = KeyFolder(relDir);
            }
            bool dragging = ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, DragThreshold);

            if (dragging)
            {
                _pendingClickArmed = false;
                BeginDragFromCurrentSelection(originKey: KeyFolder(relDir), includeFolders: true, includeAssets: true);
            }

            if (ImGui.IsItemDeactivated() && _pendingClickArmed && _pendingClickKey == KeyFolder(relDir))
            {
                HandleFolderClick(relDir); // simple clic = sélection
                _pendingClickArmed = false;
            }

            if (dbl && !dragging)
            {
                _currentDir = relDir;
                ClearSelection();
            }

            // Contexte
            if (ImGui.BeginPopupContextItem($"FolderCtx##{relDir}"))
            {
                if (ImGui.MenuItem("New Material"))
                {
                    _newKind = NewKind.Material; _newName = "NewMaterial";
                    _newTargetRel = relDir; _newPopupJustOpened = true;
                }
                if (ImGui.MenuItem("New Skybox Material"))
                {
                    _newKind = NewKind.SkyboxMaterial; _newName = "NewSkyboxMaterial";
                    _newTargetRel = relDir; _newPopupJustOpened = true;
                }
                if (ImGui.MenuItem("New Folder"))
                {
                    _newKind = NewKind.Folder; _newName = "NewFolder";
                    _newTargetRel = relDir; _newPopupJustOpened = true;
                }
                if (ImGui.MenuItem("Rename Folder"))
                {
                    var leaf = name;
                    OpenRenameFolder(relDir, leaf);
                }
                if (ImGui.MenuItem("Delete Folder"))
                {
                    TryDeleteFolder(relDir);
                    RefreshNow();
                }
                if (ImGui.MenuItem("Reveal in Explorer"))
                    RevealFile(Path.Combine(AssetDatabase.AssetsRoot, relDir));
                ImGui.EndPopup();
            }

            ImGui.PopID();
        }

        private static void DrawAssetTileGrid(AssetRecord a, float tileW, float tileH)
        {
            ImGui.PushID(a.Guid.ToString());
            var tl = ImGui.GetCursorScreenPos();
            var br = new SysVec2(tl.X + tileW, tl.Y + tileH);
            _itemBounds[KeyAsset(a.Guid)] = (tl, br);

            var dl = ImGui.GetWindowDrawList();

            ImGui.InvisibleButton("##hit", new SysVec2(tileW, tileH));
            bool hovered = ImGui.IsItemHovered();
            bool dbl = hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
            bool selected = _selAssets.Contains(a.Guid);

            // Cible drop: les assets ne sont pas des destinations (pas de BeginDragDropTarget ici)

            DrawTileFrame(dl, tl, br, hovered, selected, dropHovered: false);

            // Icône / preview
            var iconTL = new SysVec2(tl.X + 10, tl.Y + 10);
            var iconBR = new SysVec2(iconTL.X + _iconSize, iconTL.Y + _iconSize);

            if (a.Type.Equals("Texture2D", StringComparison.OrdinalIgnoreCase))
            {
                int handle = TextureCache.GetOrLoad(a.Guid, g => AssetDatabase.TryGet(g, out var rr) ? rr.Path : null);
                if (handle != 0)
                {
                    ImGui.GetWindowDrawList().AddImage(
                        (IntPtr)handle, iconTL, iconBR,
                        new SysVec2(0, 1), new SysVec2(1, 0), 0xFFFFFFFF);
                }
                else
                {
                    // Fallback : icône "texture" au lieu de "Missing"
                    var tex = IconManager.GetIconTexture("texture", (int)_iconSize);
                    if (tex != nint.Zero)
                        ImGui.GetWindowDrawList().AddImage(tex, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                    else
                    {
                        ImGui.GetWindowDrawList().AddRectFilled(iconTL, iconBR, 0xFF444444, 6);
                        ImGui.GetWindowDrawList().AddText(new SysVec2(iconTL.X + 8, iconTL.Y + 8), 0xFFFFFFFF, "[texture]");
                    }
                }
            }
            else if (a.Type.Equals("Material", StringComparison.OrdinalIgnoreCase))
            {
                // Rendu de l’icône "material"
                var tex = IconManager.GetIconTexture("material", (int)_iconSize);
                if (tex != nint.Zero)
                    ImGui.GetWindowDrawList().AddImage(tex, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                else
                    ImGui.GetWindowDrawList().AddRectFilled(iconTL, iconBR, 0xFF444444, 6);

                // (Optionnel) petite pastille avec la couleur d’albedo si dispo
                var col = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1f);
                try
                {
                    var mat = AssetDatabase.LoadMaterial(a.Guid);
                    if (mat?.AlbedoColor is { Length: >= 3 })
                        col = new(mat.AlbedoColor[0], mat.AlbedoColor[1], mat.AlbedoColor[2], 1f);
                }
                catch { }

                var chip = new SysVec2(16, 16);
                var chipTL = new SysVec2(iconBR.X - chip.X - 4, iconBR.Y - chip.Y - 4);
                var chipBR = new SysVec2(iconBR.X - 4, iconBR.Y - 4);
                uint abgr = ImGui.ColorConvertFloat4ToU32(col);
                //ImGui.GetWindowDrawList().AddRectFilled(chipTL, chipBR, abgr, 4);
                //ImGui.GetWindowDrawList().AddRect(chipTL, chipBR, 0xAA000000, 4);
            }
            else if (a.Type.Equals("MeshAsset", StringComparison.OrdinalIgnoreCase) ||
                     a.Type.StartsWith("Model", StringComparison.OrdinalIgnoreCase))
            {
                // Mesh/Model assets - show 3D model icon
                var tex = IconManager.GetIconTexture("model", (int)_iconSize);
                if (tex != nint.Zero)
                    ImGui.GetWindowDrawList().AddImage(tex, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                else
                {
                    // Fallback: draw a simple cube representation
                    ImGui.GetWindowDrawList().AddRectFilled(iconTL, iconBR, 0xFF444444, 6);
                    ImGui.GetWindowDrawList().AddText(new SysVec2(iconTL.X + 8, iconTL.Y + 8), 0xFFFFFFFF, "[mesh]");
                }
            }
            else
            {
                // Essaye une icône spécifique si tu as une correspondance simple
                var key = a.Type.Equals("Folder", StringComparison.OrdinalIgnoreCase) ? "folder" :
                          Path.GetExtension(a.Path).Equals(".scene", StringComparison.OrdinalIgnoreCase) ? "scene_file" :
                          "file"; // mets une "file" générique si tu en ajoutes une dans le JSON

                var tex = IconManager.GetIconTexture(key, (int)_iconSize);
                if (tex != nint.Zero)
                    ImGui.GetWindowDrawList().AddImage(tex, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                else
                    ImGui.GetWindowDrawList().AddRectFilled(iconTL, iconBR, 0xFF444444, 6);
            }

            // Label
            var label = FitTextEllipsis(DisplayName(a), tileW - 14f);
            var textPos = new SysVec2(tl.X + 7, br.Y - ImGui.GetTextLineHeight() - 6);
            ImGui.GetWindowDrawList().AddText(textPos, 0xCCFFFFFF, label);

            // Click vs drag
            if (ImGui.IsItemActivated())
            {
                _pendingClickArmed = true;
                _pendingClickKey = KeyAsset(a.Guid);
            }
            bool dragging = ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, DragThreshold);
            if (dragging)
            {
                _pendingClickArmed = false;
                // Si c'est un script .cs, envoie un payload SCRIPT_FILE
                if (a.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.BeginDragDropSource())
                    {
                        var pathBytes = System.Text.Encoding.UTF8.GetBytes(a.Path);
                        unsafe { fixed (byte* p = pathBytes) ImGui.SetDragDropPayload("SCRIPT_FILE", (IntPtr)p, (uint)pathBytes.Length); }
                        ImGui.TextUnformatted(System.IO.Path.GetFileName(a.Path));
                        ImGui.EndDragDropSource();
                    }
                }
                else
                {
                    BeginDragFromCurrentSelection(originKey: KeyAsset(a.Guid), includeFolders: true, includeAssets: true);
                }
            }
            if (ImGui.IsItemDeactivated() && _pendingClickArmed && _pendingClickKey == KeyAsset(a.Guid))
            {
                HandleAssetClick(a.Guid);
                _pendingClickArmed = false;
            }

            if (dbl && !dragging)
            {
                // If it's a C# script, open it in the external editor
                if (a.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    State.EditorSettings.OpenScript(a.Path);
                }
                else
                {
                    Selection.SetActiveAsset(a.Guid, a.Type);
                }
            }

            // Contexte
            if (ImGui.BeginPopupContextItem($"AssetCtx##{a.Guid}"))
            {
                // Open C# Script option for .cs files
                if (a.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (ImGui.MenuItem("Open C# Script"))
                    {
                        State.EditorSettings.OpenScript(a.Path);
                    }
                    ImGui.Separator();
                }
                
                if (ImGui.MenuItem("Reveal in Explorer")) RevealFile(a.Path);

                // If this is an HDR-like texture, offer quick creation of a skybox material
                try
                {
                    var ext = Path.GetExtension(a.Path).ToLowerInvariant();
                    bool isHdr = a.Type.Equals("TextureHDR", StringComparison.OrdinalIgnoreCase) || ext == ".hdr" || ext == ".exr";
                    if (isHdr)
                    {
                        if (ImGui.MenuItem("Create Skybox Material from HDR"))
                        {
                            try
                            {
                                var rec = a; // small alias
                                var skyboxMat = new Engine.Assets.SkyboxMaterialAsset
                                {
                                    Guid = Guid.NewGuid(),
                                    Name = $"Skybox_{Path.GetFileNameWithoutExtension(rec.Path)}",
                                    // Default to panoramic; we may switch to cubemap if PMREM outputs are present
                                    Type = Engine.Assets.SkyboxType.Panoramic,
                                    PanoramicTexture = rec.Guid,
                                    PanoramicTint = new float[] { 1f, 1f, 1f, 1f },
                                    PanoramicExposure = 1.0f,
                                    PanoramicRotation = 0.0f,
                                    Mapping = Engine.Assets.PanoramicMapping.Latitude_Longitude_Layout,
                                    ImageType = Engine.Assets.PanoramicImageType.Degrees360
                                };

                                // If PMREM outputs were generated for this HDR (Assets/Generated/Env/<basename>), prefer the generated cubemap
                                try
                                {
                                    var baseName = Path.GetFileNameWithoutExtension(rec.Path);
                                    var pmremFolder = Path.Combine(AssetDatabase.AssetsRoot, "Generated", "Env", baseName);
                                    if (Directory.Exists(pmremFolder))
                                    {
                                        // Look for a generated skybox cubemap file (ktx)
                                        var files = Directory.GetFiles(pmremFolder, "*_skybox.ktx", SearchOption.TopDirectoryOnly);
                                        if (files.Length == 0)
                                        {
                                            // fallback: look for any .ktx in the folder
                                            files = Directory.GetFiles(pmremFolder, "*.ktx", SearchOption.TopDirectoryOnly);
                                        }
                                        if (files.Length > 0)
                                        {
                                            var ktxPath = files[0];
                                            // Convert to project-relative path for AssetDatabase lookup
                                            var rel = Path.GetRelativePath(AssetDatabase.AssetsRoot, ktxPath);
                                            var abs = ktxPath;
                                            if (Engine.Assets.AssetDatabase.TryGetByPath(abs, out var recKtx))
                                            {
                                                skyboxMat.Type = Engine.Assets.SkyboxType.Cubemap;
                                                skyboxMat.CubemapTexture = recKtx.Guid;
                                                skyboxMat.CubemapExposure = 1.0f;
                                                Console.WriteLine($"[Assets] Detected PMREM cubemap and linked to SkyboxMaterial: {rel}");
                                            }
                                        }
                                    }
                                }
                                catch { }

                                    // Additionally, look for PNG face outputs (cmgen -f png) and create a six-sided skybox if present
                                    try
                                    {
                                        var baseName = Path.GetFileNameWithoutExtension(rec.Path);
                                        var pmremFolder = Path.Combine(AssetDatabase.AssetsRoot, "Generated", "Env", baseName);
                                        if (Directory.Exists(pmremFolder))
                                        {
                                            var pngs = Directory.GetFiles(pmremFolder, "*.png", SearchOption.TopDirectoryOnly);
                                            if (pngs.Length >= 6)
                                            {
                                                // Attempt to map by common face name patterns
                                                string?[] faces = new string?[6]; // +X, -X, +Y, -Y, +Z, -Z
                                                foreach (var p in pngs)
                                                {
                                                    var n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                                                    if (n.Contains("posx") || n.Contains("px") || n.Contains("right")) faces[0] = p;
                                                    else if (n.Contains("negx") || n.Contains("nx") || n.Contains("left")) faces[1] = p;
                                                    else if (n.Contains("posy") || n.Contains("py") || n.Contains("up") || n.Contains("top")) faces[2] = p;
                                                    else if (n.Contains("negy") || n.Contains("ny") || n.Contains("down") || n.Contains("bottom")) faces[3] = p;
                                                    else if (n.Contains("posz") || n.Contains("pz") || n.Contains("front")) faces[4] = p;
                                                    else if (n.Contains("negz") || n.Contains("nz") || n.Contains("back")) faces[5] = p;
                                                }

                                                // If any face missing, fallback to first 6 alphabetical files
                                                if (faces.Any(f => f == null))
                                                {
                                                    Array.Sort(pngs, StringComparer.OrdinalIgnoreCase);
                                                    for (int i = 0; i < 6; i++) faces[i] = pngs[i];
                                                }

                                                // Resolve paths to asset GUIDs
                                                Guid? TryGetGuid(string? path)
                                                {
                                                    if (path == null) return null;
                                                    if (Engine.Assets.AssetDatabase.TryGetByPath(path, out var r)) return r.Guid;
                                                    return null;
                                                }

                                                var g0 = TryGetGuid(faces[0]);
                                                var g1 = TryGetGuid(faces[1]);
                                                var g2 = TryGetGuid(faces[2]);
                                                var g3 = TryGetGuid(faces[3]);
                                                var g4 = TryGetGuid(faces[4]);
                                                var g5 = TryGetGuid(faces[5]);

                                                if (g0.HasValue && g1.HasValue && g2.HasValue && g3.HasValue && g4.HasValue && g5.HasValue)
                                                {
                                                    skyboxMat.Type = Engine.Assets.SkyboxType.SixSided;
                                                    skyboxMat.RightTexture = g0; // +X
                                                    skyboxMat.LeftTexture = g1;  // -X
                                                    skyboxMat.UpTexture = g2;    // +Y
                                                    skyboxMat.DownTexture = g3;  // -Y
                                                    skyboxMat.FrontTexture = g4; // +Z
                                                    skyboxMat.BackTexture = g5;  // -Z
                                                    Console.WriteLine($"[Assets] Detected PMREM PNG faces and linked six-sided SkyboxMaterial from: {pmremFolder}");
                                                }
                                            }
                                        }
                                    }
                                    catch { }

                                string baseDir = Path.GetDirectoryName(rec.Path) ?? AssetDatabase.AssetsRoot;
                                string skyPath = Path.Combine(baseDir, skyboxMat.Name + Engine.Assets.AssetDatabase.SkyboxExt);
                                // unique path
                                int counter = 1;
                                string candidate = skyPath;
                                while (File.Exists(candidate))
                                {
                                    candidate = Path.Combine(baseDir, skyboxMat.Name + "_" + (counter++)) + Engine.Assets.AssetDatabase.SkyboxExt;
                                }
                                skyPath = candidate;

                                Engine.Assets.SkyboxMaterialAsset.Save(skyPath, skyboxMat);

                                // Write meta file
                                var meta = new { guid = skyboxMat.Guid.ToString(), type = "SkyboxMaterial" };
                                File.WriteAllText(skyPath + ".meta", System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                                AssetDatabase.Refresh();
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                if (ImGui.MenuItem("New Material"))
                {
                    var root = AssetDatabase.AssetsRoot;
                    var dirRel = ToRelativeDir(Path.GetDirectoryName(a.Path) ?? "", root);
                    _newKind = NewKind.Material; _newName = "NewMaterial";
                    _newTargetRel = dirRel; _newPopupJustOpened = true;
                }
                if (ImGui.MenuItem("New Skybox Material"))
                {
                    var root = AssetDatabase.AssetsRoot;
                    var dirRel = ToRelativeDir(Path.GetDirectoryName(a.Path) ?? "", root);
                    _newKind = NewKind.SkyboxMaterial; _newName = "NewSkyboxMaterial";
                    _newTargetRel = dirRel; _newPopupJustOpened = true;
                }
                if (ImGui.MenuItem("New Folder"))
                {
                    var root = AssetDatabase.AssetsRoot;
                    var dirRel = ToRelativeDir(Path.GetDirectoryName(a.Path) ?? "", root);
                    _newKind = NewKind.Folder; _newName = "NewFolder";
                    _newTargetRel = dirRel; _newPopupJustOpened = true;
                }
                if (ImGui.MenuItem("Rename"))
                    OpenRenameAsset(a.Guid, Path.GetFileNameWithoutExtension(a.Path));
                if (ImGui.MenuItem("Delete"))
                {
                    try { if (File.Exists(a.Path)) File.Delete(a.Path); } catch { }
                    _selAssets.Remove(a.Guid);
                    RefreshNow();
                    if (Selection.ActiveAssetGuid == a.Guid) Selection.ClearAsset();
                }
                ImGui.EndPopup();
            }

            ImGui.PopID();
        }

        // ===== List =====
        private static void DrawList(List<string> childDirs, List<AssetRecord> files)
        {
            ImGui.Columns(3, "ListCols", true);
            ImGui.SetColumnWidth(0, 280);
            ImGui.TextDisabled("Name"); ImGui.NextColumn();
            ImGui.TextDisabled("Type"); ImGui.NextColumn();
            ImGui.TextDisabled("Path"); ImGui.NextColumn();
            ImGui.Separator();

            // Folders
            foreach (var rel in childDirs)
            {
                ImGui.PushID($"row-folder::{rel}");
                bool selected = _selFolders.Contains(rel);

                // Taille icône liste (entre 16 et 24 px)
                float rowH = ImGui.GetTextLineHeightWithSpacing();
                float iconSz = MathF.Max(16f, MathF.Min(24f, _iconSize * 0.35f));

                // Bornes de la ligne pour sélection rectangulaire & DnD
                var rowStart = ImGui.GetCursorScreenPos();

                // Indenter le texte pour laisser la place à l’icône
                var savedCursor = ImGui.GetCursorPos();
                ImGui.SetCursorPos(new SysVec2(savedCursor.X + iconSz + 8f, savedCursor.Y));

                bool clicked = ImGui.Selectable(Path.GetFileName(rel), selected, ImGuiSelectableFlags.SpanAllColumns);

                // Restaurer (pas indispensable mais propre)
                ImGui.SetCursorPos(savedCursor);

                // Calcul de la fin de ligne
                var rowEnd = new SysVec2(
                    rowStart.X + ImGui.GetContentRegionAvail().X + ImGui.GetStyle().ScrollbarSize,
                    rowStart.Y + rowH
                );
                _itemBounds[KeyFolder(rel)] = (rowStart, rowEnd);
                _displayOrder.Add(KeyFolder(rel));

                // Dessin de l’icône à gauche
                var dl = ImGui.GetWindowDrawList();
                var iconTL = new SysVec2(rowStart.X + 4f, rowStart.Y + (rowH - iconSz) * 0.5f);
                var iconBR = new SysVec2(iconTL.X + iconSz, iconTL.Y + iconSz);
                var tex = IconManager.GetIconTexture("folder", (int)iconSz);
                if (tex != nint.Zero)
                    dl.AddImage(tex, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                else
                {
                    dl.AddRectFilled(iconTL, iconBR, 0xFF444444, 4);
                    dl.AddText(new SysVec2(iconTL.X + 3, iconTL.Y + 1), 0xFFFFFFFF, "[f]");
                }

                // Drop target (déplacer assets/dossiers dans ce dossier)
                if (ImGui.BeginDragDropTarget())
                {
                    if (TryAcceptAssetPayload(out var assetGuids))
                    {
                        MoveAssetsToFolder(assetGuids, rel);
                        RefreshNow();
                    }
                    if (TryAcceptFolderPayload(out var folderRels))
                    {
                        MoveFoldersToFolder(folderRels, rel);
                        RefreshNow();
                    }
                    ImGui.EndDragDropTarget();
                }

                if (clicked) HandleFolderClick(rel);
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                { _currentDir = rel; ClearSelection(); }

                ImGui.NextColumn(); ImGui.TextDisabled("Folder"); ImGui.NextColumn();
                ImGui.TextDisabled(rel); ImGui.NextColumn();
                ImGui.PopID();
            }

            // Files
            foreach (var a in files)
            {
                ImGui.PushID(a.Guid.ToString());
                bool selected = _selAssets.Contains(a.Guid);

                float rowH = ImGui.GetTextLineHeightWithSpacing();
                float iconSz = MathF.Max(16f, MathF.Min(24f, _iconSize * 0.35f));

                var rowStart = ImGui.GetCursorScreenPos();

                // Indenter le label pour laisser l’icône
                var savedCursor = ImGui.GetCursorPos();
                ImGui.SetCursorPos(new SysVec2(savedCursor.X + iconSz + 8f, savedCursor.Y));
                bool clicked = ImGui.Selectable(DisplayName(a), selected, ImGuiSelectableFlags.SpanAllColumns);
                ImGui.SetCursorPos(savedCursor);

                var rowEnd = new SysVec2(
                    rowStart.X + ImGui.GetContentRegionAvail().X + ImGui.GetStyle().ScrollbarSize,
                    rowStart.Y + rowH
                );
                _itemBounds[KeyAsset(a.Guid)] = (rowStart, rowEnd);
                _displayOrder.Add(KeyAsset(a.Guid));

                // Drag source (multi)
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, DragThreshold))
                    BeginDragFromCurrentSelection(originKey: KeyAsset(a.Guid), includeFolders: true, includeAssets: true);

                // Contexte
                if (ImGui.BeginPopupContextItem($"AssetCtx##{a.Guid}"))
                {
                    // Open C# Script option for .cs files
                    if (a.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ImGui.MenuItem("Open C# Script"))
                        {
                            State.EditorSettings.OpenScript(a.Path);
                        }
                        ImGui.Separator();
                    }
                    
                    if (ImGui.MenuItem("Reveal in Explorer")) RevealFile(a.Path);
                    if (ImGui.MenuItem("Rename")) OpenRenameAsset(a.Guid, Path.GetFileNameWithoutExtension(a.Path));
                    if (ImGui.MenuItem("Delete"))
                    {
                        try { if (File.Exists(a.Path)) File.Delete(a.Path); } catch { }
                        _selAssets.Remove(a.Guid);
                        RefreshNow();
                        if (Selection.ActiveAssetGuid == a.Guid) Selection.ClearAsset();
                    }
                    ImGui.EndPopup();
                }

                if (clicked) HandleAssetClick(a.Guid);
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                {
                    // If it's a C# script, open it in the external editor
                    if (a.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        State.EditorSettings.OpenScript(a.Path);
                    }
                    else
                    {
                        Selection.SetActiveAsset(a.Guid, a.Type);
                    }
                }

                // --- Icône / preview à gauche ---
                var dl = ImGui.GetWindowDrawList();
                var iconTL = new SysVec2(rowStart.X + 4f, rowStart.Y + (rowH - iconSz) * 0.5f);
                var iconBR = new SysVec2(iconTL.X + iconSz, iconTL.Y + iconSz);

                if (a.Type.Equals("Texture2D", StringComparison.OrdinalIgnoreCase))
                {
                    int handle = TextureCache.GetOrLoad(a.Guid, g => AssetDatabase.TryGet(g, out var rr) ? rr.Path : null);
                    if (handle != 0)
                    {
                        dl.AddImage((IntPtr)handle, iconTL, iconBR, new SysVec2(0, 1), new SysVec2(1, 0), 0xFFFFFFFF);
                    }
                    else
                    {
                        var t = IconManager.GetIconTexture("texture", (int)iconSz);
                        if (t != nint.Zero) dl.AddImage(t, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                        else dl.AddRectFilled(iconTL, iconBR, 0xFF444444, 4);
                    }
                }
                else if (a.Type.Equals("Material", StringComparison.OrdinalIgnoreCase))
                {
                    var t = IconManager.GetIconTexture("material", (int)iconSz);
                    if (t != nint.Zero) dl.AddImage(t, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                    else dl.AddRectFilled(iconTL, iconBR, 0xFF444444, 4);

                    // petite pastille d’albedo (optionnelle)
                    var col = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1f);
                    try
                    {
                        var mat = AssetDatabase.LoadMaterial(a.Guid);
                        if (mat?.AlbedoColor is { Length: >= 3 })
                            col = new(mat.AlbedoColor[0], mat.AlbedoColor[1], mat.AlbedoColor[2], 1f);
                    }
                    catch { }
                    var chip = new SysVec2(MathF.Min(10f, iconSz * 0.6f), MathF.Min(10f, iconSz * 0.6f));
                    var chipTL = new SysVec2(iconBR.X - chip.X + 1, iconBR.Y - chip.Y + 1);
                    var chipBR = new SysVec2(iconBR.X + 1, iconBR.Y + 1);
                    uint abgr = ImGui.ColorConvertFloat4ToU32(col);
                    dl.AddRectFilled(chipTL, chipBR, abgr, 3);
                    dl.AddRect(chipTL, chipBR, 0xAA000000, 3);
                }
                else
                {
                    var key = a.Type.Equals("Model", StringComparison.OrdinalIgnoreCase) ? "model" :
                              a.Type.Equals("Folder", StringComparison.OrdinalIgnoreCase) ? "folder" :
                              Path.GetExtension(a.Path).Equals(".scene", StringComparison.OrdinalIgnoreCase) ? "scene_file" :
                              "file"; // ajoute "file" plus tard si tu veux une icône générique
                    var t = IconManager.GetIconTexture(key, (int)iconSz);
                    if (t != nint.Zero) dl.AddImage(t, iconTL, iconBR, new SysVec2(0, 0), new SysVec2(1, 1), 0xFFFFFFFF);
                    else dl.AddRectFilled(iconTL, iconBR, 0xFF444444, 4);
                }

                ImGui.NextColumn(); ImGui.Text(a.Type); ImGui.NextColumn();
                ImGui.TextDisabled(RelPath(a.Path)); ImGui.NextColumn();
                ImGui.PopID();
            }

            ImGui.Columns(1);
        }

        // ===== Rectangle Selection =====
        private static void HandleSelectionRectangle()
        {
            var io = ImGui.GetIO();
            if (!ImGui.IsWindowHovered()) return;

            // démarrage : clic dans le vide sans modifieurs
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered() && !io.KeyCtrl && !io.KeyShift)
            {
                _isRectSelecting = true;
                _rectStart = io.MousePos;
                _rectEnd = io.MousePos;
                ClearSelection();
            }

            if (_isRectSelecting && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                _rectEnd = io.MousePos;
                DrawSelectionRect(_rectStart, _rectEnd);

                var (TL, BR) = GetRect(_rectStart, _rectEnd);
                foreach (var kv in _itemBounds)
                {
                    var (a, b) = kv.Value;
                    if (RectOverlap(TL, BR, a, b))
                    {
                        if (kv.Key.StartsWith("asset:"))
                        {
                            var guid = Guid.Parse(kv.Key.Substring(6));
                            _selAssets.Add(guid);
                        }
                        else if (kv.Key.StartsWith("folder:"))
                        {
                            var rel = kv.Key.Substring(7);
                            _selFolders.Add(rel);
                        }
                    }
                }
            }

            if (_isRectSelecting && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _isRectSelecting = false;
            }
        }

        private static void DrawSelectionRect(SysVec2 a, SysVec2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            var tl = new SysVec2(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
            var br = new SysVec2(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
            dl.AddRectFilled(tl, br, ImGui.GetColorU32(new System.Numerics.Vector4(0.2f, 0.6f, 1f, 0.20f)));
            dl.AddRect(tl, br, ImGui.GetColorU32(new System.Numerics.Vector4(0.2f, 0.6f, 1f, 0.90f)));
        }

        private static (SysVec2 TL, SysVec2 BR) GetRect(SysVec2 a, SysVec2 b)
            => (new SysVec2(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y)),
                new SysVec2(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y)));

        private static bool RectOverlap(SysVec2 atl, SysVec2 abr, SysVec2 btl, SysVec2 bbr)
            => !(abr.X < btl.X || atl.X > bbr.X || abr.Y < btl.Y || atl.Y > bbr.Y);

        // ===== Click handlers (CTRL/SHIFT/Range) =====
        private static void HandleAssetClick(Guid guid)
        {
            var io = ImGui.GetIO();
            bool ctrl = io.KeyCtrl;
            bool shift = io.KeyShift;
            string key = KeyAsset(guid);

            if (shift && !string.IsNullOrEmpty(_lastClickedKey))
            {
                RangeSelect(_lastClickedKey, key);
            }
            else if (ctrl)
            {
                if (_selAssets.Contains(guid)) _selAssets.Remove(guid);
                else _selAssets.Add(guid);
                _lastClickedKey = key;
            }
            else
            {
                ClearSelection();
                _selAssets.Add(guid);
                _lastClickedKey = key;
            }

            if (AssetDatabase.TryGet(guid, out var rec))
                Selection.SetActiveAsset(guid, rec.Type);
        }

        private static void HandleFolderClick(string relDir)
        {
            var io = ImGui.GetIO();
            bool ctrl = io.KeyCtrl;
            bool shift = io.KeyShift;
            string key = KeyFolder(relDir);

            if (shift && !string.IsNullOrEmpty(_lastClickedKey))
            {
                RangeSelect(_lastClickedKey, key);
            }
            else if (ctrl)
            {
                if (_selFolders.Contains(relDir)) _selFolders.Remove(relDir);
                else _selFolders.Add(relDir);
                _lastClickedKey = key;
            }
            else
            {
                ClearSelection();
                _selFolders.Add(relDir);
                _lastClickedKey = key;
            }
        }

        private static void RangeSelect(string keyA, string keyB)
        {
            if (_displayOrder.Count == 0) return;
            int i1 = _displayOrder.IndexOf(keyA);
            int i2 = _displayOrder.IndexOf(keyB);
            if (i1 < 0 || i2 < 0) return;
            if (i1 > i2) (i1, i2) = (i2, i1);

            _selAssets.Clear();
            _selFolders.Clear();

            for (int i = i1; i <= i2; i++)
            {
                string k = _displayOrder[i];
                if (k.StartsWith("asset:"))
                    _selAssets.Add(Guid.Parse(k.Substring(6)));
                else if (k.StartsWith("folder:"))
                    _selFolders.Add(k.Substring(7));
            }
        }

        private static void ClearSelection()
        {
            _selAssets.Clear();
            _selFolders.Clear();
            _lastClickedKey = "";
            Selection.ClearAsset();
        }

        // ===== DnD helpers =====
        // ===== DnD helpers =====
        private static void BeginDragFromCurrentSelection(string originKey, bool includeFolders, bool includeAssets)
        {
            var io = ImGui.GetIO();
            bool multiMod = io.KeyCtrl || io.KeyShift;

            // Construire un snapshot de la sélection courante
            var assets = new HashSet<Guid>(_selAssets);
            var folders = new HashSet<string>(_selFolders, StringComparer.OrdinalIgnoreCase);

            if (originKey.StartsWith("asset:"))
            {
                var originGuid = Guid.Parse(originKey.Substring(6));

                // Si l'origine n'est pas sélectionnée, ou si on a une sélection multiple sans modifieur,
                // on bascule la sélection locale pour que le visuel + payload portent sur l'origine.
                if (!assets.Contains(originGuid) || (!multiMod && assets.Count > 1))
                {
                    assets.Clear();
                    assets.Add(originGuid);

                    // Feedback visuel local SEULEMENT (ne pas toucher l'Inspector global)
                    _selAssets.Clear();
                    _selAssets.Add(originGuid);
                    _selFolders.Clear();
                    _lastClickedKey = originKey;
                    // (Surtout ne PAS appeler Selection.SetActiveAsset ici)
                }
            }
            else if (originKey.StartsWith("folder:"))
            {
                var originFolder = originKey.Substring(7);

                if (!folders.Contains(originFolder) || (!multiMod && folders.Count > 1))
                {
                    folders.Clear();
                    folders.Add(originFolder);

                    _selFolders.Clear();
                    _selFolders.Add(originFolder);
                    _selAssets.Clear();
                    _lastClickedKey = originKey;
                }
            }
            else
            {
                // Cas "aucune sélection" initiale : rien à faire ici
            }

            // Source DnD
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceAllowNullID))
            {
                if (includeAssets && assets.Count > 0)
                {
                    var bytes = assets.SelectMany(g => g.ToByteArray()).ToArray();
                    unsafe { fixed (byte* p = bytes) ImGui.SetDragDropPayload("ASSET_MULTI", (IntPtr)p, (uint)bytes.Length); }
                }
                if (includeFolders && folders.Count > 0)
                {
                    string joined = string.Join('\n', folders);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(joined);
                    unsafe { fixed (byte* p = bytes) ImGui.SetDragDropPayload("FOLDER_MULTI", (IntPtr)p, (uint)bytes.Length); }
                }
                ImGui.TextUnformatted($"{folders.Count + assets.Count} item(s)");
                ImGui.EndDragDropSource();
            }
        }

        private static unsafe bool TryAcceptAssetPayload(out List<Guid> guids)
        {
            guids = new List<Guid>();
            var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
            if (payload.NativePtr == null || payload.Data == IntPtr.Zero || payload.DataSize == 0) return false;

            int count = (int)payload.DataSize / 16;
            var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
            for (int i = 0; i < count; i++)
            {
                var g = new Guid(span.Slice(i * 16, 16));
                guids.Add(g);
            }
            return guids.Count > 0;
        }

        private static unsafe bool TryAcceptFolderPayload(out List<string> rels)
        {
            rels = new List<string>();
            var payload = ImGui.AcceptDragDropPayload("FOLDER_MULTI");
            if (payload.NativePtr == null || payload.Data == IntPtr.Zero || payload.DataSize == 0) return false;

            var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
            string utf8 = System.Text.Encoding.UTF8.GetString(span);
            foreach (var line in utf8.Split('\n'))
            {
                var rel = line.Trim();
                if (!string.IsNullOrEmpty(rel)) rels.Add(rel);
            }
            return rels.Count > 0;
        }

        private static void MoveAssetsToFolder(IEnumerable<Guid> guids, string targetRel)
        {
            string targetAbs = Path.Combine(AssetDatabase.AssetsRoot, targetRel);
            Directory.CreateDirectory(targetAbs);

            foreach (var g in guids)
            {
                if (!AssetDatabase.TryGet(g, out var rec)) continue;

                try
                {
                    SafeMoveAssetWithMeta(rec.Path, targetAbs);
                }
                catch
                {
                    // (log si besoin)
                }
            }
        }

        private static void MoveFoldersToFolder(IEnumerable<string> folderRels, string targetRel)
        {
            string targetAbs = Path.Combine(AssetDatabase.AssetsRoot, targetRel);
            Directory.CreateDirectory(targetAbs);

            foreach (var rel in folderRels)
            {
                // Interdictions : déplacer dans soi-même ou un descendant
                if (rel.Equals(targetRel, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsDescendant(rel, targetRel)) continue;

                string srcAbs = Path.Combine(AssetDatabase.AssetsRoot, rel);
                if (!Directory.Exists(srcAbs)) continue;

                string destAbs = Path.Combine(targetAbs, Path.GetFileName(srcAbs));
                if (destAbs.Equals(srcAbs, StringComparison.OrdinalIgnoreCase)) continue;

                // collision -> suffixe
                int i = 1;
                string finalAbs = destAbs;
                while (Directory.Exists(finalAbs))
                    finalAbs = destAbs + "_" + (i++).ToString();

                try { Directory.Move(srcAbs, finalAbs); } catch { /* log si besoin */ }
            }
        }

        private static bool IsDescendant(string ancestorRel, string candidateRel)
        {
            if (string.IsNullOrEmpty(ancestorRel)) return false;
            if (string.IsNullOrEmpty(candidateRel)) return false;
            if (candidateRel.StartsWith(ancestorRel + "/", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ===== Suppression =====
        private static void DeleteSelection()
        {
            // Dossiers d’abord (pour éviter de laisser traîner des fichiers orphelins)
            foreach (var rel in _selFolders.ToList())
                TryDeleteFolder(rel);

            // Puis fichiers
            foreach (var g in _selAssets.ToList())
            {
                if (AssetDatabase.TryGet(g, out var rec))
                {
                    try { if (File.Exists(rec.Path)) File.Delete(rec.Path); } catch { }
                }
                _selAssets.Remove(g);
            }

            RefreshNow();
            Selection.ClearAsset();
        }

        private static void TryDeleteFolder(string rel)
        {
            try
            {
                string abs = Path.Combine(AssetDatabase.AssetsRoot, rel);
                if (Directory.Exists(abs)) Directory.Delete(abs, recursive: true);
                _selFolders.Remove(rel);
            }
            catch { /* log si besoin */ }
        }

        // ===== Rendu cadres / helpers UI =====
        private static unsafe void DrawTileFrame(ImDrawListPtr dl, SysVec2 tl, SysVec2 br, bool hovered, bool selected, bool dropHovered)
        {
            float r = 8f;
            var baseCol = ImGui.GetStyleColorVec4(ImGuiCol.FrameBg);
            var hoverCol = ImGui.GetStyleColorVec4(ImGuiCol.FrameBgHovered);
            var selCol = ImGui.GetStyleColorVec4(ImGuiCol.ButtonActive);

            uint fill = ImGui.ColorConvertFloat4ToU32(*baseCol);
            if (hovered) fill = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(hoverCol->X, hoverCol->Y, hoverCol->Z, 0.65f));
            dl.AddRectFilled(tl, br, fill, r);

            // bordure
            uint border = hovered ? 0x55FFFFFFu : 0x33222222u;
            dl.AddRect(tl, br, border, r, ImDrawFlags.None, 1.0f);

            if (selected)
            {
                uint sel = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(selCol->X, selCol->Y, selCol->Z, 0.90f));
                dl.AddRect(tl, br, sel, r, ImDrawFlags.None, 2.0f);
            }

            if (dropHovered)
            {
                // halo bleu pour la cible de drop
                uint halo = ImGui.GetColorU32(new System.Numerics.Vector4(0.2f, 0.6f, 1f, 0.85f));
                dl.AddRect(tl, br, halo, r, ImDrawFlags.None, 3.0f);
            }
        }

        private static string FitTextEllipsis(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (ImGui.CalcTextSize(text).X <= maxWidth) return text;

            const string ell = "…";
            float ellW = ImGui.CalcTextSize(ell).X;
            int len = text.Length;
            while (len > 0 && ImGui.CalcTextSize(text.AsSpan(0, len)).X + ellW > maxWidth) len--;
            return (len <= 0) ? ell : text.Substring(0, len) + ell;
        }

        // ===== FS / Refresh =====
        private static void EnsureWatcher()
        {
            if (_watcher != null) return;

            var root = AssetDatabase.AssetsRoot;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            _watcher = new FileSystemWatcher(root)
            {
                Filter = "*",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            void OnFs(object? s, FileSystemEventArgs e)
            {
                _pendingRefresh = true;
                _lastFsEvent = DateTime.UtcNow;
            }
            void OnRen(object? s, RenamedEventArgs e) => OnFs(s, e);

            _watcher.Created += OnFs;
            _watcher.Changed += OnFs;
            _watcher.Deleted += OnFs;
            _watcher.Renamed += OnRen;

            _watcher.EnableRaisingEvents = true;
        }

        private static void RefreshNow()
        {
            try { AssetDatabase.Refresh(); } catch { }

            // _currentDir valide ?
            if (!string.IsNullOrEmpty(_currentDir))
            {
                string abs = Path.Combine(AssetDatabase.AssetsRoot, _currentDir);
                if (!Directory.Exists(abs))
                {
                    var parts = _currentDir.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                    _currentDir = string.Join("/", parts);
                    PruneOrphanMetas(abs);
                }
            }

            // purge sélections orphelines
            _selAssets.RemoveWhere(g => !AssetDatabase.TryGet(g, out _));
            _selFolders.RemoveWhere(rel => !Directory.Exists(Path.Combine(AssetDatabase.AssetsRoot, rel)));

            if (Selection.HasAsset && !AssetDatabase.TryGet(Selection.ActiveAssetGuid, out _))
                Selection.ClearAsset();
        }

        // ===== Helpers Dataset/UI =====
        private static IEnumerable<AssetRecord> FilterByDirectory(IEnumerable<AssetRecord> src, string relDir)
        {
            var root = AssetDatabase.AssetsRoot;

            if (string.IsNullOrEmpty(relDir))
            {
                return src.Where(a =>
                {
                    // Exclude backup files
                    if (Path.GetFileName(a.Path).Contains(".backup"))
                        return false;
                        
                    var dirAbs = Path.GetDirectoryName(a.Path) ?? "";
                    var dirRel = ToRelativeDir(dirAbs, root);
                    return string.IsNullOrEmpty(dirRel);
                });
            }

            return src.Where(a =>
            {
                // Exclude backup files
                if (Path.GetFileName(a.Path).Contains(".backup"))
                    return false;
                    
                var dirAbs = Path.GetDirectoryName(a.Path) ?? "";
                var dirRel = ToRelativeDir(dirAbs, root);
                return string.Equals(dirRel, relDir, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static List<AssetRecord> ApplySearch(IEnumerable<AssetRecord> src, string search)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(search)) return src.ToList();
                search = search.Trim();
                return src.Where(a =>
                    (a.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.Type?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    Path.GetFileNameWithoutExtension(a.Path).Contains(search, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            catch (InvalidOperationException)
            {
                // Collection was modified during enumeration - return empty list for this frame
                return new List<AssetRecord>();
            }
        }

        private static List<AssetRecord> SortAssets(List<AssetRecord> files, SortMode s)
        {
            return s switch
            {
                SortMode.NameAsc => files.OrderBy(a => DisplayName(a), StringComparer.OrdinalIgnoreCase).ToList(),
                SortMode.NameDesc => files.OrderByDescending(a => DisplayName(a), StringComparer.OrdinalIgnoreCase).ToList(),
                SortMode.TypeAsc => files.OrderBy(a => a.Type, StringComparer.OrdinalIgnoreCase)
                                         .ThenBy(a => DisplayName(a), StringComparer.OrdinalIgnoreCase).ToList(),
                SortMode.TypeDesc => files.OrderByDescending(a => a.Type, StringComparer.OrdinalIgnoreCase)
                                          .ThenBy(a => DisplayName(a), StringComparer.OrdinalIgnoreCase).ToList(),
                _ => files
            };
        }

        private static SortedSet<string> EnumerateAllDirsRelative(string root)
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "" };
            try
            {
                foreach (var abs in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                {
                    var rel = ToRelativeDir(abs, root);
                    if (!string.IsNullOrEmpty(rel)) set.Add(rel);
                }
            }
            catch { }
            return set;
        }

        private static List<string> ListChildDirsFs(string parentRel)
        {
            string abs = string.IsNullOrEmpty(parentRel)
                ? AssetDatabase.AssetsRoot
                : Path.Combine(AssetDatabase.AssetsRoot, parentRel);

            try
            {
                if (!Directory.Exists(abs)) return new List<string>();
                return Directory.EnumerateDirectories(abs, "*", SearchOption.TopDirectoryOnly)
                                .Select(d => ToRelativeDir(d, AssetDatabase.AssetsRoot))
                                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                                .ToList();
            }
            catch { return new List<string>(); }
        }

        private static string DisplayName(AssetRecord a)
            => string.IsNullOrWhiteSpace(a.Name)
               ? Path.GetFileNameWithoutExtension(a.Path)
               : a.Name;

        private static string RelPath(string abs)
        {
            try
            {
                var root = AssetDatabase.AssetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return abs.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    ? abs.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : abs;
            }
            catch { return abs; }
        }

        private static bool IsDirectChild(string parent, string candidate)
        {
            if (candidate == "") return false;
            if (parent == "") return candidate.IndexOf('/') < 0;
            if (!candidate.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase)) return false;
            var rest = candidate.Substring(parent.Length + 1);
            return !rest.Contains('/');
        }

        private static string ToRelativeDir(string absDir, string root)
        {
            if (string.IsNullOrEmpty(absDir)) return "";
            root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            absDir = absDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (absDir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var rel = absDir.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return rel.Replace('\\', '/');
            }
            return absDir.Replace('\\', '/');
        }

        private static void RevealFile(string path)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                else if (OperatingSystem.IsMacOS())
                    System.Diagnostics.Process.Start("open", $"-R \"{path}\"");
                else
                    System.Diagnostics.Process.Start("xdg-open", Path.GetDirectoryName(path)!);
            }
            catch { }
        }

        private static AssetDatabase.AssetRecord CreateMaterialInFolder(string name, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);

            var mat = new Engine.Assets.MaterialAsset
            {
                Guid = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? "Material" : name,
                AlbedoColor = new float[] { 1, 1, 1, 1 },
                Metallic = 0f,
                Roughness = 0.5f
            };

            var baseName = SanitizeFileName(mat.Name);
            var file = Path.Combine(targetFolder, baseName) + ".material";
            int i = 1;
            while (File.Exists(file))
                file = Path.Combine(targetFolder, $"{baseName}_{i++}") + ".material";

            Engine.Assets.MaterialAsset.Save(file, mat);
            return new AssetDatabase.AssetRecord(mat.Guid, file, "Material");
        }

        private static AssetDatabase.AssetRecord CreateSkyboxMaterialInFolder(string name, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);

            var skyboxMat = new Engine.Assets.SkyboxMaterialAsset
            {
                Guid = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? "SkyboxMaterial" : name,
                Type = Engine.Assets.SkyboxType.Procedural,
                SkyTint = new float[] { 0.5f, 0.5f, 0.5f, 1.0f },
                GroundColor = new float[] { 0.369f, 0.349f, 0.341f, 1.0f },
                Exposure = 1.3f,
                AtmosphereThickness = 1.0f
            };

            var baseName = SanitizeFileName(skyboxMat.Name);
            var file = Path.Combine(targetFolder, baseName) + ".skymat";
            int i = 1;
            while (File.Exists(file))
                file = Path.Combine(targetFolder, $"{baseName}_{i++}") + ".skymat";

            Engine.Assets.SkyboxMaterialAsset.Save(file, skyboxMat);
            return new AssetDatabase.AssetRecord(skyboxMat.Guid, file, "SkyboxMaterial");
        }

        private static void CreateNewFolder(string name, string basePath)
        {
            Directory.CreateDirectory(basePath);
            string sanitized = SanitizeFileName(string.IsNullOrWhiteSpace(name) ? "NewFolder" : name);
            string folderPath = Path.Combine(basePath, sanitized);

            int i = 1;
            while (Directory.Exists(folderPath))
                folderPath = Path.Combine(basePath, $"{sanitized}_{i++}");

            Directory.CreateDirectory(folderPath);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "Asset" : name.Trim();
        }

        // ===== Popups =====
        private static void DrawNewPopup()
        {
            if (_newPopupJustOpened && _newKind != NewKind.None)
            {
                ImGui.OpenPopup(NEW_POPUP_NAME);
                _newPopupJustOpened = false;
            }

            bool open = true;
            if (ImGui.BeginPopupModal(NEW_POPUP_NAME, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            {
                string title = _newKind switch
                {
                    NewKind.Folder => "Create Folder",
                    NewKind.Material => "Create Material",
                    NewKind.SkyboxMaterial => "Create Skybox Material",
                    _ => "Create"
                };
                ImGui.Text(title);
                ImGui.Separator();

                ImGui.SetNextItemWidth(280);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                ImGui.InputText("##newName", ref _newName, 128, ImGuiInputTextFlags.EnterReturnsTrue);

                bool create = ImGui.IsItemDeactivatedAfterEdit() || ImGui.Button("Create");
                ImGui.SameLine();
                bool cancel = ImGui.Button("Cancel");

                if (create)
                {
                    string relBase = string.IsNullOrEmpty(_newTargetRel) ? _currentDir : _newTargetRel;
                    string basePath = string.IsNullOrEmpty(relBase) ? AssetDatabase.AssetsRoot : Path.Combine(AssetDatabase.AssetsRoot, relBase);

                    if (_newKind == NewKind.Folder)
                    {
                        CreateNewFolder(_newName, basePath);
                        RefreshNow();
                    }
                    else if (_newKind == NewKind.Material)
                    {
                        var rec = CreateMaterialInFolder(_newName, basePath);
                        RefreshNow();
                        _selAssets.Clear(); _selFolders.Clear();
                        _selAssets.Add(rec.Guid); // highlight
                    }
                    else if (_newKind == NewKind.SkyboxMaterial)
                    {
                        var rec = CreateSkyboxMaterialInFolder(_newName, basePath);
                        RefreshNow();
                        _selAssets.Clear(); _selFolders.Clear();
                        _selAssets.Add(rec.Guid); // highlight
                    }

                    ImGui.CloseCurrentPopup();
                    _newKind = NewKind.None; _newName = "";
                }
                if (cancel)
                {
                    ImGui.CloseCurrentPopup();
                    _newKind = NewKind.None; _newName = "";
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawRenamePopup()
        {
            if (_renameJustOpened && _renameKind != RenameKind.None)
            {
                ImGui.OpenPopup(RENAME_POPUP_NAME);
                _renameJustOpened = false;
            }

            bool open = true;
            if (ImGui.BeginPopupModal(RENAME_POPUP_NAME, ref open, ImGuiWindowFlags.AlwaysAutoResize))
            {
                string title = _renameKind == RenameKind.Asset ? "Rename Asset" :
                               _renameKind == RenameKind.Folder ? "Rename Folder" : "Rename";
                ImGui.Text(title);
                ImGui.Separator();

                ImGui.SetNextItemWidth(320);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                ImGui.InputText("##renameName", ref _renameName, 256, ImGuiInputTextFlags.EnterReturnsTrue);

                bool doRename = ImGui.IsItemDeactivatedAfterEdit() || ImGui.Button("Rename");
                ImGui.SameLine();
                bool cancel = ImGui.Button("Cancel");

                if (doRename)
                {
                    try
                    {
                        if (_renameKind == RenameKind.Asset && _renameAssetGuid != Guid.Empty && AssetDatabase.TryGet(_renameAssetGuid, out var rec))
                        {
                            string newAbs = SafeRenameFile(rec.Path, _renameName);
                            RefreshNow();
                            _selAssets.Clear(); _selFolders.Clear();
                            _selAssets.Add(_renameAssetGuid);
                        }
                        else if (_renameKind == RenameKind.Folder && !string.IsNullOrEmpty(_renameFolderRel))
                        {
                            string oldAbs = Path.Combine(AssetDatabase.AssetsRoot, _renameFolderRel);
                            string newAbs = SafeRenameFolder(oldAbs, _renameName);
                            RefreshNow();
                            string newRel = ToRelativeDir(newAbs, AssetDatabase.AssetsRoot);
                            if (string.Equals(_currentDir, _renameFolderRel, StringComparison.OrdinalIgnoreCase))
                                _currentDir = newRel.Replace('\\', '/');
                            _selAssets.Clear(); _selFolders.Clear();
                            _selFolders.Add(newRel.Replace('\\', '/'));
                        }
                    }
                    catch { }

                    ImGui.CloseCurrentPopup();
                    _renameKind = RenameKind.None; _renameName = ""; _renameAssetGuid = Guid.Empty; _renameFolderRel = "";
                }
                if (cancel)
                {
                    ImGui.CloseCurrentPopup();
                    _renameKind = RenameKind.None; _renameName = ""; _renameAssetGuid = Guid.Empty; _renameFolderRel = "";
                }

                ImGui.EndPopup();
            }
        }

        private static string SafeRenameFile(string oldAbs, string newBaseName)
        {
            var dir = Path.GetDirectoryName(oldAbs)!;
            var ext = Path.GetExtension(oldAbs);
            string sanitized = SanitizeFileName(string.IsNullOrWhiteSpace(newBaseName)
                ? Path.GetFileNameWithoutExtension(oldAbs)
                : newBaseName);

            // Chemin final unique (évite d’écraser un autre asset + sa .meta)
            string targetAbs = EnsureUniquePath(dir, sanitized + ext);

            // Si le nom n’a pas changé -> rien à faire
            if (targetAbs.Equals(oldAbs, StringComparison.OrdinalIgnoreCase))
                return oldAbs;

            // Déplace le fichier + sa .meta (GUID préservé)
            SafeMoveOrCopyDelete(oldAbs, targetAbs);

            string oldMeta = oldAbs + ".meta";
            string newMeta = targetAbs + ".meta";
            if (File.Exists(oldMeta))
            {
                SafeMoveOrCopyDelete(oldMeta, newMeta);
            }

            return targetAbs;
        }

        /// <summary>
        /// Déplace un asset dans destDir en **préservant la .meta** et en résolvant les collisions
        /// (ex: foo.png -> foo_1.png si foo.png existe déjà ; la .meta est renommée pareil).
        /// Retourne le chemin final de l’asset déplacé.
        /// </summary>
        private static string SafeMoveAssetWithMeta(string srcAbs, string destDir)
        {
            Directory.CreateDirectory(destDir);

            string fileName = Path.GetFileName(srcAbs);
            string destAbs = EnsureUniquePath(destDir, fileName);

            // Déplace le fichier (copie+delete si nécessaire)
            SafeMoveOrCopyDelete(srcAbs, destAbs);

            // Déplace la meta si elle existe (préserve le GUID)
            string srcMeta = srcAbs + ".meta";
            string destMeta = destAbs + ".meta";
            if (File.Exists(srcMeta))
                SafeMoveOrCopyDelete(srcMeta, destMeta);

            return destAbs;
        }

        /// <summary>
        /// Assure un nom unique dans destDir pour fileName (évite conflit avec le fichier ET sa .meta).
        /// </summary>
        private static string EnsureUniquePath(string destDir, string fileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            string candidate = Path.Combine(destDir, fileName);

            int i = 1;
            while (File.Exists(candidate) || File.Exists(candidate + ".meta"))
            {
                candidate = Path.Combine(destDir, $"{baseName}_{i++}{ext}");
            }
            return candidate;
        }

        /// <summary>
        /// Tente File.Move(..., overwrite: true). Si ça échoue (ex: autre volume), fait Copy puis Delete.
        /// </summary>
        private static void SafeMoveOrCopyDelete(string srcAbs, string destAbs)
        {
            try
            {
                // .NET 6+ a une surcharge overwrite=true ; si indisponible remplace par Delete+Move
                if (File.Exists(destAbs)) File.Delete(destAbs);
                File.Move(srcAbs, destAbs);
            }
            catch
            {
                // Fallback cross-volume
                File.Copy(srcAbs, destAbs, overwrite: true);
                try { File.Delete(srcAbs); } catch { }
            }
        }

        private static void PruneOrphanMetas(string absDir)
        {
            if (!Directory.Exists(absDir)) return;
            foreach (var meta in Directory.EnumerateFiles(absDir, "*.meta", SearchOption.AllDirectories))
            {
                var asset = meta.Substring(0, meta.Length - 5);
                if (!File.Exists(asset))
                {
                    try { File.Delete(meta); } catch { }
                }
            }
        }

        private static string SafeRenameFolder(string oldAbs, string newName)
        {
            string parent = Path.GetDirectoryName(oldAbs)!;
            string sanitized = SanitizeFileName(string.IsNullOrWhiteSpace(newName) ? new DirectoryInfo(oldAbs).Name : newName);
            string target = Path.Combine(parent, sanitized);
            int i = 1;
            while (Directory.Exists(target) && !target.Equals(oldAbs, StringComparison.OrdinalIgnoreCase))
                target = Path.Combine(parent, $"{sanitized}_{i++}");

            if (!target.Equals(oldAbs, StringComparison.OrdinalIgnoreCase))
                Directory.Move(oldAbs, target);

            return target;
        }

        private static void OpenRenameAsset(Guid guid, string suggested)
        {
            _renameKind = RenameKind.Asset;
            _renameAssetGuid = guid;
            _renameFolderRel = "";
            _renameName = suggested;
            _renameJustOpened = true;
        }

        private static void OpenRenameFolder(string folderRel, string suggested)
        {
            _renameKind = RenameKind.Folder;
            _renameAssetGuid = Guid.Empty;
            _renameFolderRel = folderRel;
            _renameName = suggested;
            _renameJustOpened = true;
        }

        // ===== Utils =====
        private static bool BeginChildCompat(string id, SysVec2 size, bool border)
        {
            return ImGui.BeginChild(
                id,
                size,
                border ? ImGuiChildFlags.Borders : ImGuiChildFlags.None,
                ImGuiWindowFlags.None
            );
        }

        private static string KeyAsset(Guid g) => "asset:" + g.ToString();
        private static string KeyFolder(string rel) => "folder:" + rel;

        private static string SafeJoin(params string[] parts)
            => string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
