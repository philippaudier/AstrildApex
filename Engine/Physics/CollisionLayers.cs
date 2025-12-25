using System;

namespace Engine.Physics
{
    /// <summary>
    /// Collision layer system with matrix-based filtering.
    ///
    /// ARCHITECTURE:
    /// - 32 available layers (0-31)
    /// - Symmetric collision matrix (if A collides with B, then B collides with A)
    /// - Layers can be named for better organization
    /// - Used to filter physics queries and collision detection
    ///
    /// INSPIRED BY: Unity's Physics.IgnoreLayerCollision, Unreal's Collision Channels
    ///
    /// USAGE:
    /// // Define your layers
    /// public const int LayerPlayer = 0;
    /// public const int LayerEnemy = 1;
    /// public const int LayerTerrain = 2;
    /// public const int LayerProjectile = 3;
    ///
    /// // Configure which layers collide
    /// CollisionLayers.SetLayerCollision(LayerPlayer, LayerEnemy, false); // Player doesn't collide with enemies
    /// CollisionLayers.SetLayerCollision(LayerProjectile, LayerTerrain, true); // Projectiles hit terrain
    /// </summary>
    public static class CollisionLayers
    {
        // === PREDEFINED LAYERS (Users can define more) ===
        public const int Default = 0;
        public const int Player = 1;
        public const int Enemy = 2;
        public const int Terrain = 3;
        public const int Projectile = 4;
        public const int Trigger = 5;
        public const int Water = 6;
        public const int UI = 7;

        // === COLLISION MATRIX (32x32) ===
        // Uses a 1D array for better memory locality (32*32 = 1024 bools = 1KB)
        private static readonly bool[] _collisionMatrix = new bool[32 * 32];
        private static readonly string[] _layerNames = new string[32];
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the collision matrix with default settings.
        /// Called automatically on first use.
        /// </summary>
        static CollisionLayers()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize default collision settings.
        /// By default, all layers collide with each other.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            // === DEFAULT: All layers collide with all layers ===
            for (int i = 0; i < 32; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    _collisionMatrix[i * 32 + j] = true;
                }
            }

            // === Setup default layer names ===
            _layerNames[Default] = "Default";
            _layerNames[Player] = "Player";
            _layerNames[Enemy] = "Enemy";
            _layerNames[Terrain] = "Terrain";
            _layerNames[Projectile] = "Projectile";
            _layerNames[Trigger] = "Trigger";
            _layerNames[Water] = "Water";
            _layerNames[UI] = "UI";

            for (int i = 8; i < 32; i++)
            {
                _layerNames[i] = $"Layer{i}";
            }

            // === Configure common layer interactions ===
            // Example: UI layer doesn't collide with anything except itself
            SetLayerCollisionAll(UI, false);
            SetLayerCollision(UI, UI, true);

            // Example: Triggers don't block movement (handled separately via IsTrigger flag)
            // But we keep collision enabled so they can be detected

            _initialized = true;
        }

        /// <summary>
        /// Reset collision matrix to default (all layers collide).
        /// </summary>
        public static void Reset()
        {
            _initialized = false;
            Initialize();
        }

        /// <summary>
        /// Set whether two layers should collide.
        /// Matrix is symmetric: SetLayerCollision(A, B, x) also sets (B, A, x).
        /// </summary>
        public static void SetLayerCollision(int layerA, int layerB, bool canCollide)
        {
            if (layerA < 0 || layerA >= 32 || layerB < 0 || layerB >= 32)
            {
                throw new ArgumentOutOfRangeException($"Layer indices must be 0-31 (got {layerA}, {layerB})");
            }

            // Symmetric matrix
            _collisionMatrix[layerA * 32 + layerB] = canCollide;
            _collisionMatrix[layerB * 32 + layerA] = canCollide;
        }

        /// <summary>
        /// Set whether a layer collides with all other layers.
        /// </summary>
        public static void SetLayerCollisionAll(int layer, bool canCollide)
        {
            if (layer < 0 || layer >= 32)
            {
                throw new ArgumentOutOfRangeException($"Layer index must be 0-31 (got {layer})");
            }

            for (int i = 0; i < 32; i++)
            {
                SetLayerCollision(layer, i, canCollide);
            }
        }

        /// <summary>
        /// Check if two layers can collide.
        /// </summary>
        public static bool CanLayersCollide(int layerA, int layerB)
        {
            if (layerA < 0 || layerA >= 32 || layerB < 0 || layerB >= 32)
            {
                return false; // Invalid layers don't collide
            }

            return _collisionMatrix[layerA * 32 + layerB];
        }

        /// <summary>
        /// Set a custom name for a layer (for debugging and editor display).
        /// </summary>
        public static void SetLayerName(int layer, string name)
        {
            if (layer < 0 || layer >= 32)
            {
                throw new ArgumentOutOfRangeException($"Layer index must be 0-31 (got {layer})");
            }

            _layerNames[layer] = name ?? $"Layer{layer}";
        }

        /// <summary>
        /// Get the name of a layer.
        /// </summary>
        public static string GetLayerName(int layer)
        {
            if (layer < 0 || layer >= 32)
            {
                return "Invalid";
            }

            return _layerNames[layer] ?? $"Layer{layer}";
        }

        /// <summary>
        /// Convert layer mask to human-readable string (for debugging).
        /// </summary>
        public static string LayerMaskToString(int layerMask)
        {
            var layers = new System.Collections.Generic.List<string>();

            for (int i = 0; i < 32; i++)
            {
                if ((layerMask & (1 << i)) != 0)
                {
                    layers.Add(GetLayerName(i));
                }
            }

            return layers.Count > 0 ? string.Join(", ", layers) : "None";
        }

        /// <summary>
        /// Check if a layer is included in a layer mask.
        /// </summary>
        public static bool IsLayerInMask(int layer, int layerMask)
        {
            if (layer < 0 || layer >= 32) return false;
            return (layerMask & (1 << layer)) != 0;
        }

        /// <summary>
        /// Create a layer mask from multiple layers.
        /// </summary>
        public static int CreateLayerMask(params int[] layers)
        {
            int mask = 0;
            foreach (int layer in layers)
            {
                if (layer >= 0 && layer < 32)
                {
                    mask |= (1 << layer);
                }
            }
            return mask;
        }

        /// <summary>
        /// Create a layer mask for everything EXCEPT the specified layers.
        /// </summary>
        public static int CreateLayerMaskExcept(params int[] layers)
        {
            int mask = ~0; // All bits set
            foreach (int layer in layers)
            {
                if (layer >= 0 && layer < 32)
                {
                    mask &= ~(1 << layer);
                }
            }
            return mask;
        }
    }
}
