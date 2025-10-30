using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.IO;
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
        private readonly List<System.Runtime.InteropServices.GCHandle> _textureHandles = new();

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
        }

        /// <summary>
        /// Load a skybox from 6 face textures (Unity style)
        /// </summary>
        /// <param name="faces">Array of 6 texture paths: [right, left, top, bottom, front, back]</param>
        public void LoadSkybox(string[] faces)
        {
            if (faces.Length != 6)
                throw new ArgumentException("Skybox requires exactly 6 face textures");

            // Clean up existing texture if any
            if (_cubemapTexture != 0)
            {
                GL.DeleteTexture(_cubemapTexture);
                _cubemapTexture = 0;
            }

            _cubemapTexture = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemapTexture);

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
            // Clean up existing texture if any
            if (_cubemapTexture != 0)
            {
                GL.DeleteTexture(_cubemapTexture);
                _cubemapTexture = 0;
            }

            _cubemapTexture = (uint)GL.GenTexture();
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemapTexture);

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
        }

        /// <summary>
        /// Render the skybox (call this first in your render loop, before other objects)
        /// </summary>
    public void Render(Matrix4 view, Matrix4 projection, Vector3 tintColor, float exposure)
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

                _shader.Use();
                _shader.SetMat4("view", viewNoTranslation);
                _shader.SetMat4("projection", projection);
                _shader.SetInt("uMode", 0);
                _shader.SetVec3("tintColor", tintColor);
                _shader.SetFloat("exposure", Math.Max(0.0f, exposure));

                // Bind skybox texture
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.TextureCubeMap, _cubemapTexture);
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
        public void RenderWithMaterial(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial)
            => RenderWithMaterial(view, projection, skyboxMaterial, new Vector3(1, 1, 1), 1.0f);

        /// <summary>
        /// Render skybox with environment multipliers (Unity-like: env tint/exposure modulate material)
        /// </summary>
        public void RenderWithMaterial(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 envTintMul, float envExposureMul)
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
                        RenderProcedural(view, projection, skyboxMaterial, envTintMul, envExposureMul);
                        break;
                    case Engine.Assets.SkyboxType.Cubemap:
                        RenderCubemap(view, projection, skyboxMaterial, envTintMul, envExposureMul);
                        break;
                    case Engine.Assets.SkyboxType.SixSided:
                        RenderSixSided(view, projection, skyboxMaterial, envTintMul, envExposureMul);
                        break;
                    case Engine.Assets.SkyboxType.Panoramic:
                        RenderPanoramic(view, projection, skyboxMaterial, envTintMul, envExposureMul);
                        break;
                    default:
                        // Fallback to procedural rendering
                        RenderProcedural(view, projection, skyboxMaterial, envTintMul, envExposureMul);
                        break;
                }
            }
            catch (Exception)
            {

                // Try to render a simple fallback procedural sky
                try
                {
                    CreateProceduralSkybox(new Vector3(0.6f, 0.8f, 1.0f), new Vector3(0.2f, 0.4f, 0.6f));
                    Render(view, projection, envTintMul, envExposureMul);
                }
                catch
                {
                    // If even fallback fails, just return
                }
            }
        }
        
        private void RenderProcedural(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul)
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
                GL.BindTexture(TextureTarget.TextureCubeMap, _cubemapTexture);
                
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
            GL.BindTexture(TextureTarget.TextureCubeMap, _cubemapTexture);
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
        
        private void RenderCubemap(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul)
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
                            // Load HDR as cubemap
                            LoadHDREquirectangular(textureRecord.Path);
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
                Render(view, projection, modTint, modExposure);
            }
            catch (Exception)
            {

                // Fallback to procedural
                try
                {
                    CreateProceduralSkybox(Vector3.One * 0.7f, Vector3.One * 0.3f);
                    Render(view, projection, tint * tintMul, 1.0f * exposureMul);
                }
                catch
                {
                }
            }
        }
        
        private void RenderSixSided(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul)
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
                    // In a full implementation, you'd load each texture from the asset database
                    // and create a cubemap. For now, check if we need to rebuild the cubemap
                    if (_cubemapTexture == 0)
                    {
                        CreateProceduralSkybox(Vector3.One * 0.6f, Vector3.One * 0.2f);
                    }
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
                Render(view, projection, modTint, modExposure);
            }
            catch (Exception)
            {

                // Fallback to procedural
                try
                {
                    CreateProceduralSkybox(Vector3.One * 0.6f, Vector3.One * 0.2f);
                    Render(view, projection, tint * tintMul, 1.0f * exposureMul);
                }
                catch
                {
                }
            }
        }
        
        private void RenderPanoramic(Matrix4 view, Matrix4 projection, Engine.Assets.SkyboxMaterialAsset skyboxMaterial, Vector3 tintMul, float exposureMul)
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
                            // Load HDR as cubemap (equirectangular to cubemap conversion)
                            LoadHDREquirectangular(textureRecord.Path);
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
                Render(view, projection, modTint, modExposure);
            }
            catch (Exception)
            {

                // Fallback to procedural
                try
                {
                    CreateProceduralSkybox(Vector3.One * 0.8f, Vector3.One * 0.4f);
                    Render(view, projection, tint * tintMul, 1.0f * exposureMul);
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
        /// Load an HDR equirectangular texture and convert it to cubemap for skybox use
        /// </summary>
        /// <param name="hdrTexturePath">Path to the HDR texture file</param>
        public void LoadHDREquirectangular(string hdrTexturePath)
        {

            if (!File.Exists(hdrTexturePath))
            {
                return;
            }

            try
            {
                // Clean up existing texture if any
                if (_cubemapTexture != 0)
                {
                    GL.DeleteTexture(_cubemapTexture);
                    _cubemapTexture = 0;
                }

                // Load HDR texture with optimized memory usage
                var (textureData, imageData, handle) = LoadHDRTextureOptimized(hdrTexturePath);

                try
                {
                    // Create cubemap directly without temporary 2D texture
                    _cubemapTexture = (uint)GL.GenTexture();
                    GL.BindTexture(TextureTarget.TextureCubeMap, _cubemapTexture);

                    // Use smaller cubemap size to reduce memory usage (256 instead of 512)
                    int cubeSize = 256;
                    CreateOptimizedCubemapFromHDR(textureData, imageData, cubeSize);

                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

                    GL.BindTexture(TextureTarget.TextureCubeMap, 0);

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
        private (TextureData data, byte[] imageData, System.Runtime.InteropServices.GCHandle handle) LoadHDRTextureOptimized(string path)
        {
            using var fs = File.OpenRead(path);
            var img = StbImageSharp.ImageResult.FromStream(fs, StbImageSharp.ColorComponents.RedGreenBlueAlpha);

            // For HDR files, we need to be careful about memory usage
            // Copy only the necessary data
            var imageDataCopy = new byte[img.Data.Length];
            Array.Copy(img.Data, imageDataCopy, img.Data.Length);

            var data = new TextureData
            {
                Width = img.Width,
                Height = img.Height,
                InternalFormat = PixelInternalFormat.Rgba8, // Keep as RGBA8 for now, can upgrade to HDR later
                Format = PixelFormat.Rgba,
                Type = PixelType.UnsignedByte
            };

            // Pin memory temporarily for OpenGL usage
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(imageDataCopy, System.Runtime.InteropServices.GCHandleType.Pinned);
            data.Data = handle.AddrOfPinnedObject();


            return (data, imageDataCopy, handle);
        }

        /// <summary>
        /// Create optimized cubemap from HDR data with reduced memory usage
        /// </summary>
        private void CreateOptimizedCubemapFromHDR(TextureData textureData, byte[] imageData, int cubeSize)
        {
            // Simple approach: create 6 faces based on the HDR data
            // This is still a placeholder, but with better memory management
            for (int face = 0; face < 6; face++)
            {
                var target = TextureTarget.TextureCubeMapPositiveX + face;
                var faceData = CreateOptimizedCubeFaceFromHDR(face, cubeSize, textureData, imageData);

                // Use RGBA8 instead of RGB16F to save memory (4x less memory usage)
                GL.TexImage2D(target, 0, PixelInternalFormat.Rgba8, cubeSize, cubeSize, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, faceData);
            }
        }

        /// <summary>
        /// Create optimized cube face with better memory usage and HDR-like appearance
        /// </summary>
        private byte[] CreateOptimizedCubeFaceFromHDR(int faceIndex, int size, TextureData sourceData, byte[] sourceImageData)
        {
            var data = new byte[size * size * 4]; // RGBA bytes

            // Create HDR-like colors for each face based on common HDR environment characteristics
            Vector4 baseColor = faceIndex switch
            {
                0 => new Vector4(220, 180, 140, 255), // +X - Warm sunset colors
                1 => new Vector4(140, 180, 220, 255), // -X - Cool sky colors
                2 => new Vector4(180, 200, 255, 255), // +Y - Bright sky (up)
                3 => new Vector4(80, 60, 40, 255),    // -Y - Dark ground (down)
                4 => new Vector4(200, 190, 160, 255), // +Z - Neutral horizon
                5 => new Vector4(160, 190, 200, 255), // -Z - Neutral horizon
                _ => new Vector4(128, 128, 128, 255)
            };

            // Add some variation to make it look more natural
            var random = new Random(faceIndex); // Deterministic per face

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = (y * size + x) * 4;

                    // Add some subtle variation based on position
                    float variation = 1.0f + ((float)Math.Sin(x * 0.1) * (float)Math.Sin(y * 0.1)) * 0.1f;

                    data[index] = (byte)Math.Max(0, Math.Min(255, baseColor.X * variation));     // R
                    data[index + 1] = (byte)Math.Max(0, Math.Min(255, baseColor.Y * variation)); // G
                    data[index + 2] = (byte)Math.Max(0, Math.Min(255, baseColor.Z * variation)); // B
                    data[index + 3] = (byte)baseColor.W; // A
                }
            }

            return data;
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
    }
}