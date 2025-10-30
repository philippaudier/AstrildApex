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
        private static Guid _defaultWaterMaterialGuid = Guid.Empty;

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

            // Fichiers bruts (png/jpg/gltf/fbx/)  GUID via sidecar .meta
            foreach (var f in Directory.EnumerateFiles(AssetsRoot, "*.*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(MetaExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(MaterialExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(SkyboxExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.EndsWith(MeshAssetExt, StringComparison.OrdinalIgnoreCase)) continue;

                var type = GuessTypeFromExtension(Path.GetExtension(f));
                var metaPath = f + MetaExt;

                Guid guid;
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var meta = JsonSerializer.Deserialize<MetaData>(File.ReadAllText(metaPath));
                        guid = meta?.guid ?? Guid.NewGuid();
                    }
                    catch { guid = Guid.NewGuid(); }
                }
                else guid = Guid.NewGuid();

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
                ".hdr" => "TextureHDR",
                ".gltf" or ".glb" => "ModelGLTF",
                ".fbx" => "ModelFBX",
                ".obj" => "ModelOBJ",
                ".dae" => "ModelDAE",
                ".3ds" => "Model3DS",
                ".blend" => "ModelBlend",
                ".ply" => "ModelPLY",
                ".stl" => "ModelSTL",
                ".meshasset" => "MeshAsset",
                ".skymat" => "SkyboxMaterial",
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

        public static void SaveMaterial(MaterialAsset mat)
        {
            if (!TryGet(mat.Guid, out var rec)) throw new InvalidOperationException("Material not indexed");
            // Prevent re-entrant saves for same material GUID which can cause overwrite races
            if (!_savingInProgress.Add(mat.Guid))
            {
                try { Console.WriteLine($"[AssetDatabase] Skipping SaveMaterial for {mat.Guid} because save already in progress"); } catch { }
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
                    Console.WriteLine($"[AssetDatabase] SaveMaterial() called by {caller} for {mat.Guid} -> {rec.Path}");
                }
                catch { Console.WriteLine($"[AssetDatabase] SaveMaterial() called for {mat.Guid} -> {rec.Path}"); }

                // Save synchronously - simple and reliable
                MaterialAsset.SaveAtomic(rec.Path, mat);
                Console.WriteLine($"[AssetDatabase] Material file written: {rec.Path}");

                EnsureMetaExists(rec);
                Console.WriteLine($"[AssetDatabase] EnsureMetaExists completed for {rec.Path + MetaExt}");

                // Read back the file to verify what was persisted (debug help)
                try
                {
                    var saved = MaterialAsset.Load(rec.Path);
                    Console.WriteLine($"[AssetDatabase] Saved material readback: Guid={saved.Guid}, Name={saved.Name}, Roughness={saved.Roughness}, Metallic={saved.Metallic}");
                    // Update in-memory cache to prefer this freshly-saved copy and avoid immediate disk read races
                    lock (_materialCacheLock)
                    {
                        _materialCache[saved.Guid] = saved;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AssetDatabase] Failed to readback saved material: {ex.Message}");
                }
                // Notify that material has been saved/modified
                // Notify that material has been saved/modified
                MaterialSaved?.Invoke(mat.Guid);
                Console.WriteLine($"[AssetDatabase] MaterialSaved event invoked for {mat.Guid}");

                // Ensure runtime material caches (GL-side) are cleared so updated properties
                // (tiling, offset, normal strength, etc.) are picked up without requiring a manual reassign.
                try
                {
                    Engine.Rendering.MaterialRuntime.ClearGlobalCache();
                }
                catch { }
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

        public static string GetName(Guid guid) => TryGet(guid, out var r) ? r.Name : guid.ToString();
        public static string GetTypeName(Guid guid) => TryGet(guid, out var r) ? r.Type : "?";

        static string Sanitize(string n)
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

        public static Guid EnsureDefaultWaterMaterial()
        {
            // Si on a déjà un GUID en cache et qu'il existe toujours, le retourner
            if (_defaultWaterMaterialGuid != Guid.Empty && TryGet(_defaultWaterMaterialGuid, out _))
                return _defaultWaterMaterialGuid;

            // Chercher d'abord physiquement le fichier "WaterMaterial.material"
            string matFolder = Path.Combine(AssetsRoot, "Materials");
            string matPath = Path.Combine(matFolder, "WaterMaterial.material");

            if (File.Exists(matPath))
            {
                try
                {
                    var loadedMat = MaterialAsset.Load(matPath);
                    _defaultWaterMaterialGuid = loadedMat.Guid;

                    // S'assurer qu'il est indexé
                    if (!TryGet(_defaultWaterMaterialGuid, out _))
                    {
                        var loadedRec = new AssetRecord(loadedMat.Guid, matPath, "Material");
                        Index(loadedRec);
                        EnsureMetaExists(loadedRec);
                    }
                    return _defaultWaterMaterialGuid;
                }
                catch { /* continue si échec de lecture */ }
            }

            // Chercher dans l'index un material déjà nommé exactement "WaterMaterial"
            foreach (var assetRec in _byGuid.Values)
            {
                if (string.Equals(assetRec.Type, "Material", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(assetRec.Name, "WaterMaterial", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultWaterMaterialGuid = assetRec.Guid;
                    return _defaultWaterMaterialGuid;
                }
            }

            // Aucun "WaterMaterial" trouvé - créer un nouveau UNIQUEMENT avec ce nom exact
            Directory.CreateDirectory(matFolder);

            var mat = new MaterialAsset
            {
                Guid = Guid.NewGuid(),
                Name = "WaterMaterial",
                Shader = "Water", // Use Water shader
                AlbedoColor = new float[] { 0.1f, 0.3f, 0.5f, 0.8f }, // Blue water color
                AlbedoTexture = null,
                Metallic = 0f,
                Roughness = 0.1f,
                WaterProperties = new WaterMaterialProperties
                {
                    // Wave parameters - moderate waves by default
                    WaveAmplitude = 0.15f,
                    WaveFrequency = 1.2f,
                    WaveSpeed = 1.5f,
                    WaveDirection = new float[] { 1f, 0f },

                    // Water color and appearance
                    WaterColor = new float[] { 0.1f, 0.3f, 0.5f, 0.8f },
                    Opacity = 0.8f,

                    // Albedo texture
                    AlbedoTexture = null,
                    AlbedoColor = new float[] { 1.0f, 1.0f, 1.0f, 1.0f },

                    // Normal map
                    NormalTexture = null,
                    NormalStrength = 1.0f,

                    // PBR properties
                    Metallic = 0.0f,
                    Smoothness = 0.9f,

                    // Noise textures (will be null initially - user can add textures)
                    NoiseTexture1 = null,
                    NoiseTexture2 = null,
                    Noise1Speed = new float[] { 0.03f, 0.03f },
                    Noise1Direction = new float[] { 1f, 0f },
                    Noise1Tiling = new float[] { 1f, 1f },
                    Noise1Strength = 0.05f,
                    Noise2Speed = new float[] { 0.02f, -0.02f },
                    Noise2Direction = new float[] { 0f, 1f },
                    Noise2Tiling = new float[] { 1.5f, 1.5f },
                    Noise2Strength = 0.03f,

                    // Refraction and fresnel
                    RefractionStrength = 0.5f,
                    FresnelPower = 2.0f,
                    FresnelColor = new float[] { 0.8f, 0.9f, 1.0f, 1.0f },

                    // Tessellation
                    TessellationLevel = 32.0f
                }
            };

            // Sauver directement avec le nom exact (pas de numérotation)
            MaterialAsset.Save(matPath, mat);

            var rec = new AssetRecord(mat.Guid, matPath, "Material");
            Index(rec);
            EnsureMetaExists(rec);

            _defaultWaterMaterialGuid = mat.Guid;
            return _defaultWaterMaterialGuid;
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
                Metallic = src.Metallic,
                Roughness = src.Roughness
            };

            var rec = CreateMaterial(clone.Name);   // crée l’asset, enregistre et renvoie l’enregistrement
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
                    if (File.Exists(meshAssetPath))
                    {
                        meshAsset = MeshAsset.Load(meshAssetPath);
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

                // DIAGNOSTIC: Log where the mesh is being loaded from to find duplicate loads
                var caller = new System.Diagnostics.StackTrace(2, false).GetFrame(0);
                var callerMethod = caller?.GetMethod();
                var callerType = callerMethod?.DeclaringType?.Name ?? "Unknown";
                Console.WriteLine($"[AssetDatabase] ⚡ Loaded mesh from cache: {Path.GetFileName(cachePath)} (called by {callerType}.{callerMethod?.Name})");
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
