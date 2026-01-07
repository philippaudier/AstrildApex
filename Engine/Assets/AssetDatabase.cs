using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Engine.Assets
{
    public static class AssetDatabase
    {
        public const string MetaExt = ".meta";
        public const string MaterialExt = ".material";
    public const string SkyboxExt = ".skymat";
        public const string MeshAssetExt = ".meshasset";
        private static Guid _defaultWhiteMaterialGuid = Guid.Empty;

        static readonly Dictionary<Guid, AssetRecord> _byGuid = new();
        static readonly Dictionary<string, AssetRecord> _byPath = new(StringComparer.OrdinalIgnoreCase);
    // Guard against re-entrant SaveMaterial calls for the same GUID
    static readonly System.Collections.Generic.HashSet<Guid> _savingInProgress = new();
    // In-memory cache for recently loaded/saved materials to avoid read-after-write races
    static readonly Dictionary<Guid, MaterialAsset> _materialCache = new();
    static readonly object _materialCacheLock = new();


        public static string AssetsRoot { get; private set; } = "";
        static bool _initialized;

        public static void Initialize(string rootDir)
        {
            if (_initialized && string.Equals(AssetsRoot, rootDir, StringComparison.OrdinalIgnoreCase)) return;
            AssetsRoot = rootDir ?? throw new ArgumentNullException(nameof(rootDir));
            Directory.CreateDirectory(AssetsRoot);
            Refresh();
            _initialized = true;
        }

        public static void Refresh()
        {
            _byGuid.Clear();
            _byPath.Clear();
            // Clear material cache when refreshing index
            lock (_materialCacheLock)
            {
                _materialCache.Clear();
            }
            if (string.IsNullOrWhiteSpace(AssetsRoot) || !Directory.Exists(AssetsRoot)) return;

            // Matériaux (.material)  contiennent eux-mêmes leur GUID
            foreach (var f in Directory.EnumerateFiles(AssetsRoot, "*" + MaterialExt, SearchOption.AllDirectories))
            {
                try
                {
                    var mat = MaterialAsset.Load(f);
                    var rec = new AssetRecord(mat.Guid, f, "Material");
                    Index(rec);
                    EnsureMetaExists(rec);
                }
                catch (Exception)
                {
                }
            }

            // Skybox materials (.skymat) contain their GUID too
            foreach (var f in Directory.EnumerateFiles(AssetsRoot, "*" + SkyboxExt, SearchOption.AllDirectories))
            {
                try
                {
                    var sky = SkyboxMaterialAsset.Load(f);
                    var rec = new AssetRecord(sky.Guid, f, "SkyboxMaterial");
                    Index(rec);
                    EnsureMetaExists(rec);
                }
                catch (Exception)
                {
                }
            }

            // Mesh assets (.meshasset) contain their GUID too
            foreach (var f in Directory.EnumerateFiles(AssetsRoot, "*" + MeshAssetExt, SearchOption.AllDirectories))
            {
                try
                {
                    var mesh = MeshAsset.Load(f);
                    var rec = new AssetRecord(mesh.Guid, f, "MeshAsset");
                    Index(rec);
                    EnsureMetaExists(rec);
                }
                catch (Exception)
                {
                }
            }

            // Prefabs (.prefab) contain their GUID too
            foreach (var f in Directory.EnumerateFiles(AssetsRoot, "*.prefab", SearchOption.AllDirectories))
            {
                try
                {
                    var prefab = PrefabAsset.Load(f);
                    var rec = new AssetRecord(prefab.Guid, f, "Prefab");
                    Index(rec);
                    EnsureMetaExists(rec);
                }
                catch (Exception)
                {
                }
            }

            // Fichiers bruts (png/jpg/gltf/fbx/)  GUID via sidecar .meta
            foreach (var f in Directory.EnumerateFiles(AssetsRoot, "*.*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(MetaExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(MaterialExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(SkyboxExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(MeshAssetExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;

                var type = GuessTypeFromExtension(Path.GetExtension(f));
                var metaPath = f + MetaExt;

                Guid guid;
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var meta = JsonSerializer.Deserialize<MetaData>(File.ReadAllText(metaPath));
                        guid = meta?.guid ?? GenerateGuidFromPath(f);
                    }
                    catch { guid = GenerateGuidFromPath(f); }
                }
                else guid = GenerateGuidFromPath(f);

                var rec = new AssetRecord(guid, f, type);
                Index(rec);
                EnsureMetaExists(rec);
            }
            try { EnsureDefaultWhiteMaterial(); } catch { /* ignore */ }
            try
            {
                Engine.Utils.DebugLogger.Log($"[AssetDatabase] Indexed assets: {_byGuid.Count} entries, paths: {_byPath.Count}");
            }
            catch { }
        }

        static void Index(AssetRecord rec)
        {
            _byGuid[rec.Guid] = rec;
            _byPath[rec.Path] = rec;
        }

        static string GuessTypeFromExtension(string ext)
        {
            ext = (ext ?? "").ToLowerInvariant();
            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp" => "Texture2D",
                ".hdr" or ".exr" => "TextureHDR",
                ".gltf" or ".glb" => "ModelGLTF",
                ".fbx" => "ModelFBX",
                ".obj" => "ModelOBJ",
                ".dae" => "ModelDAE",
                ".meshasset" => "MeshAsset",
                ".skymat" => "SkyboxMaterial",
                ".prefab" => "Prefab",
                ".ttf" or ".otf" or ".woff" or ".woff2" => "TrueTypeFont",
                ".fontasset" => "FontAsset",
                _ => "File"
            };
        }

        static void EnsureMetaExists(AssetRecord rec)
        {
            var metaPath = rec.Path + MetaExt;
            try
            {
                // If the .meta file exists, merge guid/type into it instead of overwriting
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var existing = File.ReadAllText(metaPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(existing);
                        var dest = new System.Collections.Generic.Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            var name = prop.Name;
                            var el = prop.Value;
                            switch (el.ValueKind)
                            {
                                case System.Text.Json.JsonValueKind.True:
                                case System.Text.Json.JsonValueKind.False:
                                    dest[name] = el.GetBoolean(); break;
                                case System.Text.Json.JsonValueKind.Number:
                                    if (el.TryGetInt64(out var iv)) dest[name] = iv; else if (el.TryGetDouble(out var dv)) dest[name] = dv; else dest[name] = el.GetRawText();
                                    break;
                                case System.Text.Json.JsonValueKind.String:
                                    dest[name] = el.GetString(); break;
                                default:
                                    dest[name] = el.GetRawText(); break;
                            }
                        }

                        // Ensure GUID and type are correct
                        dest["guid"] = rec.Guid;
                        dest["type"] = rec.Type;

                        var json = JsonSerializer.Serialize(dest, new JsonSerializerOptions { WriteIndented = true });
                        // Avoid rewriting the meta file if the content is identical to prevent triggering FS watchers
                        if (!string.Equals(existing, json, StringComparison.Ordinal))
                        {
                            File.WriteAllText(metaPath, json);
                        }
                        return;
                    }
                    catch
                    {
                        // Fall through to write a fresh meta if parsing fails
                    }
                }

                var md = new MetaData { guid = rec.Guid, type = rec.Type };
                var jsonFresh = JsonSerializer.Serialize(md, new JsonSerializerOptions { WriteIndented = true });
                // If a meta file suddenly appeared and matches, avoid rewriting
                try
                {
                    if (File.Exists(metaPath))
                    {
                        var existing2 = File.ReadAllText(metaPath);
                        if (!string.Equals(existing2, jsonFresh, StringComparison.Ordinal))
                        {
                            File.WriteAllText(metaPath, jsonFresh);
                        }
                    }
                    else
                    {
                        File.WriteAllText(metaPath, jsonFresh);
                    }
                }
                catch { }
            }
            catch { }
        }

        /// <summary>
        /// Génère un GUID déterministe basé sur le chemin du fichier.
        /// Utilisé pour attribuer des GUID stables aux assets qui n'ont pas de .meta.
        /// </summary>
        private static Guid GenerateGuidFromPath(string filePath)
        {
            try
            {
                using var md5 = System.Security.Cryptography.MD5.Create();
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(filePath)));
                return new Guid(hash);
            }
            catch
            {
                return Guid.NewGuid();
            }
        }

        public static IEnumerable<AssetRecord> All() => _byGuid.Values.OrderBy(r => r.Type).ThenBy(r => r.Name);
        public static bool TryGet(Guid guid, out AssetRecord rec) => _byGuid.TryGetValue(guid, out rec!);
        public static bool TryGetByPath(string path, out AssetRecord rec) => _byPath.TryGetValue(path, out rec!);

        public static AssetRecord CreateMaterial(string name, string? folder = null)
        {
            folder ??= Path.Combine(AssetsRoot, "Materials");
            Directory.CreateDirectory(folder);

            var mat = new MaterialAsset
            {
                Guid = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? "Material" : name,
                Shader = "ForwardBase",
                AlbedoColor = new float[] {1,1,1,1},
                Metallic = 0f,
                Roughness = 0.5f
            };

            var baseName = Sanitize(mat.Name);
            var file = Path.Combine(folder, baseName) + MaterialExt;
            int i = 1;
            while (File.Exists(file))
                file = Path.Combine(folder, $"{baseName}_{i++}") + MaterialExt;

            MaterialAsset.Save(file, mat);
            var rec = new AssetRecord(mat.Guid, file, "Material");
            Index(rec);
            EnsureMetaExists(rec);
            return rec;
        }

        // Event fired when a material is saved
        public static event System.Action<System.Guid>? MaterialSaved;

        /// <summary>
        /// Asynchronously save a material to disk on a background thread and
        /// invoke the MaterialSaved event on the main thread via Engine.Utils.MainThreadInvoker.
        /// By default this method is non-destructive for the `Shader` field: when
        /// `overwriteShader` is false, the on-disk `Shader` value is preserved and only
        /// other fields are updated. Set `overwriteShader` to true to forcefully write
        /// the provided `mat.Shader` value (used by undo/redo and explicit shader changes).
        /// This does not block the caller.
        /// </summary>
        public static System.Threading.Tasks.Task SaveMaterialAsync(MaterialAsset mat, bool overwriteShader = false)
        {
            if (!TryGet(mat.Guid, out var rec)) throw new InvalidOperationException("Material not indexed");

            // Prevent re-entrant saves for same material GUID which can cause overwrite races
            if (!_savingInProgress.Add(mat.Guid))
            {
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] Skipping SaveMaterialAsync for {mat.Guid} because save already in progress"); } catch { }
                return System.Threading.Tasks.Task.CompletedTask;
            }

            return System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    try
                    {
                        var st = new System.Diagnostics.StackTrace();
                        var frame = st.GetFrame(1);
                        var method = frame?.GetMethod();
                        var caller = method != null ? $"{method.DeclaringType?.FullName}.{method.Name}" : "<unknown>";
                        try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] SaveMaterialAsync() called by {caller} for {mat.Guid} -> {rec.Path}"); } catch { }
                    }
                    catch { try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] SaveMaterialAsync() called for {mat.Guid} -> {rec.Path}"); } catch { } }

                    // Prepare material to save. If overwriteShader==false we preserve the
                    // currently-on-disk Shader value to avoid accidental clobbers.
                    MaterialAsset toWrite;
                    try
                    {
                        var disk = MaterialAsset.Load(rec.Path);
                        toWrite = AssetDatabaseHelpers.MergeMaterial(disk, mat, overwriteShader);
                    }
                    catch
                    {
                        // If loading existing file fails (new file or read error) fall back to mat
                        toWrite = mat;
                    }

                    // Save synchronously on background thread
                    MaterialAsset.SaveAtomic(rec.Path, toWrite);
                    try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] Material file written (async): {rec.Path}"); } catch { }

                    EnsureMetaExists(rec);

                    // Readback and update in-memory cache
                    try
                    {
                        var saved = MaterialAsset.Load(rec.Path);
                        lock (_materialCacheLock)
                        {
                            _materialCache[saved.Guid] = saved;
                        }
                    }
                        catch (Exception ex)
                        {
                            try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] Failed to readback saved material (async): {ex.Message}"); } catch { }
                        }

                    // Enqueue event invocation on main thread so subscribers can safely touch GL state
                    try
                    {
                        Engine.Utils.MainThreadInvoker.Enqueue(() =>
                        {
                            try
                            {
                                MaterialSaved?.Invoke(mat.Guid);
                                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] MaterialSaved event invoked (async) for {mat.Guid}"); } catch { }
                            }
                            catch { }
                            // DON'T clear global cache here - OnMaterialSaved handles cache update correctly
                        });
                    }
                    catch { }
                }
                finally
                {
                    _savingInProgress.Remove(mat.Guid);
                }
            });
        }

        public static void SaveMaterial(MaterialAsset mat, bool overwriteShader = false)
        {
            if (!TryGet(mat.Guid, out var rec)) throw new InvalidOperationException("Material not indexed");
            // Prevent re-entrant saves for same material GUID which can cause overwrite races
            if (!_savingInProgress.Add(mat.Guid))
            {
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] Skipping SaveMaterial for {mat.Guid} because save already in progress"); } catch { }
                return;
            }

            try
            {
                // Print the immediate caller to identify who is invoking SaveMaterial
                try
                {
                    var st = new System.Diagnostics.StackTrace();
                    var frame = st.GetFrame(1); // caller frame
                    var method = frame?.GetMethod();
                    var caller = method != null ? $"{method.DeclaringType?.FullName}.{method.Name}" : "<unknown>";
                    try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] SaveMaterial() called by {caller} for {mat.Guid} -> {rec.Path}"); } catch { }
                }
                catch { try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] SaveMaterial() called for {mat.Guid} -> {rec.Path}"); } catch { } }

                // Prepare material to save by merging only changed fields into the
                // on-disk material. This avoids overwriting user-intended fields with
                // stale in-memory values.
                MaterialAsset toWrite;
                try
                {
                    var disk = MaterialAsset.Load(rec.Path);
                    toWrite = AssetDatabaseHelpers.MergeMaterial(disk, mat, overwriteShader);
                }
                catch { toWrite = mat; }

                // Save synchronously - simple and reliable
                MaterialAsset.SaveAtomic(rec.Path, toWrite);
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] Material file written: {rec.Path}"); } catch { }

                EnsureMetaExists(rec);
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] EnsureMetaExists completed for {rec.Path + MetaExt}"); } catch { }

                // Read back the file to verify what was persisted (debug help)
                try
                {
                    var saved = MaterialAsset.Load(rec.Path);
                    try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] Saved material readback: Guid={saved.Guid}, Name={saved.Name}, Roughness={saved.Roughness}, Metallic={saved.Metallic}"); } catch { }
                    // Update in-memory cache to prefer this freshly-saved copy and avoid immediate disk read races
                    lock (_materialCacheLock)
                    {
                        _materialCache[saved.Guid] = saved;
                    }
                }
                catch (Exception ex)
                {
                    try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] Failed to readback saved material: {ex.Message}"); } catch { }
                }
                // Notify that material has been saved/modified
                MaterialSaved?.Invoke(mat.Guid);
                try { if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log($"[AssetDatabase] MaterialSaved event invoked for {mat.Guid}"); } catch { }

                // DON'T clear global cache here - OnMaterialSaved handles cache update correctly
            }
            finally
            {
                _savingInProgress.Remove(mat.Guid);
            }
        }

        public static MaterialAsset LoadMaterial(Guid guid)
        {
            if (!TryGet(guid, out var rec)) throw new FileNotFoundException($"Material {guid} not found.");
            // Prefer in-memory cache to avoid read-after-write races
            lock (_materialCacheLock)
            {
                if (_materialCache.TryGetValue(guid, out var cached))
                {
                    return cached;
                }
            }

            var loaded = MaterialAsset.Load(rec.Path);
            lock (_materialCacheLock)
            {
                _materialCache[guid] = loaded;
            }
            return loaded;
        }

        /// <summary>
        /// Update the in-memory material cache with a live material instance.
        /// Used during interactive editing to ensure all renderers see live changes
        /// without writing to disk on every slider movement.
        /// </summary>
        public static void UpdateMaterialCache(Guid guid, MaterialAsset material)
        {
            lock (_materialCacheLock)
            {
                _materialCache[guid] = material;
            }
        }

        /// <summary>
        /// Clear the in-memory material cache.
        /// This forces all subsequent LoadMaterial calls to read from disk.
        /// Call this when entering play mode or when you need to ensure fresh disk values.
        /// </summary>
        public static void ClearMaterialCache()
        {
            lock (_materialCacheLock)
            {
                _materialCache.Clear();
                Console.WriteLine("[AssetDatabase] Material cache cleared");
            }
        }

        /// <summary>
        /// CENTRALIZED: Clear ALL material caches - both AssetDatabase and MaterialRuntime.
        /// This ensures complete cache invalidation across the entire system.
        /// Use this when:
        /// - Changing shader on a material
        /// - Loading a new scene
        /// - Entering/exiting play mode
        /// - Refreshing all materials
        /// </summary>
        public static void ClearAllMaterialCaches()
        {
            // Clear AssetDatabase material cache (MaterialAsset data)
            lock (_materialCacheLock)
            {
                _materialCache.Clear();
                Console.WriteLine("[AssetDatabase] Asset material cache cleared");
            }

            // Clear MaterialRuntime cache (OpenGL handles + textures)
            try
            {
                Engine.Rendering.MaterialRuntime.ClearGlobalCache();
                Console.WriteLine("[AssetDatabase] Runtime material cache cleared");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetDatabase] Failed to clear runtime cache: {ex.Message}");
            }
        }

        /// <summary>
        /// CENTRALIZED: Invalidate a single material across ALL caches.
        /// Use this when a material changes and needs to be reloaded.
        /// </summary>
        public static void InvalidateMaterial(Guid guid)
        {
            // Remove from AssetDatabase cache
            lock (_materialCacheLock)
            {
                _materialCache.Remove(guid);
            }

            // Invalidate MaterialRuntime cache entry
            try
            {
                Engine.Rendering.MaterialRuntime.InvalidateCacheEntry(guid);
            }
            catch { }
        }

        // Prefab cache
        private static readonly Dictionary<Guid, PrefabAsset> _prefabCache = new();
        private static readonly object _prefabCacheLock = new();

        public static PrefabAsset LoadPrefab(Guid guid)
        {
            if (!TryGet(guid, out var rec)) throw new FileNotFoundException($"Prefab {guid} not found.");
            
            lock (_prefabCacheLock)
            {
                if (_prefabCache.TryGetValue(guid, out var cached))
                {
                    return cached;
                }
            }

            var loaded = PrefabAsset.Load(rec.Path);
            lock (_prefabCacheLock)
            {
                _prefabCache[guid] = loaded;
            }
            return loaded;
        }

        public static void SavePrefab(PrefabAsset prefab, string? customPath = null)
        {
            string path;
            if (customPath != null)
            {
                path = customPath;
            }
            else if (TryGet(prefab.Guid, out var rec))
            {
                path = rec.Path;
            }
            else
            {
                // Create new prefab file
                var fileName = Sanitize(prefab.Name ?? "Prefab") + ".prefab";
                var prefabFolder = Path.Combine(AssetsRoot, "Prefabs");
                Directory.CreateDirectory(prefabFolder);
                path = Path.Combine(prefabFolder, fileName);
            }

            PrefabAsset.SaveAtomic(path, prefab);
            
            // Update cache
            lock (_prefabCacheLock)
            {
                _prefabCache[prefab.Guid] = prefab;
            }
            
            // Refresh will discover the new asset
            Refresh();
        }

        public static string GetName(Guid guid) => TryGet(guid, out var r) ? r.Name : guid.ToString();
        public static string GetTypeName(Guid guid) => TryGet(guid, out var r) ? r.Type : "?";

        public static string Sanitize(string n)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return string.IsNullOrWhiteSpace(n) ? "Asset" : n.Trim();
        }

        public sealed record AssetRecord(Guid Guid, string Path, string Type)
        {
            public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        }

        public static Guid EnsureDefaultWhiteMaterial()
        {
            // Si on a déjà un GUID en cache et qu'il existe toujours, le retourner
            if (_defaultWhiteMaterialGuid != Guid.Empty && TryGet(_defaultWhiteMaterialGuid, out _))
                return _defaultWhiteMaterialGuid;

            // Chercher d'abord physiquement le fichier "Default White.material"
            string matFolder = Path.Combine(AssetsRoot, "Materials");
            string matPath = Path.Combine(matFolder, "Default White.material");

            if (File.Exists(matPath))
            {
                try
                {
                    var loadedMat = MaterialAsset.Load(matPath);
                    _defaultWhiteMaterialGuid = loadedMat.Guid;

                    // S'assurer qu'il est indexé
                    if (!TryGet(_defaultWhiteMaterialGuid, out _))
                    {
                        var loadedRec = new AssetRecord(loadedMat.Guid, matPath, "Material");
                        Index(loadedRec);
                        EnsureMetaExists(loadedRec);
                    }
                    return _defaultWhiteMaterialGuid;
                }
                catch { /* continue si échec de lecture */ }
            }

            // Chercher dans l'index un material déjà nommé exactement "Default White"
            foreach (var assetRec in _byGuid.Values)
            {
                if (string.Equals(assetRec.Type, "Material", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(assetRec.Name, "Default White", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultWhiteMaterialGuid = assetRec.Guid;
                    return _defaultWhiteMaterialGuid;
                }
            }

            // Aucun "Default White" trouvé - créer un nouveau UNIQUEMENT avec ce nom exact
            Directory.CreateDirectory(matFolder);

            var mat = new MaterialAsset
            {
                Guid = Guid.NewGuid(),
                Name = "Default White",
                Shader = "ForwardBase",
                AlbedoColor = new float[] { 1, 1, 1, 1 },
                AlbedoTexture = null,
                Metallic = 0f,
                Roughness = 0.5f
            };

            // Sauver directement avec le nom exact (pas de numérotation)
            MaterialAsset.Save(matPath, mat);

            var rec = new AssetRecord(mat.Guid, matPath, "Material");
            Index(rec);
            EnsureMetaExists(rec);

            _defaultWhiteMaterialGuid = mat.Guid;
            return _defaultWhiteMaterialGuid;
        }


        public static Guid CloneMaterial(Guid srcGuid, string? newName = null)
        {
            var src = LoadMaterial(srcGuid);
            if (src == null) throw new InvalidOperationException("Source material not found: " + srcGuid);

            var clone = new MaterialAsset
            {
                Name = string.IsNullOrWhiteSpace(newName) ? (src.Name + " (Instance)") : newName,
                AlbedoColor = src.AlbedoColor != null ? (float[])src.AlbedoColor.Clone() : new float[] { 1, 1, 1, 1 },
                AlbedoTexture = src.AlbedoTexture.GetValueOrDefault(Guid.Empty),
                NormalTexture = src.NormalTexture,
                NormalStrength = src.NormalStrength,
                Metallic = src.Metallic,
                Roughness = src.Roughness,
                TextureTiling = src.TextureTiling != null ? (float[])src.TextureTiling.Clone() : new float[] { 1, 1 },
                TextureOffset = src.TextureOffset != null ? (float[])src.TextureOffset.Clone() : new float[] { 0, 0 },
                Saturation = src.Saturation,
                Brightness = src.Brightness,
                Contrast = src.Contrast,
                Hue = src.Hue,
                Emission = src.Emission,
                TransparencyMode = src.TransparencyMode,
                Opacity = src.Opacity,
                UsePlanarReflection = src.UsePlanarReflection,
                WaterReflectionStrength = src.WaterReflectionStrength,
                Shader = src.Shader
            };

            var rec = CreateMaterial(clone.Name);   // crée l'asset, enregistre et renvoie l'enregistrement
            clone.Guid = rec.Guid; SaveMaterial(clone);         // persiste les champs (si ta SaveMaterial a une surcharge (Guid, asset))

            return rec.Guid;
        }

        public static bool TryGetMaterialName(Guid guid, out string? name)
        {
            name = null;
            if (TryGet(guid, out var rec))
            {
                name = rec.Name;
                return true;
            }
            return false;
        }

        // Mesh asset methods
        public static MeshAsset? LoadMeshAsset(Guid guid)
        {
            if (!TryGet(guid, out var rec))
                return null;

            try
            {
                MeshAsset? meshAsset = null;
                string meshAssetPath = "";

                // Determine the .meshasset path
                if (rec.Path.EndsWith(MeshAssetExt, StringComparison.OrdinalIgnoreCase))
                {
                    meshAssetPath = rec.Path;
                }
                else
                {
                    meshAssetPath = rec.Path + MeshAssetExt;
                }

                // Try to load from binary cache first
                if (TryLoadMeshFromCache(guid, meshAssetPath, out meshAsset))
                {
                    return meshAsset;
                }

                // Cache miss - load from JSON
                if (rec.Path.EndsWith(MeshAssetExt, StringComparison.OrdinalIgnoreCase))
                {
                    meshAsset = MeshAsset.Load(rec.Path);
                }
                else
                {
                    // If it's a source model file, look for the corresponding .meshasset
                    // First check if the expected .meshasset exists
                    Console.WriteLine($"[AssetDatabase] Looking for .meshasset: {meshAssetPath} (exists: {File.Exists(meshAssetPath)})");
                    
                    if (!File.Exists(meshAssetPath))
                    {
                        // .meshasset not found at expected path, check if there's another .meshasset in the same folder
                        // This can happen when a model file is renamed but the .meshasset keeps the old name
                        var modelDir = Path.GetDirectoryName(rec.Path);
                        Console.WriteLine($"[AssetDatabase] .meshasset not found, checking folder: {modelDir}");
                        
                        if (!string.IsNullOrEmpty(modelDir))
                        {
                            var meshAssetFiles = Directory.GetFiles(modelDir, "*.meshasset");
                            Console.WriteLine($"[AssetDatabase] Found {meshAssetFiles.Length} .meshasset file(s) in folder");
                            
                            if (meshAssetFiles.Length == 1)
                            {
                                // Found exactly one .meshasset in the folder, assume it's for this model
                                var oldMeshAssetPath = meshAssetFiles[0];
                                Console.WriteLine($"[AssetDatabase] Found orphaned .meshasset: {Path.GetFileName(oldMeshAssetPath)}");
                                Console.WriteLine($"[AssetDatabase] Renaming to match source: {Path.GetFileName(meshAssetPath)}");
                                
                                try
                                {
                                    // Also rename the .meta file if it exists
                                    var oldMetaPath = oldMeshAssetPath + MetaExt;
                                    var newMetaPath = meshAssetPath + MetaExt;
                                    
                                    File.Move(oldMeshAssetPath, meshAssetPath);
                                    Console.WriteLine($"[AssetDatabase] Successfully renamed .meshasset");
                                    
                                    if (File.Exists(oldMetaPath))
                                    {
                                        File.Move(oldMetaPath, newMetaPath);
                                        Console.WriteLine($"[AssetDatabase] Successfully renamed .meshasset.meta");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[AssetDatabase] Failed to rename .meshasset: {ex.Message}");
                                }
                            }
                            else if (meshAssetFiles.Length > 1)
                            {
                                Console.WriteLine($"[AssetDatabase] Warning: Multiple .meshasset files found in {modelDir}, cannot auto-rename");
                            }
                            else
                            {
                                Console.WriteLine($"[AssetDatabase] No .meshasset files found in folder");
                            }
                        }
                    }
                    
                    if (File.Exists(meshAssetPath))
                    {
                        meshAsset = MeshAsset.Load(meshAssetPath);
                        
                        // Check if the SourcePath in the .meshasset is outdated (file was renamed)
                        if (meshAsset != null && !string.IsNullOrEmpty(meshAsset.SourcePath))
                        {
                            string? resolvedSourcePath = null;
                            if (!Path.IsPathRooted(meshAsset.SourcePath) && !meshAsset.SourcePath.Contains('/') && !meshAsset.SourcePath.Contains('\\'))
                            {
                                var meshAssetDir = Path.GetDirectoryName(meshAssetPath);
                                if (!string.IsNullOrEmpty(meshAssetDir))
                                {
                                    resolvedSourcePath = Path.Combine(meshAssetDir, meshAsset.SourcePath);
                                }
                            }
                            else
                            {
                                resolvedSourcePath = meshAsset.SourcePath;
                            }
                            
                            // If the old source file doesn't exist but our current file does, update the SourcePath
                            if (!string.IsNullOrEmpty(resolvedSourcePath) && !File.Exists(resolvedSourcePath) && File.Exists(rec.Path))
                            {
                                Console.WriteLine($"[AssetDatabase] Source file was renamed: {meshAsset.SourcePath} → {Path.GetFileName(rec.Path)}");
                                meshAsset.SourcePath = Path.GetFileName(rec.Path);
                                // Save the updated .meshasset
                                MeshAsset.Save(meshAssetPath, meshAsset);
                                Console.WriteLine($"[AssetDatabase] Updated .meshasset with new source path");
                            }
                        }
                    }
                    else
                    {
                        // .meshasset doesn't exist, try to generate it on-the-fly
                            try
                            {
                                Engine.Utils.DebugLogger.Log($"[AssetDatabase] .meshasset not found, processing model on-the-fly: {rec.Path}");
                                Engine.Utils.DebugLogger.Log($"[AssetDatabase] Checking meshAssetPath: {meshAssetPath} exists={File.Exists(meshAssetPath)}");
                                ModelImporter.ProcessExistingModel(rec.Path, guid);

                                // Try loading again
                                if (File.Exists(meshAssetPath))
                                {
                                    Engine.Utils.DebugLogger.Log($"[AssetDatabase] meshasset generated, loading: {meshAssetPath}");
                                    meshAsset = MeshAsset.Load(meshAssetPath);
                                }
                                else
                                {
                                    Engine.Utils.DebugLogger.Log($"[AssetDatabase] meshasset still missing after processing: {meshAssetPath}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Engine.Utils.DebugLogger.Log($"[AssetDatabase] Exception while processing model {rec.Path}: {ex.Message}");
                            }
                    }
                }

                // Fix bounding box if it's invalid (empty from deserialization)
                if (meshAsset != null && IsBoundingBoxInvalid(meshAsset.Bounds))
                {
                    meshAsset.Bounds = RecalculateBounds(meshAsset);
                }

                // Save to binary cache for next time
                if (meshAsset != null)
                {
                    SaveMeshToCache(guid, meshAssetPath, meshAsset);
                }

                return meshAsset;
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[AssetDatabase] Failed to load mesh asset {guid}: {ex.Message}");
                return null;
            }
        }

        private static bool IsBoundingBoxInvalid(BoundingBox bounds)
        {
            // Check if bounds are empty/invalid (all zeros from failed deserialization)
            return bounds.Min.X == 0 && bounds.Min.Y == 0 && bounds.Min.Z == 0 &&
                   bounds.Max.X == 0 && bounds.Max.Y == 0 && bounds.Max.Z == 0;
        }

        private static BoundingBox RecalculateBounds(MeshAsset meshAsset)
        {
            var bounds = new BoundingBox
            {
                Min = new System.Numerics.Vector3(float.MaxValue),
                Max = new System.Numerics.Vector3(float.MinValue)
            };

            foreach (var subMesh in meshAsset.SubMeshes)
            {
                // Extract positions from interleaved data
                for (int i = 0; i < subMesh.Vertices.Length; i += 8)
                {
                    var pos = new System.Numerics.Vector3(
                        subMesh.Vertices[i + 0],
                        subMesh.Vertices[i + 1],
                        subMesh.Vertices[i + 2]
                    );
                    bounds.Encapsulate(pos);
                }
            }

            return bounds;
        }

        private static bool TryLoadMeshFromCache(Guid guid, string meshAssetPath, out MeshAsset? meshAsset)
        {
            meshAsset = null;

            try
            {
                var cachePath = GetMeshCachePath(guid, meshAssetPath);
                if (!File.Exists(cachePath))
                {
                    Console.WriteLine($"[AssetDatabase] Cache not found for {Path.GetFileName(meshAssetPath)} (expected: {Path.GetFileName(cachePath)})");
                    return false;
                }

                // Check if source .meshasset is newer than cache
                if (File.Exists(meshAssetPath))
                {
                    var sourceTime = File.GetLastWriteTimeUtc(meshAssetPath);
                    var cacheTime = File.GetLastWriteTimeUtc(cachePath);
                    if (sourceTime > cacheTime)
                    {
                        // Source is newer, cache is stale
                        Console.WriteLine($"[AssetDatabase] Cache stale for {Path.GetFileName(meshAssetPath)}: source={sourceTime:HH:mm:ss}, cache={cacheTime:HH:mm:ss}");
                        return false;
                    }
                }
                else
                {
                    // .meshasset doesn't exist, cache is invalid
                    Console.WriteLine($"[AssetDatabase] Cache invalid: .meshasset not found at {meshAssetPath}");
                    File.Delete(cachePath);
                    return false;
                }

                using (var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fs))
                {
                    // Verify magic number and version
                    string magic = new string(reader.ReadChars(4));
                    if (magic != "MESH")
                    {
                        Console.WriteLine($"[AssetDatabase] Cache invalid magic for {Path.GetFileName(cachePath)}");
                        return false;
                    }

                    int version = reader.ReadInt32();
                    if (version != 1)
                        return false;

                    // Read metadata
                    meshAsset = new MeshAsset();
                    meshAsset.Guid = new Guid(reader.ReadBytes(16));
                    meshAsset.Name = reader.ReadString();
                    meshAsset.SourcePath = reader.ReadString();
                    
                    // Verify that the source file still exists (in case it was renamed/moved)
                    if (!string.IsNullOrEmpty(meshAsset.SourcePath))
                    {
                        // Try to resolve the source path (could be relative to .meshasset or just a filename)
                        string? resolvedSourcePath = null;
                        
                        // If it's just a filename, look in the same directory as the .meshasset
                        if (!Path.IsPathRooted(meshAsset.SourcePath) && !meshAsset.SourcePath.Contains('/') && !meshAsset.SourcePath.Contains('\\'))
                        {
                            var meshAssetDir = Path.GetDirectoryName(meshAssetPath);
                            if (!string.IsNullOrEmpty(meshAssetDir))
                            {
                                resolvedSourcePath = Path.Combine(meshAssetDir, meshAsset.SourcePath);
                            }
                        }
                        else
                        {
                            resolvedSourcePath = meshAsset.SourcePath;
                        }
                        
                        // Check if the resolved source file exists
                        if (!string.IsNullOrEmpty(resolvedSourcePath) && !File.Exists(resolvedSourcePath))
                        {
                            Console.WriteLine($"[AssetDatabase] Cache invalid: source file not found at {resolvedSourcePath}");
                            Console.WriteLine($"[AssetDatabase] Deleting stale cache: {Path.GetFileName(cachePath)}");
                            File.Delete(cachePath);
                            return false;
                        }
                    }
                    
                    meshAsset.TotalVertexCount = reader.ReadInt32();
                    meshAsset.TotalTriangleCount = reader.ReadInt32();

                    // Read bounding box
                    meshAsset.Bounds = new BoundingBox
                    {
                        Min = new System.Numerics.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        Max = new System.Numerics.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                    };

                    // Read material GUIDs
                    int materialCount = reader.ReadInt32();
                    meshAsset.MaterialGuids = new List<Guid?>(materialCount);
                    for (int i = 0; i < materialCount; i++)
                    {
                        bool hasGuid = reader.ReadBoolean();
                        if (hasGuid)
                            meshAsset.MaterialGuids.Add(new Guid(reader.ReadBytes(16)));
                        else
                            meshAsset.MaterialGuids.Add(null);
                    }

                    // Read submeshes
                    int submeshCount = reader.ReadInt32();
                    meshAsset.SubMeshes = new List<SubMesh>(submeshCount);
                    for (int i = 0; i < submeshCount; i++)
                    {
                        var subMesh = new SubMesh();
                        subMesh.Name = reader.ReadString();
                        subMesh.MaterialIndex = reader.ReadInt32();

                        // Read vertices
                        int vertexCount = reader.ReadInt32();
                        subMesh.Vertices = new float[vertexCount];
                        for (int v = 0; v < vertexCount; v++)
                            subMesh.Vertices[v] = reader.ReadSingle();

                        // Read indices
                        int indexCount = reader.ReadInt32();
                        subMesh.Indices = new uint[indexCount];
                        for (int idx = 0; idx < indexCount; idx++)
                            subMesh.Indices[idx] = reader.ReadUInt32();

                        meshAsset.SubMeshes.Add(subMesh);
                    }
                }

                // Log removed to reduce verbosity
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetDatabase] Cache read failed: {ex.Message}");
                meshAsset = null;
                return false;
            }
        }

        private static void SaveMeshToCache(Guid guid, string meshAssetPath, MeshAsset meshAsset)
        {
            try
            {
                var cachePath = GetMeshCachePath(guid, meshAssetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

                using (var fs = new FileStream(cachePath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs))
                {
                    // Write magic number and version
                    writer.Write("MESH".ToCharArray());
                    writer.Write(1); // version

                    // Write metadata
                    writer.Write(meshAsset.Guid.ToByteArray());
                    writer.Write(meshAsset.Name ?? "");
                    writer.Write(meshAsset.SourcePath ?? "");
                    writer.Write(meshAsset.TotalVertexCount);
                    writer.Write(meshAsset.TotalTriangleCount);

                    // Write bounding box
                    writer.Write(meshAsset.Bounds.Min.X);
                    writer.Write(meshAsset.Bounds.Min.Y);
                    writer.Write(meshAsset.Bounds.Min.Z);
                    writer.Write(meshAsset.Bounds.Max.X);
                    writer.Write(meshAsset.Bounds.Max.Y);
                    writer.Write(meshAsset.Bounds.Max.Z);

                    // Write material GUIDs
                    writer.Write(meshAsset.MaterialGuids.Count);
                    foreach (var matGuid in meshAsset.MaterialGuids)
                    {
                        writer.Write(matGuid.HasValue);
                        if (matGuid.HasValue)
                            writer.Write(matGuid.Value.ToByteArray());
                    }

                    // Write submeshes
                    writer.Write(meshAsset.SubMeshes.Count);
                    foreach (var subMesh in meshAsset.SubMeshes)
                    {
                        writer.Write(subMesh.Name ?? "");
                        writer.Write(subMesh.MaterialIndex);

                        // Write vertices
                        writer.Write(subMesh.Vertices.Length);
                        foreach (var v in subMesh.Vertices)
                            writer.Write(v);

                        // Write indices
                        writer.Write(subMesh.Indices.Length);
                        foreach (var idx in subMesh.Indices)
                            writer.Write(idx);
                    }
                }

                Console.WriteLine($"[AssetDatabase] 💾 Saved mesh to cache: {Path.GetFileName(cachePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssetDatabase] Cache write failed: {ex.Message}");
            }
        }

        private static string GetMeshCachePath(Guid guid, string meshAssetPath)
        {
            string cacheDir = Path.Combine("Cache", "Meshes");
            Directory.CreateDirectory(cacheDir);

            // Create STABLE hash from GUID and file modification time
            // IMPORTANT: Don't use GetHashCode() - it's NOT stable across process runs!
            string key = guid.ToString("N");
            if (File.Exists(meshAssetPath))
            {
                var modTime = File.GetLastWriteTimeUtc(meshAssetPath);
                key += "_" + modTime.Ticks.ToString("X");
            }

            // Use SHA256 for a stable hash (like Terrain cache does)
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
            var hash = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8);

            return Path.Combine(cacheDir, $"mesh_{hash}.cache");
        }

        public static bool IsMeshAsset(Guid guid)
        {
            if (!TryGet(guid, out var rec))
                return false;

            return rec.Type.StartsWith("Model", StringComparison.OrdinalIgnoreCase) ||
                   rec.Type.Equals("MeshAsset", StringComparison.OrdinalIgnoreCase);
        }

        public sealed class MetaData
        {
            public Guid guid { get; set; }
            public string? type { get; set; }
        }
    }
}

// ------------------ Merge helper for non-destructive material saves ------------------
namespace Engine.Assets
{
    public static class AssetDatabaseHelpers
    {
        // Merge incoming -> disk: start from disk state and copy only fields that differ
        // from incoming. If overwriteShader is true, use incoming.Shader; otherwise keep disk.Shader.
        public static MaterialAsset MergeMaterial(MaterialAsset disk, MaterialAsset incoming, bool overwriteShader)
        {
            // Start from disk copy
            var merged = new MaterialAsset
            {
                Guid = disk.Guid,
                Name = disk.Name,
            Shader = disk.Shader,
                AlbedoTexture = disk.AlbedoTexture,
                AlbedoColor = disk.AlbedoColor != null ? (float[])disk.AlbedoColor.Clone() : new float[] {1,1,1,1},
                NormalTexture = disk.NormalTexture,
                NormalStrength = disk.NormalStrength,
                MetallicTexture = disk.MetallicTexture,
                RoughnessTexture = disk.RoughnessTexture,
                MetallicRoughnessTexture = disk.MetallicRoughnessTexture,
                OcclusionTexture = disk.OcclusionTexture,
                OcclusionStrength = disk.OcclusionStrength,
                EmissiveTexture = disk.EmissiveTexture,
                EmissiveColor = disk.EmissiveColor != null ? (float[])disk.EmissiveColor.Clone() : new float[] {1f,1f,1f},
                HeightTexture = disk.HeightTexture,
                HeightScale = disk.HeightScale,
                DetailMaskTexture = disk.DetailMaskTexture,
                DetailAlbedoTexture = disk.DetailAlbedoTexture,
                DetailNormalTexture = disk.DetailNormalTexture,
                Metallic = disk.Metallic,
                Roughness = disk.Roughness,
                TextureTiling = disk.TextureTiling != null ? (float[])disk.TextureTiling.Clone() : new float[] {1f,1f},
                TextureOffset = disk.TextureOffset != null ? (float[])disk.TextureOffset.Clone() : new float[] {0f,0f},
                UseTriplanar = disk.UseTriplanar,
                TriplanarScale = disk.TriplanarScale,
                TriplanarBlendSharpness = disk.TriplanarBlendSharpness,
                TransparencyMode = disk.TransparencyMode,
                Opacity = disk.Opacity,
                CullingMode = disk.CullingMode,
                AlphaClippingEnabled = disk.AlphaClippingEnabled,
                AlphaClipThreshold = disk.AlphaClipThreshold,
                Saturation = disk.Saturation,
                Brightness = disk.Brightness,
                Contrast = disk.Contrast,
                Hue = disk.Hue,
                Emission = disk.Emission,
                GlassProperties = disk.GlassProperties
                ,UsePlanarReflection = disk.UsePlanarReflection
                ,WaterReflectionStrength = disk.WaterReflectionStrength
                ,WaterProperties = disk.WaterProperties
                ,VegetationProperties = disk.VegetationProperties
            };

            // Overwrite simple fields when incoming differs from disk
            if (!string.Equals(incoming.Name, disk.Name, StringComparison.Ordinal)) merged.Name = incoming.Name;
            if (overwriteShader) merged.Shader = incoming.Shader;

            // Note: TerrainLayers are intentionally ignored here (legacy property).

            if (incoming.AlbedoTexture != disk.AlbedoTexture) merged.AlbedoTexture = incoming.AlbedoTexture;
            if (!ArrayEquals(incoming.AlbedoColor, disk.AlbedoColor)) merged.AlbedoColor = (float[])incoming.AlbedoColor.Clone();

            if (incoming.NormalTexture != disk.NormalTexture) merged.NormalTexture = incoming.NormalTexture;
            if (Math.Abs(incoming.NormalStrength - disk.NormalStrength) > 1e-6f) merged.NormalStrength = incoming.NormalStrength;

            if (incoming.MetallicTexture != disk.MetallicTexture) merged.MetallicTexture = incoming.MetallicTexture;
            if (incoming.RoughnessTexture != disk.RoughnessTexture) merged.RoughnessTexture = incoming.RoughnessTexture;
            if (incoming.MetallicRoughnessTexture != disk.MetallicRoughnessTexture) merged.MetallicRoughnessTexture = incoming.MetallicRoughnessTexture;

            if (incoming.OcclusionTexture != disk.OcclusionTexture) merged.OcclusionTexture = incoming.OcclusionTexture;
            if (Math.Abs(incoming.OcclusionStrength - disk.OcclusionStrength) > 1e-6f) merged.OcclusionStrength = incoming.OcclusionStrength;

            if (incoming.EmissiveTexture != disk.EmissiveTexture) merged.EmissiveTexture = incoming.EmissiveTexture;
            if (!ArrayEquals(incoming.EmissiveColor, disk.EmissiveColor)) merged.EmissiveColor = (float[])incoming.EmissiveColor.Clone();

            if (incoming.HeightTexture != disk.HeightTexture) merged.HeightTexture = incoming.HeightTexture;
            if (Math.Abs(incoming.HeightScale - disk.HeightScale) > 1e-6f) merged.HeightScale = incoming.HeightScale;

            if (incoming.DetailMaskTexture != disk.DetailMaskTexture) merged.DetailMaskTexture = incoming.DetailMaskTexture;
            if (incoming.DetailAlbedoTexture != disk.DetailAlbedoTexture) merged.DetailAlbedoTexture = incoming.DetailAlbedoTexture;
            if (incoming.DetailNormalTexture != disk.DetailNormalTexture) merged.DetailNormalTexture = incoming.DetailNormalTexture;

            if (Math.Abs(incoming.Metallic - disk.Metallic) > 1e-6f) merged.Metallic = incoming.Metallic;
            if (Math.Abs(incoming.Roughness - disk.Roughness) > 1e-6f) merged.Roughness = incoming.Roughness;

            if (!ArrayEquals(incoming.TextureTiling, disk.TextureTiling)) merged.TextureTiling = (float[])incoming.TextureTiling.Clone();
            if (!ArrayEquals(incoming.TextureOffset, disk.TextureOffset)) merged.TextureOffset = (float[])incoming.TextureOffset.Clone();

            if (incoming.UseTriplanar != disk.UseTriplanar) merged.UseTriplanar = incoming.UseTriplanar;
            if (Math.Abs(incoming.TriplanarScale - disk.TriplanarScale) > 1e-6f) merged.TriplanarScale = incoming.TriplanarScale;
            if (Math.Abs(incoming.TriplanarBlendSharpness - disk.TriplanarBlendSharpness) > 1e-6f) merged.TriplanarBlendSharpness = incoming.TriplanarBlendSharpness;

            if (incoming.TransparencyMode != disk.TransparencyMode) merged.TransparencyMode = incoming.TransparencyMode;
            if (Math.Abs(incoming.Opacity - disk.Opacity) > 1e-6f) merged.Opacity = incoming.Opacity;

            if (incoming.CullingMode != disk.CullingMode) merged.CullingMode = incoming.CullingMode;
            if (incoming.AlphaClippingEnabled != disk.AlphaClippingEnabled) merged.AlphaClippingEnabled = incoming.AlphaClippingEnabled;
            if (Math.Abs(incoming.AlphaClipThreshold - disk.AlphaClipThreshold) > 1e-6f) merged.AlphaClipThreshold = incoming.AlphaClipThreshold;

            if (Math.Abs(incoming.Saturation - disk.Saturation) > 1e-6f) merged.Saturation = incoming.Saturation;
            if (Math.Abs(incoming.Brightness - disk.Brightness) > 1e-6f) merged.Brightness = incoming.Brightness;
            if (Math.Abs(incoming.Contrast - disk.Contrast) > 1e-6f) merged.Contrast = incoming.Contrast;
            if (Math.Abs(incoming.Hue - disk.Hue) > 1e-6f) merged.Hue = incoming.Hue;
            if (Math.Abs(incoming.Emission - disk.Emission) > 1e-6f) merged.Emission = incoming.Emission;

            if (incoming.GlassProperties != disk.GlassProperties) merged.GlassProperties = incoming.GlassProperties;

            // Merge WaterProperties (important for WaterForward initialization)
            if (incoming.WaterProperties != disk.WaterProperties)
            {
                merged.WaterProperties = incoming.WaterProperties;
            }

            // Merge VegetationProperties (important for VegetationForward wind animation)
            if (incoming.VegetationProperties != disk.VegetationProperties)
            {
                merged.VegetationProperties = incoming.VegetationProperties;
            }

            // Water / planar reflection fields
            if (incoming.UsePlanarReflection != disk.UsePlanarReflection) merged.UsePlanarReflection = incoming.UsePlanarReflection;
            if (Math.Abs(incoming.WaterReflectionStrength - disk.WaterReflectionStrength) > 1e-6f) merged.WaterReflectionStrength = incoming.WaterReflectionStrength;

            return merged;
        }

        private static bool ArrayEquals(float[]? x, float[]? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (Math.Abs(x[i] - y[i]) > 1e-6f) return false;
            return true;
        }

        /// <summary>
        /// Create a deep clone of a MaterialAsset to avoid shared reference issues.
        /// This ensures that modifications to the clone don't affect the original.
        /// </summary>
        public static MaterialAsset CloneMaterial(MaterialAsset m)
        {
            return new MaterialAsset
            {
                Guid = m.Guid,
                Name = m.Name,
                Shader = m.Shader,
                AlbedoTexture = m.AlbedoTexture,
                AlbedoColor = m.AlbedoColor != null ? (float[])m.AlbedoColor.Clone() : new float[] { 1f, 1f, 1f, 1f },
                NormalTexture = m.NormalTexture,
                NormalStrength = m.NormalStrength,
                MetallicTexture = m.MetallicTexture,
                RoughnessTexture = m.RoughnessTexture,
                MetallicRoughnessTexture = m.MetallicRoughnessTexture,
                OcclusionTexture = m.OcclusionTexture,
                OcclusionStrength = m.OcclusionStrength,
                EmissiveTexture = m.EmissiveTexture,
                EmissiveColor = m.EmissiveColor != null ? (float[])m.EmissiveColor.Clone() : new float[] { 1f, 1f, 1f },
                HeightTexture = m.HeightTexture,
                HeightScale = m.HeightScale,
                DetailMaskTexture = m.DetailMaskTexture,
                DetailAlbedoTexture = m.DetailAlbedoTexture,
                DetailNormalTexture = m.DetailNormalTexture,
                Metallic = m.Metallic,
                Roughness = m.Roughness,
                TextureTiling = m.TextureTiling != null ? (float[])m.TextureTiling.Clone() : new float[] { 1f, 1f },
                TextureOffset = m.TextureOffset != null ? (float[])m.TextureOffset.Clone() : new float[] { 0f, 0f },
                UseTriplanar = m.UseTriplanar,
                TriplanarScale = m.TriplanarScale,
                TriplanarBlendSharpness = m.TriplanarBlendSharpness,
                TransparencyMode = m.TransparencyMode,
                Opacity = m.Opacity,
                CullingMode = m.CullingMode,
                AlphaClippingEnabled = m.AlphaClippingEnabled,
                AlphaClipThreshold = m.AlphaClipThreshold,
                Saturation = m.Saturation,
                Brightness = m.Brightness,
                Contrast = m.Contrast,
                Hue = m.Hue,
                Emission = m.Emission,
                GlassProperties = m.GlassProperties != null ? new GlassMaterialProperties
                {
                    RefractiveIndex = m.GlassProperties.RefractiveIndex,
                    DistortionStrength = m.GlassProperties.DistortionStrength,
                    ChromaticAberration = m.GlassProperties.ChromaticAberration,
                    Roughness = m.GlassProperties.Roughness,
                    Thickness = m.GlassProperties.Thickness,
                    Tint = m.GlassProperties.Tint != null ? (float[])m.GlassProperties.Tint.Clone() : new float[] { 1f, 1f, 1f },
                    Opacity = m.GlassProperties.Opacity,
                    FresnelPower = m.GlassProperties.FresnelPower,
                    ReflectionStrength = m.GlassProperties.ReflectionStrength
                } : null,
                WaterProperties = m.WaterProperties != null ? new WaterProperties
                {
                    WaveSpeed = m.WaterProperties.WaveSpeed,
                    WaveHeight = m.WaterProperties.WaveHeight,
                    WaveFrequency = m.WaterProperties.WaveFrequency,
                    Reflectivity = m.WaterProperties.Reflectivity,
                    FresnelPower = m.WaterProperties.FresnelPower,
                    DistortionStrength = m.WaterProperties.DistortionStrength,
                    Transparency = m.WaterProperties.Transparency,
                    SpecularPower = m.WaterProperties.SpecularPower,
                    SpecularColor = m.WaterProperties.SpecularColor != null ? (float[])m.WaterProperties.SpecularColor.Clone() : new float[] { 1f, 1f, 1f }
                } : null,
                UsePlanarReflection = m.UsePlanarReflection,
                WaterReflectionStrength = m.WaterReflectionStrength
            };
        }
    }
}
