using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.IO;
using StbImageSharp;
using Engine.Assets;
using Engine.Scene;

namespace Engine.Rendering
{
    /// <summary>
    /// Skybox renderer for Unity-like environment system
    /// </summary>
    public sealed class SkyboxRenderer : IDisposable
    {
        private int _vao;
        private int _vbo;
        private ShaderProgram? _shader;
        private uint _cubemapTexture;
        private PMREMGenerator? _pmremGen;
        private Guid _cubemapTextureGuid = Guid.Empty; // Track GUID to check if texture is still pending

        // Public static handles for global IBL resources (can be bound by MaterialRuntime)
        public static uint IrradianceMap = 0;
        public static uint PrefilteredEnvMap = 0;
        public static uint BRDFLUTTexture = 0;
        public static float PrefilterMaxLod = 6.0f; // Maximum mipmap level for prefiltered environment
        private string? _currentEquirectangularPath = null;
        private readonly List<System.Runtime.InteropServices.GCHandle> _textureHandles = new();
        // Logging state to avoid spamming the console each frame
        private uint _lastLoggedCubemapHandle = 0;
        private string? _lastLoggedKtxPath = null;
        private bool _ktxPendingLogged = false;

        // Static reference to current lighting state for sun direction
        public static LightingState? CurrentLightingState { get; set; }
        
        // Skybox cube vertices (positions only)
        private static readonly float[] SkyboxVertices = {
            // Positions        
            -1.0f,  1.0f, -1.0f,
            -1.0f, -1.0f, -1.0f,
             1.0f, -1.0f, -1.0f,
             1.0f, -1.0f, -1.0f,
             1.0f,  1.0f, -1.0f,
            -1.0f,  1.0f, -1.0f,

            -1.0f, -1.0f,  1.0f,
            -1.0f, -1.0f, -1.0f,
            -1.0f,  1.0f, -1.0f,
            -1.0f,  1.0f, -1.0f,
            -1.0f,  1.0f,  1.0f,
            -1.0f, -1.0f,  1.0f,

             1.0f, -1.0f, -1.0f,
             1.0f, -1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,
             1.0f,  1.0f, -1.0f,
             1.0f, -1.0f, -1.0f,

            -1.0f, -1.0f,  1.0f,
            -1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,
             1.0f, -1.0f,  1.0f,
            -1.0f, -1.0f,  1.0f,

            -1.0f,  1.0f, -1.0f,
             1.0f,  1.0f, -1.0f,
             1.0f,  1.0f,  1.0f,
             1.0f,  1.0f,  1.0f,
            -1.0f,  1.0f,  1.0f,
            -1.0f,  1.0f, -1.0f,

            -1.0f, -1.0f, -1.0f,
            -1.0f, -1.0f,  1.0f,
             1.0f, -1.0f, -1.0f,
             1.0f, -1.0f, -1.0f,
            -1.0f, -1.0f,  1.0f,
             1.0f, -1.0f,  1.0f
        };

        public SkyboxRenderer()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Load skybox shader
            try
            {
                _shader = ShaderProgram.FromFiles(
                    "Engine/Rendering/Shaders/skybox.vert",
                    "Engine/Rendering/Shaders/skybox.frag"
                );
            }
            catch (Exception)
            {

                // Try alternative paths
                var alternatePaths = new[]
                {
                    ("Shaders/skybox.vert", "Shaders/skybox.frag"),
                    ("../Engine/Rendering/Shaders/skybox.vert", "../Engine/Rendering/Shaders/skybox.frag"),
                    ("skybox.vert", "skybox.frag")
                };

                foreach (var (vert, frag) in alternatePaths)
                {
                    try
                    {
                        if (File.Exists(vert) && File.Exists(frag))
                        {
                            _shader = ShaderProgram.FromFiles(vert, frag);
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (_shader == null)
                {
                    return;
                }
            }

            // Generate VAO and VBO
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, SkyboxVertices.Length * sizeof(float), SkyboxVertices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            GL.BindVertexArray(0);

            // Generate BRDF LUT for IBL specular integration
            try
            {
                GenerateBRDFLUT();
            }
            catch (Exception)
            {
                // If BRDF LUT generation fails, fallback remains White1x1 assigned elsewhere
            }
            // PMREM generator (optional - used when KTX lacks prefiltered mipmaps)
            try { _pmremGen = new PMREMGenerator(); } catch { _pmremGen = null; }
        }

        /// <summary>
        /// Load a skybox from 6 face textures (Unity style)
        /// </summary>
        /// <param name="faces">Array of 6 texture paths: [right, left, top, bottom, front, back]</param>
        public void LoadSkybox(string[] faces)
        {
            if (faces.Length != 6)
                throw new ArgumentException("Skybox requires exactly 6 face textures");

            // Clean up existing texture ONLY if we created it ourselves (not from TextureCache)
            // TextureCache-managed textures (_currentEquirectangularPath is null or "six-sided") should NOT be deleted
            if (_cubemapTexture != 0 && !string.IsNullOrEmpty(_currentEquirectangularPath))
            {
                // We created this texture (equirectangular conversion), so we own it and can delete it
                GL.DeleteTexture(_cubemapTexture);
                _cubemapTexture = 0;
            }
            else if (_cubemapTexture != 0)
            {
                // Texture is from TextureCache or other source, just clear our reference
                _cubemapTexture = 0;
            }

            // Reset logging state so future assignments are logged
            _lastLoggedCubemapHandle = 0;
            _lastLoggedKtxPath = null;
            _ktxPendingLogged = false;

            _cubemapTexture = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);

            // Not generated from an equirectangular HDR source
            _currentEquirectangularPath = null;

            bool anyFaceLoaded = false;
            for (int i = 0; i < faces.Length; i++)
            {
                var target = TextureTarget.TextureCubeMapPositiveX + i;

                if (!string.IsNullOrEmpty(faces[i]) && File.Exists(faces[i]))
                {
                    try
                    {
                        var (textureData, imageData) = LoadTextureDataSafe(faces[i]);
                        GL.TexImage2D(target, 0, textureData.InternalFormat,
                            textureData.Width, textureData.Height, 0,
                            textureData.Format, textureData.Type, textureData.Data);
                        anyFaceLoaded = true;
                    }
                    catch (Exception)
                    {
                        // Create a fallback solid color face
                        CreateFallbackFace(target, GetFallbackColor(i));
                    }
                }
                else
                {
                    // Create a fallback solid color face
                    CreateFallbackFace(target, GetFallbackColor(i));
                }
            }

            if (!anyFaceLoaded)
            {
            }

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.TextureCubeMap, 0);
        }

        /// <summary>
        /// Texture data structure for loading
        /// </summary>
        private struct TextureData
        {
            public IntPtr Data;
            public int Width, Height;
            public PixelInternalFormat InternalFormat;
            public PixelFormat Format;
            public PixelType Type;
        }

        /// <summary>
        /// Load texture data from file and keep it alive until OpenGL is done with it
        /// </summary>
        private (TextureData data, byte[] imageData) LoadTextureDataSafe(string path)
        {
            using var fs = File.OpenRead(path);
            var img = StbImageSharp.ImageResult.FromStream(fs, StbImageSharp.ColorComponents.RedGreenBlueAlpha);

            // Keep the image data alive by copying it
            var imageDataCopy = new byte[img.Data.Length];
            Array.Copy(img.Data, imageDataCopy, img.Data.Length);

            var data = new TextureData
            {
                Width = img.Width,
                Height = img.Height,
                InternalFormat = PixelInternalFormat.Rgba8,
                Format = PixelFormat.Rgba,
                Type = PixelType.UnsignedByte
            };

            // Clean up old handles first to prevent memory accumulation
            foreach (var oldHandle in _textureHandles)
            {
                if (oldHandle.IsAllocated)
                    oldHandle.Free();
            }
            _textureHandles.Clear();

            // Pin the copied image data for OpenGL usage
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(imageDataCopy, System.Runtime.InteropServices.GCHandleType.Pinned);
            data.Data = handle.AddrOfPinnedObject();
            _textureHandles.Add(handle);

            return (data, imageDataCopy);
        }

        /// <summary>
        /// Create a fallback solid color face for missing textures
        /// </summary>
        private void CreateFallbackFace(TextureTarget target, Vector3 color)
        {
            const int size = 64;
            var pixels = new byte[size * size * 3];
            byte r = (byte)(color.X * 255);
            byte g = (byte)(color.Y * 255);
            byte b = (byte)(color.Z * 255);

            for (int i = 0; i < pixels.Length; i += 3)
            {
                pixels[i] = r;
                pixels[i + 1] = g;
                pixels[i + 2] = b;
            }

            GL.TexImage2D(target, 0, PixelInternalFormat.Rgb8, size, size, 0,
                PixelFormat.Rgb, PixelType.UnsignedByte, pixels);
        }

        /// <summary>
        /// Get fallback colors for each cubemap face (Unity-like defaults)
        /// </summary>
        private Vector3 GetFallbackColor(int faceIndex)
        {
            return faceIndex switch
            {
                0 => new Vector3(0.8f, 0.4f, 0.4f), // +X (Right) - Red tint
                1 => new Vector3(0.4f, 0.8f, 0.4f), // -X (Left) - Green tint
                2 => new Vector3(0.6f, 0.8f, 1.0f), // +Y (Top) - Sky blue
                3 => new Vector3(0.2f, 0.4f, 0.2f), // -Y (Bottom) - Dark green
                4 => new Vector3(0.4f, 0.4f, 0.8f), // +Z (Front) - Blue tint
                5 => new Vector3(0.8f, 0.8f, 0.4f), // -Z (Back) - Yellow tint
                _ => new Vector3(0.5f, 0.5f, 0.5f)  // Default gray
            };
        }

        /// <summary>
        /// Create a simple procedural skybox (gradient)
        /// </summary>
        public void CreateProceduralSkybox(Vector3 topColor, Vector3 bottomColor)
        {
            // Clean up existing texture ONLY if we created it ourselves (not from TextureCache)
            // TextureCache-managed textures (_currentEquirectangularPath is null) should NOT be deleted
            if (_cubemapTexture != 0 && !string.IsNullOrEmpty(_currentEquirectangularPath))
            {
                // We created this texture (equirectangular conversion), so we own it and can delete it
                GL.DeleteTexture(_cubemapTexture);
                _cubemapTexture = 0;
            }
            else if (_cubemapTexture != 0)
            {
                // Texture is from TextureCache or other source, just clear our reference
                _cubemapTexture = 0;
            }

            // Reset logging state so future assignments are logged
            _lastLoggedCubemapHandle = 0;
            _lastLoggedKtxPath = null;
            _ktxPendingLogged = false;

            _cubemapTexture = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);

            // Create simple gradient data for each face
            var pixels = new byte[512 * 512 * 3];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < 512; y++)
                {
                    float t = (float)y / 511.0f; // 0 = bottom, 1 = top
                    var color = Vector3.Lerp(bottomColor, topColor, t);
                    
                    for (int x = 0; x < 512; x++)
                    {
                        int idx = (y * 512 + x) * 3;
                        pixels[idx] = (byte)(color.X * 255);
                        pixels[idx + 1] = (byte)(color.Y * 255);
                        pixels[idx + 2] = (byte)(color.Z * 255);
                    }
                }

                var target = TextureTarget.TextureCubeMapPositiveX + face;
                GL.TexImage2D(target, 0, PixelInternalFormat.Rgb, 512, 512, 0, 
                    PixelFormat.Rgb, PixelType.UnsignedByte, pixels);
            }

            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.TextureCubeMap, 0);

            // procedural cubemap is not from an equirectangular source
            _currentEquirectangularPath = null;
        }

        /// <summary>
        /// Render the skybox (call this first in your render loop, before other objects)
        /// </summary>
    public void Render(Matrix4 view, Matrix4 projection, Vector3 tintColor, float exposure, uint entityId = 0)
        {
            if (_shader == null)
            {
                return;
            }

            if (_cubemapTexture == 0)
            {
                return;
            }

            try
            {
                // Remove translation from view matrix (only rotation)
                var viewNoTranslation = new Matrix4(
                    view.M11, view.M12, view.M13, 0,
                    view.M21, view.M22, view.M23, 0,
                    view.M31, view.M32, view.M33, 0,
                    0, 0, 0, 1
                );

                // Unity-style skybox rendering: render only where nothing else has been rendered
                GL.DepthMask(false); // Don't write to depth buffer
                GL.DepthFunc(DepthFunction.Lequal); // Render where depth = 1.0 (far plane)

                // CRITICAL: Check if cubemap is valid before rendering
                // Background texture loading might not be complete yet
                if (_cubemapTexture == 0 || TextureCache.IsPending(_cubemapTextureGuid))
                {
                    // Texture not ready yet, use procedural fallback
                    CreateProceduralSkybox(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.3f, 0.3f, 0.3f));

                    // Render procedural instead
                    if (_cubemapTexture == 0)
                        return;

                    // If texture is still pending upload, skip rendering it
                    if (TextureCache.IsPending(_cubemapTextureGuid))
                        return;
                }

                _shader.Use();
                _shader.SetMat4("view", viewNoTranslation);
                _shader.SetMat4("projection", projection);
                _shader.SetInt("uMode", 0);
                _shader.SetVec3("tintColor", tintColor);
                _shader.SetFloat("exposure", Math.Max(0.0f, exposure));
                _shader.SetUInt("u_ObjectId", entityId);

                // Bind skybox texture
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);
                _shader.SetInt("skybox", 0);

                // If Lighting state is provided via static CurrentLightingState, set fog uniforms
                var ls = GetCurrentLightingState();
                if (ls != null)
                {
                    _shader.SetInt("uFogEnabled", ls.FogEnabled ? 1 : 0);
                    _shader.SetVec3("uFogColor", ls.FogColor);
                    _shader.SetFloat("uFogStart", ls.FogStart);
                    _shader.SetFloat("uFogEnd", ls.FogEnd);
                    _shader.SetFloat("uFogDensity", ls.FogDensity);
                }

                // Render cube
                GL.BindVertexArray(_vao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
                GL.BindVertexArray(0);

                // Restore normal depth state
                GL.DepthMask(true);
                GL.DepthFunc(DepthFunction.Less);
            }
            catch (Exception)
            {

                // Always restore depth state even on error
                try
                {
                    GL.DepthMask(true);
                    GL.DepthFunc(DepthFunction.Less);
                    GL.BindVertexArray(0);
                    GL.BindTexture(TextureTarget.TextureCubeMap, 0);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        
        /// <summary>
        /// Render skybox using a SkyboxMaterial (no extra tint/exposure)
        /// </summary>
        public void RenderWithMaterial(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, uint entityId = 0)
            => RenderWithMaterial(view, projection, skyboxMaterial, new Vector3(1, 1, 1), 1.0f, entityId);

        /// <summary>
        /// Render skybox with environment multipliers (Unity-like: env tint/exposure modulate material)
        /// </summary>
        public void RenderWithMaterial(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 envTintMul, float envExposureMul, uint entityId = 0)
        {
            if (skyboxMaterial == null)
            {
                return;
            }

            if (_shader == null)
            {
                return;
            }

            try
            {
                switch (skyboxMaterial.Type)
                {
                    case Engine.Assets.SkyboxType.Procedural:
                        RenderProcedural(view, projection, skyboxMaterial, envTintMul, envExposureMul, entityId);
                        break;
                    case Engine.Assets.SkyboxType.Cubemap:
                        RenderCubemap(view, projection, skyboxMaterial, envTintMul, envExposureMul, entityId);
                        break;
                    case Engine.Assets.SkyboxType.SixSided:
                        RenderSixSided(view, projection, skyboxMaterial, envTintMul, envExposureMul, entityId);
                        break;
                    case Engine.Assets.SkyboxType.Panoramic:
                        RenderPanoramic(view, projection, skyboxMaterial, envTintMul, envExposureMul, entityId);
                        break;
                    default:
                        // Fallback to procedural rendering
                        RenderProcedural(view, projection, skyboxMaterial, envTintMul, envExposureMul, entityId);
                        break;
                }
            }
            catch (Exception)
            {

                // Try to render a simple fallback procedural sky
                try
                {
                    CreateProceduralSkybox(new Vector3(0.6f, 0.8f, 1.0f), new Vector3(0.2f, 0.4f, 0.6f));
                    Render(view, projection, envTintMul, envExposureMul, entityId);
                }
                catch
                {
                    // If even fallback fails, just return
                }
            }
        }
        
        private void RenderProcedural(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul, uint entityId = 0)
        {
            if (_shader == null)
            {
                return;
            }

            try
            {

            // Use sky shader in procedural mode (no cubemap sampling needed here)
            var sky = new Vector3(skyboxMaterial.SkyTint[0], skyboxMaterial.SkyTint[1], skyboxMaterial.SkyTint[2]);
            var ground = new Vector3(skyboxMaterial.GroundColor[0], skyboxMaterial.GroundColor[1], skyboxMaterial.GroundColor[2]);
            var sunTint = new Vector3(skyboxMaterial.SunTint[0], skyboxMaterial.SunTint[1], skyboxMaterial.SunTint[2]);
            float atmosphereThickness = skyboxMaterial.AtmosphereThickness;
            float sunSize = skyboxMaterial.SunSize;
            float sunConv = skyboxMaterial.SunSizeConvergence;
            float modExposure = skyboxMaterial.Exposure * Math.Max(0.0f, exposureMul);

            // Sun direction: get from lighting state if available, otherwise use default
            var sunDir = new Vector3(0.321f, 0.766f, -0.557f); // Default Unity-like sun direction

            // Get sun direction from scene lighting if available
            var lightingState = GetCurrentLightingState();
            if (lightingState != null && lightingState.HasDirectional)
            {
                // The sun should appear where light comes from
                // DirDirection = forward direction of the light entity (where it points TO)
                // Lighting shader uses: L = -uDirLightDirection (direction FROM surface TO light)
                // Skybox needs: direction TO the sun = same as L = -DirDirection
                sunDir = -lightingState.DirDirection;
            }

            // Remove translation from view matrix (only rotation) like in Render()
            var viewNoTranslation = new Matrix4(
                view.M11, view.M12, view.M13, 0,
                view.M21, view.M22, view.M23, 0,
                view.M31, view.M32, view.M33, 0,
                0, 0, 0, 1
            );

            // sunDirection stays in world space, same as TexCoords (cube local space)
            // Both are independent of camera rotation

            // Create a minimal cubemap texture if none exists (required for shader to not error)
            if (_cubemapTexture == 0)
            {
                _cubemapTexture = (uint)GL.GenTexture();
                GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);
                
                // Create minimal 1x1 faces
                byte[] pixel = { 128, 128, 128 }; // Gray pixel
                for (int i = 0; i < 6; i++)
                {
                    var target = TextureTarget.TextureCubeMapPositiveX + i;
                    GL.TexImage2D(target, 0, PixelInternalFormat.Rgb, 1, 1, 0, 
                        PixelFormat.Rgb, PixelType.UnsignedByte, pixel);
                }
                
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
                GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            }

            // Unity-style skybox rendering: render only where nothing else has been rendered
            GL.DepthMask(false); // Don't write to depth buffer
            GL.DepthFunc(DepthFunction.Lequal); // Render where depth = 1.0 (far plane)
            
            _shader.Use();
            _shader.SetMat4("view", viewNoTranslation);
            _shader.SetMat4("projection", projection);
            _shader.SetInt("uMode", 1); // Procedural mode
            _shader.SetFloat("exposure", modExposure);
            _shader.SetUInt("u_ObjectId", entityId);
            _shader.SetVec3("skyTint", sky * tintMul);
            _shader.SetVec3("groundColor", ground);
            _shader.SetFloat("atmosphereThickness", atmosphereThickness);
            _shader.SetVec3("sunDirection", sunDir);
            _shader.SetVec3("sunTint", sunTint);
            _shader.SetFloat("sunSize", sunSize);
            _shader.SetFloat("sunSizeConvergence", sunConv);

            // Pass ambient and fog data if available
            if (lightingState != null)
            {
                _shader.SetVec3("uAmbientColor", lightingState.AmbientColor);
                _shader.SetFloat("uAmbientIntensity", lightingState.AmbientIntensity);

                _shader.SetInt("uFogEnabled", lightingState.FogEnabled ? 1 : 0);
                _shader.SetVec3("uFogColor", lightingState.FogColor);
                _shader.SetFloat("uFogStart", lightingState.FogStart);
                _shader.SetFloat("uFogEnd", lightingState.FogEnd);
                // Fog density param
                _shader.SetFloat("uFogDensity", lightingState.FogDensity);
            }
            else
            {
                _shader.SetInt("uFogEnabled", 0);
                _shader.SetFloat("uFogDensity", 0.01f);
            }

            // Debug procedural skybox values

            // Bind dummy cubemap (required for shader to not error, but not used in procedural mode)
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);
            _shader.SetInt("skybox", 0);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
            GL.BindVertexArray(0);

            // Restore normal depth state
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            }
            catch (Exception)
            {

                // Always restore depth state even on error
                try
                {
                    GL.DepthMask(true);
                    GL.DepthFunc(DepthFunction.Less);
                    GL.BindVertexArray(0);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        
        private void RenderCubemap(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul, uint entityId = 0)
        {
            var tint = new Vector3(skyboxMaterial.CubemapTint[0], skyboxMaterial.CubemapTint[1], skyboxMaterial.CubemapTint[2]);

            try
            {
                // Try to load cubemap texture if specified
                if (skyboxMaterial.CubemapTexture.HasValue && skyboxMaterial.CubemapTexture.Value != Guid.Empty)
                {
                    // Try to get texture path from asset database
                    if (Engine.Assets.AssetDatabase.TryGet(skyboxMaterial.CubemapTexture.Value, out var textureRecord))
                    {

                        // Check if it's an HDR file
                        if (textureRecord.Path.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                        {
                            // Load HDR as cubemap, apply cubemap rotation from material
                            // Note: exposure is NOT applied during conversion, it's applied in shader
                            LoadHDREquirectangular(textureRecord.Path, skyboxMaterial.CubemapRotation, PanoramicMapping.Latitude_Longitude_Layout, false, PanoramicImageType.Degrees360, 1.0f);
                        }
                        else if (textureRecord.Path.EndsWith(".ktx", StringComparison.OrdinalIgnoreCase))
                        {
                            // Try to load a KTX cubemap (generated by cmgen) via TextureCache
                            try
                            {
                                int handle = TextureCache.GetOrLoad(skyboxMaterial.CubemapTexture.Value, guid => {
                                    if (Engine.Assets.AssetDatabase.TryGet(guid, out var r)) return r.Path;
                                    return null;
                                });

                                if (handle != TextureCache.White1x1)
                                {
                                    // Only reassign if the handle is different (avoid deleting/reassigning every frame)
                                    if (_cubemapTexture != (uint)handle)
                                    {
                                        // IMPORTANT: Clean up old texture ONLY if we created it (equirectangular conversions)
                                        // TextureCache-managed textures should NOT be deleted
                                        if (_cubemapTexture != 0 && !string.IsNullOrEmpty(_currentEquirectangularPath))
                                        {
                                            GL.DeleteTexture(_cubemapTexture);
                                        }

                                        _cubemapTexture = (uint)handle;
                                        _cubemapTextureGuid = skyboxMaterial.CubemapTexture.Value; // Store GUID to check pending status
                                        _currentEquirectangularPath = null; // Mark as TextureCache-managed (don't delete on cleanup)

                                        // Expose IBL resources. Prefer to use prefiltered/irradiance maps
                                        // If the assigned KTX lacks mipmaps/prefilter, generate PMREM on GPU
                                        bool usedPMREM = false;
                                        try
                                        {
                                            GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);
                                            GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, 0, GetTextureParameter.TextureWidth, out int cubeSize);
                                            GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, 1, GetTextureParameter.TextureWidth, out int mip1W);
                                            if (mip1W == 0 && _pmremGen != null)
                                            {
                                                // No extra mips: generate irradiance & prefiltered environment
                                                // Use 64x64 for better quality (eliminates white spots on matte materials)
                                                var irr = _pmremGen.GenerateIrradiance(_cubemapTexture, 64);
                                                var pre = _pmremGen.GeneratePrefilteredEnv(_cubemapTexture, cubeSize > 0 ? cubeSize : 512 );
                                                if (irr != 0) IrradianceMap = irr;
                                                else IrradianceMap = _cubemapTexture; 
                                                if (pre != 0) PrefilteredEnvMap = pre;
                                                else PrefilteredEnvMap = _cubemapTexture;
                                                usedPMREM = true;
                                                // Set prefilter max LOD
                                                if (pre != 0)
                                                {
                                                    GL.BindTexture(TextureTarget.TextureCubeMap, (int)pre);
                                                    // determine mip count
                                                    int mipCount = 0;
                                                    while (true)
                                                    {
                                                        GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, mipCount, GetTextureParameter.TextureWidth, out int wval);
                                                        if (wval <= 0) break;
                                                        mipCount++;
                                                    }
                                                    PrefilterMaxLod = Math.Max(0.0f, mipCount - 1);
                                                }
                                            }
                                        }
                                        catch { }
                                        finally { GL.BindTexture(TextureTarget.TextureCubeMap, 0); }

                                        if (!usedPMREM)
                                        {
                                            IrradianceMap = _cubemapTexture;
                                            PrefilteredEnvMap = _cubemapTexture;
                                        }
                                        BRDFLUTTexture = (uint)Engine.Rendering.TextureCache.White1x1;

                                                // Calculate max LOD based on KTX mipmap levels (assume reasonable default)
                                                // Only set a default if PrefilterMaxLod hasn't been set by PMREM generation above.
                                                try {
                                                    if (PrefilterMaxLod <= 0.0f) {
                                                        var prev = PrefilterMaxLod;
                                                        PrefilterMaxLod = 6.0f; // KTX files from cmgen typically have 6-7 mip levels
                                                        try { Console.WriteLine($"[SkyboxRenderer] PrefilterMaxLod was {prev}, defaulting to {PrefilterMaxLod}"); } catch { }
                                                    } else {
                                                        try { Console.WriteLine($"[SkyboxRenderer] PrefilterMaxLod preserved at {PrefilterMaxLod}"); } catch { }
                                                    }
                                                } catch { }

                                        // Log the assignment
                                        _lastLoggedCubemapHandle = _cubemapTexture;
                                        _lastLoggedKtxPath = textureRecord.Path;
                                        _ktxPendingLogged = false;
                                        try { Console.WriteLine($"[SkyboxRenderer] Assigned KTX cubemap handle={handle} path={textureRecord.Path}"); } catch { }
                                    }
                                    // ELSE: Same handle, already assigned, IBL already set - do nothing
                                }
                                else
                                {
                                    // KTX still loading or invalid (White1x1 placeholder)
                                    // Attempt to find a fallback equirectangular source (HDR/EXR) or PNG faces
                                    bool loadedFallback = TryLoadFallbackFromKtxFolder(textureRecord.Path);

                                    if (!loadedFallback)
                                    {
                                        // Use procedural skybox as fallback AND don't set IBL yet
                                        if (_cubemapTexture == 0 || _cubemapTexture == (uint)TextureCache.White1x1)
                                            CreateProceduralSkybox(Vector3.One * 0.7f, Vector3.One * 0.3f);
                                    }

                                    // Only log the pending state once per path
                                    if (!_ktxPendingLogged || _lastLoggedKtxPath != textureRecord.Path)
                                    {
                                        _ktxPendingLogged = true;
                                        _lastLoggedKtxPath = textureRecord.Path;
                                        try { Console.WriteLine($"[SkyboxRenderer] KTX cubemap pending or invalid: {textureRecord.Path}"); } catch { }
                                    }
                                }
                            }
                            catch
                            {
                                if (_cubemapTexture == 0)
                                    CreateProceduralSkybox(Vector3.One * 0.7f, Vector3.One * 0.3f);
                            }
                        }
                        else
                        {
                            // For regular textures, use the fallback approach
                            if (_cubemapTexture == 0)
                            {
                                CreateProceduralSkybox(Vector3.One * 0.7f, Vector3.One * 0.3f);
                            }
                        }
                    }
                    else
                    {
                        CreateProceduralSkybox(Vector3.One * 0.7f, Vector3.One * 0.3f);
                    }
                }
                else if (_cubemapTexture == 0)
                {
                    // Fallback to procedural if no cubemap specified or loaded
                    CreateProceduralSkybox(Vector3.One * 0.7f, Vector3.One * 0.3f);
                }

                var modTint = tint * tintMul;
                float modExposure = skyboxMaterial.CubemapExposure * Math.Max(0.0f, exposureMul);

                Render(view, projection, modTint, modExposure, entityId);
            }
            catch (Exception)
            {

                // Fallback to procedural
                try
                {
                    CreateProceduralSkybox(Vector3.One * 0.7f, Vector3.One * 0.3f);
                    Render(view, projection, tint * tintMul, 1.0f * exposureMul, entityId);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Try to find a reasonable fallback equirectangular source for a given KTX path.
        /// Searches the KTX's folder for an .hdr/.exr file and attempts to convert and use it.
        /// Returns true if a fallback was found and loaded as the current cubemap.
        /// </summary>
        private readonly HashSet<string> _ktxFallbackTried = new();

        private bool TryLoadFallbackFromKtxFolder(string ktxPath)
        {
            try
            {
            if (_ktxFallbackTried.Contains(ktxPath)) return false;
                var dir = System.IO.Path.GetDirectoryName(ktxPath);
                if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) return false;

                // Look for HDR/EXR in same folder
                var hdrs = System.IO.Directory.GetFiles(dir, "*.hdr", System.IO.SearchOption.TopDirectoryOnly);
                if (hdrs.Length > 0)
                {
                    try { Console.WriteLine($"[SkyboxRenderer] Found .hdr fallback for KTX: {hdrs[0]}"); } catch { }
                    LoadHDREquirectangular(hdrs[0], 0.0f, PanoramicMapping.Latitude_Longitude_Layout, false, PanoramicImageType.Degrees360, 1.0f);
                    return true;
                }
                var exrs = System.IO.Directory.GetFiles(dir, "*.exr", System.IO.SearchOption.TopDirectoryOnly);
                if (exrs.Length > 0)
                {
                    try { Console.WriteLine($"[SkyboxRenderer] Found .exr fallback for KTX: {exrs[0]}"); } catch { }
                    LoadHDREquirectangular(exrs[0], 0.0f, PanoramicMapping.Latitude_Longitude_Layout, false, PanoramicImageType.Degrees360, 1.0f);
                    return true;
                }

                // Search parent folder for HDRs (fallback for Generated/Env -> look one up)
                var parent = System.IO.Path.GetDirectoryName(dir);
                if (!string.IsNullOrEmpty(parent) && System.IO.Directory.Exists(parent))
                {
                    var parentHdrs = System.IO.Directory.GetFiles(parent, "*.hdr", System.IO.SearchOption.TopDirectoryOnly);
                    if (parentHdrs.Length > 0)
                    {
                        try { Console.WriteLine($"[SkyboxRenderer] Found parent .hdr fallback for KTX: {parentHdrs[0]}"); } catch { }
                        LoadHDREquirectangular(parentHdrs[0], 0.0f, PanoramicMapping.Latitude_Longitude_Layout, false, PanoramicImageType.Degrees360, 1.0f);
                        return true;
                    }
                }

                // No HDR/EXR found; try PNG faces (*.png) if present
                var pngs = System.IO.Directory.GetFiles(dir, "*.png", System.IO.SearchOption.TopDirectoryOnly);
                if (pngs.Length >= 6)
                {
                    // Attempt to detect six-side cubemap PNG naming; try to find posx/negx etc.
                    var posx = System.IO.Directory.GetFiles(dir, "*posx*.png", System.IO.SearchOption.TopDirectoryOnly);
                    var negx = System.IO.Directory.GetFiles(dir, "*negx*.png", System.IO.SearchOption.TopDirectoryOnly);
                    var posy = System.IO.Directory.GetFiles(dir, "*posy*.png", System.IO.SearchOption.TopDirectoryOnly);
                    var negy = System.IO.Directory.GetFiles(dir, "*negy*.png", System.IO.SearchOption.TopDirectoryOnly);
                    var posz = System.IO.Directory.GetFiles(dir, "*posz*.png", System.IO.SearchOption.TopDirectoryOnly);
                    var negz = System.IO.Directory.GetFiles(dir, "*negz*.png", System.IO.SearchOption.TopDirectoryOnly);
                    var candidateList = new List<string>();
                    if (posx.Length > 0) candidateList.Add(posx[0]); else if (pngs.Length > 0) candidateList.Add(pngs[0]);
                    if (negx.Length > 0) candidateList.Add(negx[0]); else if (pngs.Length > 1) candidateList.Add(pngs[1]);
                    if (posy.Length > 0) candidateList.Add(posy[0]); else if (pngs.Length > 2) candidateList.Add(pngs[2]);
                    if (negy.Length > 0) candidateList.Add(negy[0]); else if (pngs.Length > 3) candidateList.Add(pngs[3]);
                    if (posz.Length > 0) candidateList.Add(posz[0]); else if (pngs.Length > 4) candidateList.Add(pngs[4]);
                    if (negz.Length > 0) candidateList.Add(negz[0]); else if (pngs.Length > 5) candidateList.Add(pngs[5]);

                    if (candidateList.Count == 6)
                    {
                        try { Console.WriteLine($"[SkyboxRenderer] Found 6 PNG faces fallback for KTX in {dir}"); } catch { }
                        // Use the 6 PNGs to create a cubemap by loading them via TextureCache and building a cubemap
                        // We can call a helper to load six-sided from files to a cubemap
                        var tGuidList = new List<Guid>();
                        foreach (var p in candidateList)
                        {
                            var guid = Engine.Assets.AssetDatabase.TryGetByPath(p, out var rec) ? rec.Guid : Guid.Empty;
                            tGuidList.Add(guid);
                        }

                        // If we have valid guids, attempt to create a 6-sides cubemap from the files
                        try
                        {
                            // Create cubemap from 6 PNGs using CPU load (TextureCache) - simple approach
                            // cubeSize not required; removed unused variable
                            byte[][] faceBytes = new byte[6][];
                            int w = 0, h = 0;
                            for (int i = 0; i < 6; i++)
                            {
                                var path = candidateList[i];
                                using var fs2 = System.IO.File.OpenRead(path);
                                var img = StbImageSharp.ImageResult.FromStream(fs2, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                                faceBytes[i] = img.Data;
                                w = img.Width; h = img.Height;
                            }

                            // Create an empty cubemap and upload faces
                            if (w > 0 && h > 0)
                            {
                                if (_cubemapTexture != 0 && !string.IsNullOrEmpty(_currentEquirectangularPath))
                                {
                                    GL.DeleteTexture(_cubemapTexture);
                                }
                                int newCubemap = GL.GenTexture();
                                GL.BindTexture(TextureTarget.TextureCubeMap, newCubemap);
                                GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                                for (int f = 0; f < 6; f++)
                                {
                                    var target = TextureTarget.TextureCubeMapPositiveX + f;
                                    GL.TexImage2D(target, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, faceBytes[f]);
                                }
                                GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);
                                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
                                GL.BindTexture(TextureTarget.TextureCubeMap, 0);

                                _cubemapTexture = (uint)newCubemap;
                                _currentEquirectangularPath = null; // mark as TextureCache-managed equivalence
                                IrradianceMap = _cubemapTexture; PrefilteredEnvMap = _cubemapTexture; BRDFLUTTexture = (uint)TextureCache.White1x1;
                                PrefilterMaxLod = 6.0f;
                                try { Console.WriteLine($"[SkyboxRenderer] Loaded fallback six-sided cubemap from PNG faces in {dir}"); } catch { }
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            try { Console.WriteLine($"[SkyboxRenderer] Failed to build cubemap from PNG faces: {ex.Message}"); } catch { }
                        }
                    }
                }

                // If no local HDR/EXR found, try to find the original source HDR in Index (Assets/HDRI)
                var baseFolder = System.IO.Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(baseFolder))
                {
                    foreach (var rec in Engine.Assets.AssetDatabase.All())
                    {
                        if (!string.Equals(rec.Type, "TextureHDR", StringComparison.OrdinalIgnoreCase)) continue;
                        if (rec.Path.Contains(baseFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            try { Console.WriteLine($"[SkyboxRenderer] Found HDR in AssetDatabase by name match: {rec.Path}"); } catch { }
                            LoadHDREquirectangular(rec.Path, 0.0f, PanoramicMapping.Latitude_Longitude_Layout, false, PanoramicImageType.Degrees360, 1.0f);
                            _ktxFallbackTried.Add(ktxPath);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[SkyboxRenderer] TryLoadFallbackFromKtxFolder exception: {ex.Message}"); } catch { }
            }
            return false;
        }
        
        private void RenderSixSided(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul, uint entityId = 0)
        {
            var tint = new Vector3(skyboxMaterial.SixSidedTint[0], skyboxMaterial.SixSidedTint[1], skyboxMaterial.SixSidedTint[2]);

            try
            {
                // Check if we have any six-sided textures specified
                var textureGuids = new[] {
                    skyboxMaterial.RightTexture,  // +X
                    skyboxMaterial.LeftTexture,   // -X
                    skyboxMaterial.UpTexture,     // +Y
                    skyboxMaterial.DownTexture,   // -Y
                    skyboxMaterial.FrontTexture,  // +Z
                    skyboxMaterial.BackTexture    // -Z
                };

                bool hasAnyTextures = textureGuids.Any(guid => guid.HasValue && guid.Value != Guid.Empty);

                if (hasAnyTextures)
                {
                    // Load each texture and create a cubemap from the 6 sides
                    LoadSixSidedCubemap(textureGuids);
                }
                else
                {
                    // No textures specified, create procedural fallback
                    if (_cubemapTexture == 0)
                    {
                        CreateProceduralSkybox(Vector3.One * 0.6f, Vector3.One * 0.2f);
                    }
                }

                var modTint = tint * tintMul;
                float modExposure = skyboxMaterial.SixSidedExposure * Math.Max(0.0f, exposureMul);
                Render(view, projection, modTint, modExposure, entityId);
            }
            catch (Exception)
            {

                // Fallback to procedural
                try
                {
                    CreateProceduralSkybox(Vector3.One * 0.6f, Vector3.One * 0.2f);
                    Render(view, projection, tint * tintMul, 1.0f * exposureMul, entityId);
                }
                catch
                {
                }
            }
        }
        
        private void RenderPanoramic(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul, uint entityId = 0)
        {
            var tint = new Vector3(skyboxMaterial.PanoramicTint[0], skyboxMaterial.PanoramicTint[1], skyboxMaterial.PanoramicTint[2]);

            try
            {
                // Check if we have a panoramic texture specified
                if (skyboxMaterial.PanoramicTexture.HasValue && skyboxMaterial.PanoramicTexture.Value != Guid.Empty)
                {
                    // Try to get texture path from asset database
                    if (Engine.Assets.AssetDatabase.TryGet(skyboxMaterial.PanoramicTexture.Value, out var textureRecord))
                    {

                        // Check if it's an HDR file
                        if (textureRecord.Path.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase))
                        {
                            // Load HDR as cubemap (equirectangular to cubemap conversion), respect panoramic mapping and rotation
                            LoadHDREquirectangular(textureRecord.Path, skyboxMaterial.PanoramicRotation, skyboxMaterial.Mapping, skyboxMaterial.MirrorOnBack, skyboxMaterial.ImageType, skyboxMaterial.PanoramicExposure);
                        }
                        else
                        {
                            if (_cubemapTexture == 0)
                            {
                                CreateProceduralSkybox(Vector3.One * 0.8f, Vector3.One * 0.4f);
                            }
                        }
                    }
                    else
                    {
                        CreateProceduralSkybox(Vector3.One * 0.8f, Vector3.One * 0.4f);
                    }
                }
                else if (_cubemapTexture == 0)
                {
                    // Fallback to procedural if no panoramic texture specified
                    CreateProceduralSkybox(Vector3.One * 0.8f, Vector3.One * 0.4f);
                }

                var modTint = tint * tintMul;
                float modExposure = skyboxMaterial.PanoramicExposure * Math.Max(0.0f, exposureMul);
                Render(view, projection, modTint, modExposure, entityId);
            }
            catch (Exception)
            {

                // Fallback to procedural
                try
                {
                    CreateProceduralSkybox(Vector3.One * 0.8f, Vector3.One * 0.4f);
                    Render(view, projection, tint * tintMul, 1.0f * exposureMul, entityId);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Get current lighting state for sun direction
        /// </summary>
        private static LightingState? GetCurrentLightingState()
        {
            return CurrentLightingState;
        }

        /// <summary>
        /// Check if the skybox renderer is properly initialized
        /// </summary>
        public bool IsInitialized => _shader != null && _vao != 0 && _vbo != 0;

        /// <summary>
        /// Check if a cubemap texture is loaded
        /// </summary>
        public bool HasCubemapTexture => _cubemapTexture != 0;

        /// <summary>
        /// Force reload of skybox shader (useful for hot-reloading during development)
        /// </summary>
        public void ReloadShader()
        {
            _shader?.Dispose();
            _shader = null;
            _pmremGen?.Dispose();
            _pmremGen = null;

            try
            {
                _shader = ShaderProgram.FromFiles(
                    "Engine/Rendering/Shaders/skybox.vert",
                    "Engine/Rendering/Shaders/skybox.frag"
                );
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Load 6 separate textures and create a cubemap from them
        /// </summary>
        /// <param name="textureGuids">Array of 6 GUIDs: [+X, -X, +Y, -Y, +Z, -Z]</param>
        private void LoadSixSidedCubemap(Guid?[] textureGuids)
        {
            if (textureGuids.Length != 6)
            {
                Console.WriteLine("[SkyboxRenderer] LoadSixSidedCubemap: Expected 6 textures, got " + textureGuids.Length);
                return;
            }

            try
            {
                // Load all 6 face textures using TextureCache
                int[] faceHandles = new int[6];
                int width = 0, height = 0;
                bool allLoaded = true;

                for (int i = 0; i < 6; i++)
                {
                    if (textureGuids[i].HasValue && textureGuids[i]!.Value != Guid.Empty)
                    {
                        Guid guid = textureGuids[i]!.Value; // ! operator to suppress nullable warning
                        int handle = TextureCache.GetOrLoad(guid, g =>
                        {
                            if (Engine.Assets.AssetDatabase.TryGet(g, out var r)) return r.Path;
                            return null;
                        });

                        faceHandles[i] = handle;

                        // Check if any textures are still pending
                        if (TextureCache.IsPending(guid))
                        {
                            allLoaded = false;
                        }
                    }
                    else
                    {
                        faceHandles[i] = TextureCache.White1x1; // Use white for missing faces
                    }
                }

                // If not all textures are loaded yet, use procedural fallback and return
                if (!allLoaded)
                {
                    if (_cubemapTexture == 0)
                    {
                        CreateProceduralSkybox(Vector3.One * 0.6f, Vector3.One * 0.2f);
                    }
                    return;
                }

                // All textures loaded - create a cubemap from the 6 faces
                // Delete old cubemap if we created it ourselves (not from TextureCache)
                if (_cubemapTexture != 0 && _currentEquirectangularPath != null)
                {
                    GL.DeleteTexture((int)_cubemapTexture);
                }

                // Create new cubemap
                uint cubemap = (uint)GL.GenTexture();
                GL.BindTexture(TextureTarget.TextureCubeMap, (int)cubemap);

                // Copy each face texture to the cubemap
                for (int i = 0; i < 6; i++)
                {
                    if (faceHandles[i] == TextureCache.White1x1)
                    {
                        // Use white 1x1 for this face
                        GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, PixelInternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new byte[] { 255, 255, 255, 255 });
                    }
                    else
                    {
                        // Get texture data from the loaded face
                        // Note: This is a simplified approach - we're just binding the existing texture
                        // In a full implementation, we'd need to copy pixels from the source texture
                        // For now, we'll use glCopyImageSubData if available, or read back pixels

                        // Get the size of the source texture
                        GL.BindTexture(TextureTarget.Texture2D, faceHandles[i]);
                        GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int w);
                        GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int h);

                        if (width == 0) { width = w; height = h; }

                        // Read pixels from source texture and upload to cubemap face
                        byte[] pixels = new byte[w * h * 4];
                        GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

                        GL.BindTexture(TextureTarget.TextureCubeMap, (int)cubemap);
                        GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                    }
                }

                // Set cubemap parameters
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

                // Unbind
                GL.BindTexture(TextureTarget.TextureCubeMap, 0);

                // Assign to _cubemapTexture
                _cubemapTexture = cubemap;
                _cubemapTextureGuid = Guid.Empty; // Not from TextureCache
                _currentEquirectangularPath = "six-sided"; // Mark as six-sided to differentiate

                // Set IBL resources
                IrradianceMap = _cubemapTexture;
                PrefilteredEnvMap = _cubemapTexture;
                BRDFLUTTexture = (uint)TextureCache.White1x1;

                // Try to generate mipmaps so that roughness sampling can use LODs
                try {
                    GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);
                    GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                    // Compute max LOD from size
                    PrefilterMaxLod = (float)Math.Floor(Math.Log(width, 2));

                    // If PMREM generator is available, create high-quality prefiltered env map
                    if (_pmremGen != null)
                    {
                        try {
                            // Use 64x64 for better quality (eliminates white spots on matte materials)
                            var irr = _pmremGen.GenerateIrradiance(_cubemapTexture, 64);
                            var pre = _pmremGen.GeneratePrefilteredEnv(_cubemapTexture, width);
                            if (irr != 0) IrradianceMap = irr; else IrradianceMap = _cubemapTexture;
                            if (pre != 0)
                            {
                                PrefilteredEnvMap = pre;
                                // determine mip count
                                int mipCount = 0;
                                while (true)
                                {
                                    GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, mipCount, GetTextureParameter.TextureWidth, out int wval);
                                    if (wval <= 0) break;
                                    mipCount++;
                                }
                                PrefilterMaxLod = Math.Max(0.0f, mipCount - 1);
                            }
                        } catch { }
                    }

                    GL.BindTexture(TextureTarget.TextureCubeMap, 0);
                } catch { }

                try { Console.WriteLine($"[SkyboxRenderer] Created six-sided cubemap: {width}x{height} PrefilterMaxLod={PrefilterMaxLod}"); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkyboxRenderer] Failed to load six-sided cubemap: {ex.Message}");

                // Fallback to procedural
                if (_cubemapTexture == 0)
                {
                    CreateProceduralSkybox(Vector3.One * 0.6f, Vector3.One * 0.2f);
                }
            }
        }

        /// <summary>
        /// Load an HDR equirectangular texture and convert it to cubemap for skybox use
        /// </summary>
        /// <param name="hdrTexturePath">Path to the HDR texture file</param>
        public void LoadHDREquirectangular(string hdrTexturePath, float rotationDeg = 0.0f, PanoramicMapping mapping = PanoramicMapping.Latitude_Longitude_Layout, bool mirrorOnBack = false, PanoramicImageType imageType = PanoramicImageType.Degrees360, float exposure = 1.0f)
        {

            if (!File.Exists(hdrTexturePath))
            {
                return;
            }

            try
            {
                // If we already loaded this exact HDR into the current cubemap with same parameters, skip regeneration.
                var key = hdrTexturePath + "|rot:" + rotationDeg.ToString("F2") + "|map:" + mapping.ToString() + "|mirror:" + (mirrorOnBack ? "1" : "0") + "|type:" + imageType.ToString() + "|exp:" + exposure.ToString("F2");
                if (!string.IsNullOrEmpty(_currentEquirectangularPath) && string.Equals(_currentEquirectangularPath, key, StringComparison.OrdinalIgnoreCase) && _cubemapTexture != 0)
                {
                    return; // already loaded
                }
                // Clean up existing texture ONLY if we created it ourselves (HDR conversion creates owned textures)
                // TextureCache-managed textures (when _currentEquirectangularPath is null) should NOT be deleted
                if (_cubemapTexture != 0 && !string.IsNullOrEmpty(_currentEquirectangularPath))
                {
                    // We created this texture (equirectangular conversion), so we own it and can delete it
                    GL.DeleteTexture(_cubemapTexture);
                    _cubemapTexture = 0;
                }
                else if (_cubemapTexture != 0)
                {
                    // Texture is from TextureCache, just clear our reference
                    _cubemapTexture = 0;
                }

                // Reset logging state so future assignments are logged
                _lastLoggedCubemapHandle = 0;
                _lastLoggedKtxPath = null;
                _ktxPendingLogged = false;

                // Load HDR texture with optimized memory usage
                var (textureData, imageData, handle) = LoadHDRTextureOptimized(hdrTexturePath);

                try
                {
                    // Create cubemap directly without temporary 2D texture
                    _cubemapTexture = (uint)GL.GenTexture();
                    GL.BindTexture(TextureTarget.TextureCubeMap, (int)_cubemapTexture);

                    // Use higher cubemap size to match source HDR quality (1024 for 4K sources)
                    // Unity typically uses 512-2048 depending on quality settings
                    int cubeSize = Math.Min(1024, Math.Max(textureData.Width, textureData.Height) / 2);

                    // Create optimized cubemap using the provided rotation/mapping parameters
                        CreateOptimizedCubemapFromHDR(textureData, imageData, cubeSize, rotationDeg, mapping, mirrorOnBack, imageType, exposure);

                    // remember which source produced the cubemap so we don't rebuild each frame
                    _currentEquirectangularPath = key;

                    // Generate mipmaps so we can approximate prefiltered environment by sampling LODs
                    GL.GenerateMipmap(GenerateMipmapTarget.TextureCubeMap);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

                    GL.BindTexture(TextureTarget.TextureCubeMap, 0);

                    // Calculate maximum mipmap level for proper IBL sampling
                    // For a 1024x1024 cubemap: log2(1024) = 10 mipmaps (0-9)
                    PrefilterMaxLod = (float)Math.Floor(Math.Log(cubeSize, 2));

                    // Expose IBL resources to the rest of the engine
                    // Try to create PMREM maps for better IBL if available
                    if (_pmremGen != null)
                    {
                        try
                        {
                            // Use 64x64 for better quality (eliminates white spots on matte materials)
                            var irr = _pmremGen.GenerateIrradiance(_cubemapTexture, 64);
                            var pre = _pmremGen.GeneratePrefilteredEnv(_cubemapTexture, cubeSize);
                            if (irr != 0) IrradianceMap = irr; else IrradianceMap = _cubemapTexture;
                            if (pre != 0)
                            {
                                PrefilteredEnvMap = pre;
                                // Determine mip count for pre
                                int mipCount = 0;
                                while (true)
                                {
                                    GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, mipCount, GetTextureParameter.TextureWidth, out int wval);
                                    if (wval <= 0) break;
                                    mipCount++;
                                }
                                PrefilterMaxLod = Math.Max(0.0f, mipCount - 1);
                            }
                            else
                            {
                                PrefilteredEnvMap = _cubemapTexture;
                            }
                        }
                        catch { IrradianceMap = _cubemapTexture; PrefilteredEnvMap = _cubemapTexture; }
                    }
                    else
                    {
                        IrradianceMap = _cubemapTexture;
                        PrefilteredEnvMap = _cubemapTexture;
                    }
                    // BRDF LUT fallback to white 1x1 until a proper LUT is generated/imported
                    BRDFLUTTexture = (uint)Engine.Rendering.TextureCache.White1x1;

                }
                finally
                {
                    // Always clean up pinned memory
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }
            catch (Exception)
            {
                // Fall back to procedural skybox
                CreateProceduralSkybox(new Vector3(0.7f, 0.9f, 1.0f), new Vector3(0.3f, 0.3f, 0.4f));
            }
        }

        /// <summary>
        /// Load HDR texture with optimized memory management
        /// </summary>
        private (TextureData data, float[] imageData, System.Runtime.InteropServices.GCHandle handle) LoadHDRTextureOptimized(string path)
        {
            using var fs = File.OpenRead(path);
            // Use the float loader to preserve HDR values (no tonemapping to 8-bit)
            var img = StbImageSharp.ImageResultFloat.FromStream(fs, StbImageSharp.ColorComponents.RedGreenBlue);

            // Copy float data
            var imageDataCopy = new float[img.Data.Length];
            Array.Copy(img.Data, imageDataCopy, img.Data.Length);

            var data = new TextureData
            {
                Width = img.Width,
                Height = img.Height,
                InternalFormat = PixelInternalFormat.Rgb16f,
                Format = PixelFormat.Rgb,
                Type = PixelType.Float
            };

            // Pin memory temporarily for OpenGL usage
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(imageDataCopy, System.Runtime.InteropServices.GCHandleType.Pinned);
            data.Data = handle.AddrOfPinnedObject();

            return (data, imageDataCopy, handle);
        }

        /// <summary>
        /// Create optimized cubemap from HDR data with reduced memory usage
        /// </summary>
        private void CreateOptimizedCubemapFromHDR(TextureData textureData, float[] imageData, int cubeSize, float rotationDeg = 0.0f, PanoramicMapping mapping = PanoramicMapping.Latitude_Longitude_Layout, bool mirrorOnBack = false, PanoramicImageType imageType = PanoramicImageType.Degrees360, float exposure = 1.0f)
        {
            // Convert the equirectangular source into 6 cubemap faces using CPU sampling.
            // Produce float RGB data and upload as RGB16F so we keep HDR range. Mipmaps will be generated later.
            for (int face = 0; face < 6; face++)
            {
                var target = TextureTarget.TextureCubeMapPositiveX + face;
                var faceData = CreateCubeFaceFromEquirectangularFloat(face, cubeSize, textureData.Width, textureData.Height, imageData, rotationDeg, mapping, mirrorOnBack, imageType, exposure);

                GL.TexImage2D(target, 0, PixelInternalFormat.Rgb16f, cubeSize, cubeSize, 0,
                    PixelFormat.Rgb, PixelType.Float, faceData);
            }
        }

        /// <summary>
        /// Create optimized cube face with better memory usage and HDR-like appearance
        /// </summary>
        private byte[] CreateCubeFaceFromEquirectangular(int faceIndex, int size, int srcW, int srcH, byte[] src, float rotationDeg = 0.0f, PanoramicMapping mapping = PanoramicMapping.Latitude_Longitude_Layout, bool mirrorOnBack = false, PanoramicImageType imageType = PanoramicImageType.Degrees360, float exposure = 1.0f)
        {
            var data = new byte[size * size * 4];

            // Precompute rotation if any
            float cosR = 1.0f, sinR = 0.0f;
            if (Math.Abs(rotationDeg) > 0.001f)
            {
                float rad = MathF.PI * rotationDeg / 180.0f;
                cosR = MathF.Cos(rad);
                sinR = MathF.Sin(rad);
            }

            // For each texel on the cube face, compute the 3D direction and sample the equirectangular map.
            for (int y = 0; y < size; y++)
            {
                // v in [-1,1], top->bottom
                float v = 2.0f * ((y + 0.5f) / size) - 1.0f;
                for (int x = 0; x < size; x++)
                {
                    float u = 2.0f * ((x + 0.5f) / size) - 1.0f; // u in [-1,1]

                    // Build direction vector for this face (standard OpenGL cubemap orientation)
                    Vector3 dir = faceIndex switch
                    {
                        0 => new Vector3(1.0f, v, -u),   // +X
                        1 => new Vector3(-1.0f, v, u),   // -X
                        // Swap Y faces so top and bottom are not inverted
                        2 => new Vector3(u, -1.0f, v),   // +Y (swapped)
                        3 => new Vector3(u, 1.0f, -v),   // -Y (swapped)
                        4 => new Vector3(u, v, 1.0f),    // +Z
                        5 => new Vector3(-u, v, -1.0f),  // -Z
                        _ => new Vector3(u, v, 1.0f)
                    };

                    // Apply rotation around Y axis if requested
                    if (Math.Abs(rotationDeg) > 0.001f)
                    {
                        float rx = dir.X;
                        float rz = dir.Z;
                        dir.X = cosR * rx - sinR * rz;
                        dir.Z = sinR * rx + cosR * rz;
                    }

                    dir = dir.Normalized();

                    // Convert direction to equirectangular UV
                    float theta = (float)Math.Atan2(dir.Z, dir.X); // -PI .. PI
                    float phi = (float)Math.Acos(Math.Clamp(dir.Y, -1.0f, 1.0f)); // 0..PI

                    float texU = theta / (2.0f * (float)Math.PI) + 0.5f;
                    float texV = phi / (float)Math.PI; // 0..1

                    // Basic handling for mirrored-back or 180-degree images
                    if (mirrorOnBack && dir.Z < 0)
                    {
                        texU = 1.0f - texU;
                    }

                    if (imageType == PanoramicImageType.Degrees180)
                    {
                        // For 180 images, clamp vertical range to top hemisphere
                        texV = Math.Clamp(texV, 0f, 0.5f);
                    }

                    // Bilinear sample from source (assumed RGBA8)
                    var col = SampleEquirectangularRGBA(src, srcW, srcH, texU, texV);

                    // Apply exposure scale so generated cubemap respects material exposure
                    float rf = Math.Clamp(col.r / 255f * exposure, 0f, 1f);
                    float gf = Math.Clamp(col.g / 255f * exposure, 0f, 1f);
                    float bf = Math.Clamp(col.b / 255f * exposure, 0f, 1f);
                    byte rb = (byte)Math.Clamp((int)Math.Round(rf * 255.0f), 0, 255);
                    byte gb = (byte)Math.Clamp((int)Math.Round(gf * 255.0f), 0, 255);
                    byte bb = (byte)Math.Clamp((int)Math.Round(bf * 255.0f), 0, 255);

                    int idx = (y * size + x) * 4;
                    data[idx] = rb;
                    data[idx + 1] = gb;
                    data[idx + 2] = bb;
                    data[idx + 3] = col.a;
                }
            }

            return data;
        }

        /// <summary>
        /// Create cube face data as floats (RGB) by sampling the equirectangular source.
        /// Each component is in linear 0..1 range and multiplied by exposure.
        /// </summary>
        private float[] CreateCubeFaceFromEquirectangularFloat(int faceIndex, int size, int srcW, int srcH, float[] src, float rotationDeg = 0.0f, PanoramicMapping mapping = PanoramicMapping.Latitude_Longitude_Layout, bool mirrorOnBack = false, PanoramicImageType imageType = PanoramicImageType.Degrees360, float exposure = 1.0f)
        {
            var data = new float[size * size * 3];

            // Precompute rotation if any
            float cosR = 1.0f, sinR = 0.0f;
            if (Math.Abs(rotationDeg) > 0.001f)
            {
                float rad = MathF.PI * rotationDeg / 180.0f;
                cosR = MathF.Cos(rad);
                sinR = MathF.Sin(rad);
            }

            for (int y = 0; y < size; y++)
            {
                float v = 2.0f * ((y + 0.5f) / size) - 1.0f;
                for (int x = 0; x < size; x++)
                {
                    float u = 2.0f * ((x + 0.5f) / size) - 1.0f;

                    // Build direction vector for cubemap face (match Unity's orientation)
                    // Unity uses right-handed coordinate system
                    // For Y faces: u maps to X axis, v maps to Z axis (top) or -Z axis (bottom)
                    Vector3 dir = faceIndex switch
                    {
                        0 => new Vector3(1.0f, v, u),      // +X (right): looking +X, u→Z, v→Y
                        1 => new Vector3(-1.0f, v, -u),    // -X (left): looking -X, u→-Z, v→Y
                        2 => new Vector3(u, -1.0f, -v),    // +Y (top): looking -Y, u→X, v→-Z
                        3 => new Vector3(u, 1.0f, v),      // -Y (bottom): looking +Y, u→X, v→Z
                        4 => new Vector3(u, v, -1.0f),     // +Z (front): looking +Z, u→X, v→Y
                        5 => new Vector3(-u, v, 1.0f),     // -Z (back): looking -Z, u→-X, v→Y
                        _ => new Vector3(u, v, 1.0f)
                    };

                    if (Math.Abs(rotationDeg) > 0.001f)
                    {
                        float rx = dir.X;
                        float rz = dir.Z;
                        dir.X = cosR * rx - sinR * rz;
                        dir.Z = sinR * rx + cosR * rz;
                    }

                    dir = dir.Normalized();

                    float theta = (float)Math.Atan2(dir.Z, dir.X);
                    float phi = (float)Math.Acos(Math.Clamp(dir.Y, -1.0f, 1.0f));

                    float texU = theta / (2.0f * (float)Math.PI) + 0.5f;
                    float texV = phi / (float)Math.PI;

                    if (mirrorOnBack && dir.Z < 0)
                    {
                        texU = 1.0f - texU;
                    }

                    if (imageType == PanoramicImageType.Degrees180)
                    {
                        texV = Math.Clamp(texV, 0f, 0.5f);
                    }

                    // Bilinear sample from source (float RGB HDR)
                    (float rfS, float gfS, float bfS) = SampleEquirectangularRGBFloat(src, srcW, srcH, texU, texV);

                    // DON'T apply exposure here - let the shader do it to avoid double-application
                    // Keep HDR values as-is for proper IBL calculations
                    float rf = Math.Clamp(rfS, 0f, 65504f);
                    float gf = Math.Clamp(gfS, 0f, 65504f);
                    float bf = Math.Clamp(bfS, 0f, 65504f);

                    int idx = (y * size + x) * 3;
                    data[idx] = rf;
                    data[idx + 1] = gf;
                    data[idx + 2] = bf;
                }
            }

            return data;
        }

        private (byte r, byte g, byte b, byte a) SampleEquirectangularRGBA(byte[] src, int w, int h, float u, float v)
        {
            // Wrap u horizontally
            u = u - (float)Math.Floor(u);
            v = Math.Clamp(v, 0f, 1f);

            float fx = u * (w - 1);
            float fy = v * (h - 1);
            int x0 = (int)Math.Floor(fx);
            int x1 = (x0 + 1) % w;
            int y0 = (int)Math.Floor(fy);
            int y1 = Math.Min(h - 1, y0 + 1);

            float sx = fx - x0;
            float sy = fy - y0;

            int i00 = (y0 * w + x0) * 4;
            int i10 = (y0 * w + x1) * 4;
            int i01 = (y1 * w + x0) * 4;
            int i11 = (y1 * w + x1) * 4;

            // Read and lerp per channel
            float r00 = src[i00] / 255f; float g00 = src[i00 + 1] / 255f; float b00 = src[i00 + 2] / 255f; float a00 = src[i00 + 3] / 255f;
            float r10 = src[i10] / 255f; float g10 = src[i10 + 1] / 255f; float b10 = src[i10 + 2] / 255f; float a10 = src[i10 + 3] / 255f;
            float r01 = src[i01] / 255f; float g01 = src[i01 + 1] / 255f; float b01 = src[i01 + 2] / 255f; float a01 = src[i01 + 3] / 255f;
            float r11 = src[i11] / 255f; float g11 = src[i11 + 1] / 255f; float b11 = src[i11 + 2] / 255f; float a11 = src[i11 + 3] / 255f;

            float r0 = r00 * (1 - sx) + r10 * sx;
            float g0 = g00 * (1 - sx) + g10 * sx;
            float b0 = b00 * (1 - sx) + b10 * sx;
            float a0 = a00 * (1 - sx) + a10 * sx;

            float r1 = r01 * (1 - sx) + r11 * sx;
            float g1 = g01 * (1 - sx) + g11 * sx;
            float b1 = b01 * (1 - sx) + b11 * sx;
            float a1 = a01 * (1 - sx) + a11 * sx;

            float rf = r0 * (1 - sy) + r1 * sy;
            float gf = g0 * (1 - sy) + g1 * sy;
            float bf = b0 * (1 - sy) + b1 * sy;
            float af = a0 * (1 - sy) + a1 * sy;

            byte rr = (byte)Math.Clamp((int)Math.Round(rf * 255.0f), 0, 255);
            byte gg = (byte)Math.Clamp((int)Math.Round(gf * 255.0f), 0, 255);
            byte bb = (byte)Math.Clamp((int)Math.Round(bf * 255.0f), 0, 255);
            byte aa = (byte)Math.Clamp((int)Math.Round(af * 255.0f), 0, 255);
            return (rr, gg, bb, aa);
        }

        /// <summary>
        /// Bilinear sample for float RGB equirectangular sources (data layout: [r,g,b,r,g,b,...])
        /// Returns linear float components (not clamped to 0..1).
        /// </summary>
        private (float r, float g, float b) SampleEquirectangularRGBFloat(float[] src, int w, int h, float u, float v)
        {
            // Wrap u horizontally
            u = u - (float)Math.Floor(u);
            v = Math.Clamp(v, 0f, 1f);

            float fx = u * (w - 1);
            float fy = v * (h - 1);
            int x0 = (int)Math.Floor(fx);
            int x1 = (x0 + 1) % w;
            int y0 = (int)Math.Floor(fy);
            int y1 = Math.Min(h - 1, y0 + 1);

            float sx = fx - x0;
            float sy = fy - y0;

            int i00 = (y0 * w + x0) * 3;
            int i10 = (y0 * w + x1) * 3;
            int i01 = (y1 * w + x0) * 3;
            int i11 = (y1 * w + x1) * 3;

            float r00 = src[i00]; float g00 = src[i00 + 1]; float b00 = src[i00 + 2];
            float r10 = src[i10]; float g10 = src[i10 + 1]; float b10 = src[i10 + 2];
            float r01 = src[i01]; float g01 = src[i01 + 1]; float b01 = src[i01 + 2];
            float r11 = src[i11]; float g11 = src[i11 + 1]; float b11 = src[i11 + 2];

            float r0 = r00 * (1 - sx) + r10 * sx;
            float g0 = g00 * (1 - sx) + g10 * sx;
            float b0 = b00 * (1 - sx) + b10 * sx;

            float r1 = r01 * (1 - sx) + r11 * sx;
            float g1 = g01 * (1 - sx) + g11 * sx;
            float b1 = b01 * (1 - sx) + b11 * sx;

            float rf = r0 * (1 - sy) + r1 * sy;
            float gf = g0 * (1 - sy) + g1 * sy;
            float bf = b0 * (1 - sy) + b1 * sy;

            return (rf, gf, bf);
        }

        /// <summary>
        /// Simple equirectangular to cubemap conversion (legacy placeholder implementation)
        /// </summary>
        private void CreateCubemapFromEquirectangular(int hdrTexture2D, int srcWidth, int srcHeight, int cubeSize)
        {
            // This is a simplified approach. A proper implementation would use a compute shader
            // or geometry shader to sample the equirectangular map correctly for each cubemap face

            for (int face = 0; face < 6; face++)
            {
                var target = TextureTarget.TextureCubeMapPositiveX + face;

                // For now, create a basic sampling of the HDR texture
                // This doesn't properly map equirectangular coordinates but will show something
                var faceData = CreateSimpleCubeFaceFromHDR(face, cubeSize);

                GL.TexImage2D(target, 0, PixelInternalFormat.Rgb16f, cubeSize, cubeSize, 0,
                    PixelFormat.Rgb, PixelType.Float, faceData);
            }
        }

        /// <summary>
        /// Create a simple cube face with HDR-like colors (placeholder)
        /// </summary>
        private float[] CreateSimpleCubeFaceFromHDR(int faceIndex, int size)
        {
            var data = new float[size * size * 3];

            // Create different colors for each face to verify the cubemap works
            Vector3 baseColor = faceIndex switch
            {
                0 => new Vector3(2.0f, 1.0f, 1.0f), // +X - Bright red (HDR values > 1.0)
                1 => new Vector3(1.0f, 2.0f, 1.0f), // -X - Bright green
                2 => new Vector3(1.0f, 1.0f, 3.0f), // +Y - Bright blue (sky)
                3 => new Vector3(0.5f, 0.3f, 0.2f), // -Y - Dark ground
                4 => new Vector3(1.5f, 1.5f, 2.0f), // +Z - Bright cyan
                5 => new Vector3(2.0f, 1.5f, 1.0f), // -Z - Bright yellow
                _ => new Vector3(1.0f, 1.0f, 1.0f)
            };

            for (int i = 0; i < size * size; i++)
            {
                data[i * 3] = baseColor.X;
                data[i * 3 + 1] = baseColor.Y;
                data[i * 3 + 2] = baseColor.Z;
            }

            return data;
        }

        /// <summary>
        /// Get debug information about the current skybox state
        /// </summary>
        public string GetDebugInfo()
        {
            return $"SkyboxRenderer Debug Info:\n" +
                   $"  Initialized: {IsInitialized}\n" +
                   $"  Has Shader: {_shader != null}\n" +
                   $"  Has Cubemap: {HasCubemapTexture}\n" +
                   $"  Texture ID: {_cubemapTexture}\n" +
                   $"  VAO: {_vao}\n" +
                   $"  VBO: {_vbo}\n" +
                   $"  Active Texture Handles: {_textureHandles.Count}";
        }

        public void Dispose()
        {
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }

            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }

            if (_cubemapTexture != 0)
            {
                GL.DeleteTexture(_cubemapTexture);
                _cubemapTexture = 0;
            }

            // Free all GC handles
            foreach (var handle in _textureHandles)
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
            _textureHandles.Clear();

            _shader?.Dispose();
            _shader = null;
        }

        /// <summary>
        /// Generate a BRDF integration LUT (RG16F) used by IBL for specular energy conservation.
        /// </summary>
        private void GenerateBRDFLUT()
        {
            const int size = 256;

            // Compile shader
            ShaderProgram? brdfProg = null;
            try
            {
                brdfProg = ShaderProgram.FromFiles("Engine/Rendering/Shaders/brdf_integration.vert", "Engine/Rendering/Shaders/brdf_integration.frag");
            }
            catch (Exception)
            {
                return;
            }

            // Create texture
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16f, size, size, 0, PixelFormat.Rg, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Framebuffer
            int fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.DeleteFramebuffer(fbo);
                GL.DeleteTexture(tex);
                brdfProg?.Dispose();
                return;
            }

            // Setup a simple fullscreen triangle VBO/VAO
            int quadVao = GL.GenVertexArray();
            int quadVbo = GL.GenBuffer();
            float[] verts = new float[] { -1f, -1f, 3f, -1f, -1f, 3f };
            GL.BindVertexArray(quadVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

            // Render to texture
            GL.Viewport(0, 0, size, size);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            brdfProg.Use();
            GL.BindVertexArray(quadVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            // Unbind
            GL.BindVertexArray(0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, 1, 1);

            // Cleanup
            GL.DeleteBuffer(quadVbo);
            GL.DeleteVertexArray(quadVao);
            GL.DeleteFramebuffer(fbo);

            BRDFLUTTexture = (uint)tex;

            brdfProg?.Dispose();
        }
    }
}