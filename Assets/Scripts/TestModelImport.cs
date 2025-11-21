// Test Script for Model Import System
// Place this in Assets/Scripts/TestModelImport.cs

using System;
using System.IO;
using Engine.Assets;
using Engine.Utils;

namespace TestScripts
{
    /// <summary>
    /// Script de test pour valider le système d'importation de modèles 3D
    /// </summary>
    public static class TestModelImport
    {
        /// <summary>
        /// Test d'import d'un modèle FBX
        /// </summary>
        public static void TestFBXImport(string fbxPath)
        {
            Console.WriteLine("=== TEST FBX IMPORT ===");
            Console.WriteLine($"Fichier: {fbxPath}");
            
            try
            {
                // Vérifier que le fichier existe
                if (!File.Exists(fbxPath))
                {
                    Console.WriteLine("❌ ERREUR: Fichier introuvable");
                    return;
                }
                
                // Vérifier le format
                var ext = Path.GetExtension(fbxPath).ToLowerInvariant();
                if (!ModelLoader.IsSupported(ext))
                {
                    Console.WriteLine($"❌ ERREUR: Format {ext} non supporté");
                    return;
                }
                
                Console.WriteLine($"✓ Format {ext} supporté");
                
                // Charger le modèle
                Console.WriteLine("Chargement du modèle...");
                var meshAsset = ModelLoader.LoadModel(fbxPath);
                
                Console.WriteLine($"✓ Modèle chargé: {meshAsset.Name}");
                Console.WriteLine($"  - SubMeshes: {meshAsset.SubMeshes.Count}");
                Console.WriteLine($"  - Vertices: {meshAsset.TotalVertexCount}");
                Console.WriteLine($"  - Triangles: {meshAsset.TotalTriangleCount}");
                Console.WriteLine($"  - Materials: {meshAsset.MaterialGuids.Count}");
                
                // Recommandation pour les collisions
                Console.WriteLine();
                Console.WriteLine("💡 TIP: Pour les modèles importés, ajoutez automatiquement un MeshCollider:");
                Console.WriteLine("   ColliderSetupHelper.EnsureCollider(entity);");
                Console.WriteLine("   ou utilisez le bouton 'Add MeshCollider' dans l'Inspector");
                Console.WriteLine();
                
                // Vérifier les UVs
                foreach (var submesh in meshAsset.SubMeshes)
                {
                    Console.WriteLine($"\nSubMesh: {submesh.Name}");
                    Console.WriteLine($"  - Vertices: {submesh.VertexCount}");
                    Console.WriteLine($"  - Triangles: {submesh.TriangleCount}");
                    Console.WriteLine($"  - Material Index: {submesh.MaterialIndex}");
                    
                    // Vérifier que les UVs sont dans le range [0,1]
                    // (les vertices sont entrelacés: pos(3), normal(3), uv(2))
                    bool uvsValid = true;
                    for (int i = 0; i < submesh.Vertices.Length; i += 8)
                    {
                        float u = submesh.Vertices[i + 6];
                        float v = submesh.Vertices[i + 7];
                        
                        if (u < -0.1f || u > 1.1f || v < -0.1f || v > 1.1f)
                        {
                            uvsValid = false;
                            Console.WriteLine($"  ⚠ UVs hors range: ({u:F2}, {v:F2})");
                            break;
                        }
                    }
                    
                    if (uvsValid)
                    {
                        Console.WriteLine("  ✓ UVs dans le range valide");
                    }
                }
                
                Console.WriteLine("\n✓ TEST RÉUSSI");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERREUR: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Test d'import complet avec matériaux
        /// </summary>
        public static void TestFullImport(string modelPath, string assetsPath)
        {
            Console.WriteLine("=== TEST FULL IMPORT ===");
            Console.WriteLine($"Modèle: {modelPath}");
            Console.WriteLine($"Assets: {assetsPath}");
            
            try
            {
                // Import complet
                var guid = ModelImporter.ImportModel(modelPath, assetsPath, "Models");
                
                Console.WriteLine($"✓ Import réussi: GUID = {guid}");
                
                // Vérifier la structure de dossiers
                var modelName = Path.GetFileNameWithoutExtension(modelPath);
                var modelFolder = Path.Combine(assetsPath, "Models", modelName);
                
                Console.WriteLine($"\nVérification de la structure:");
                Console.WriteLine($"  Dossier modèle: {(Directory.Exists(modelFolder) ? "✓" : "❌")}");
                
                var materialsFolder = Path.Combine(modelFolder, "Materials");
                Console.WriteLine($"  Dossier Materials: {(Directory.Exists(materialsFolder) ? "✓" : "❌")}");
                
                var texturesFolder = Path.Combine(modelFolder, "Textures");
                Console.WriteLine($"  Dossier Textures: {(Directory.Exists(texturesFolder) ? "✓" : "❌")}");
                
                // Compter les fichiers créés
                if (Directory.Exists(materialsFolder))
                {
                    var materials = Directory.GetFiles(materialsFolder, "*.material");
                    Console.WriteLine($"  Materials créés: {materials.Length}");
                    foreach (var mat in materials)
                    {
                        Console.WriteLine($"    - {Path.GetFileName(mat)}");
                    }
                }
                
                if (Directory.Exists(texturesFolder))
                {
                    var textures = Directory.GetFiles(texturesFolder, "*.png");
                    textures = textures.Concat(Directory.GetFiles(texturesFolder, "*.jpg")).ToArray();
                    Console.WriteLine($"  Textures copiées: {textures.Length}");
                    foreach (var tex in textures)
                    {
                        Console.WriteLine($"    - {Path.GetFileName(tex)}");
                    }
                }
                
                Console.WriteLine("\n✓ TEST RÉUSSI");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERREUR: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Test de détection des patterns de texture
        /// </summary>
        public static void TestTexturePatternDetection()
        {
            Console.WriteLine("=== TEST TEXTURE PATTERN DETECTION ===");
            
            var testCases = new[]
            {
                ("Car_Albedo.png", "Albedo"),
                ("Car_BaseColor.png", "Albedo"),
                ("Car_Diffuse.png", "Albedo"),
                ("Car_Normal.png", "Normal"),
                ("Car_NormalMap.png", "Normal"),
                ("Car_Metallic.png", "Metallic"),
                ("Car_Metal.png", "Metallic"),
                ("Car_Roughness.png", "Roughness"),
                ("Car_Rough.png", "Roughness"),
                ("Car_AO.png", "AO"),
                ("Car_Occlusion.png", "AO"),
                ("Car_Emissive.png", "Emissive"),
                ("Car_Emission.png", "Emissive"),
                ("Car_MetallicRoughness.png", "Combined"),
            };
            
            foreach (var (fileName, expectedType) in testCases)
            {
                var lowerName = fileName.ToLowerInvariant();
                var baseName = Path.GetFileNameWithoutExtension(lowerName);
                
                string detectedType = "Unknown";
                
                if (baseName.Contains("normal") || baseName.Contains("norm"))
                    detectedType = "Normal";
                else if (baseName.Contains("metallic") && baseName.Contains("rough"))
                    detectedType = "Combined";
                else if (baseName.Contains("metallic") || baseName.Contains("metal"))
                    detectedType = "Metallic";
                else if (baseName.Contains("rough"))
                    detectedType = "Roughness";
                else if (baseName.Contains("ao") || baseName.Contains("occlusion"))
                    detectedType = "AO";
                else if (baseName.Contains("emissive") || baseName.Contains("emission"))
                    detectedType = "Emissive";
                else if (baseName.Contains("albedo") || baseName.Contains("basecolor") || baseName.Contains("diffuse"))
                    detectedType = "Albedo";
                
                var result = detectedType == expectedType ? "✓" : "❌";
                Console.WriteLine($"{result} {fileName} → {detectedType} (attendu: {expectedType})");
            }
            
            Console.WriteLine("\n✓ TEST TERMINÉ");
        }
        
        /// <summary>
        /// Test des formats supportés
        /// </summary>
        public static void TestSupportedFormats()
        {
            Console.WriteLine("=== TEST FORMATS SUPPORTÉS ===");
            
            var formats = new[] { ".fbx", ".obj", ".gltf", ".glb", ".dae", ".blend", ".3ds", ".ply" };
            
            foreach (var format in formats)
            {
                var supported = ModelLoader.IsSupported(format);
                var status = supported ? "✓ Supporté" : "❌ Non supporté";
                Console.WriteLine($"{format.PadRight(8)} : {status}");
            }
            
            Console.WriteLine("\nFormats attendus comme supportés: FBX, OBJ, GLTF, GLB, DAE");
            Console.WriteLine("Formats attendus comme non-supportés: BLEND, 3DS, PLY");
        }
    }
}
