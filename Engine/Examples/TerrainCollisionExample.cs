using OpenTK.Mathematics;
using Engine.Components;
using Engine.Physics;
using Engine.Scene;

namespace Engine.Examples
{
    /// <summary>
    /// Example usage of terrain collision system
    /// Demonstrates heightmap-based collision detection for gameplay
    /// </summary>
    public class TerrainCollisionExample
    {
        // =======================
        // EXAMPLE 1: Character Controller
        // =======================

        public static void Example_CharacterController(Terrain terrain, Vector3 characterPosition, float characterHeight)
        {
            // Get terrain height at character's position
            float terrainHeight = terrain.GetHeightAtPosition(characterPosition.X, characterPosition.Z);

            // Place character on terrain surface
            characterPosition.Y = terrainHeight + characterHeight;

            // Check if character can walk on this slope
            float slopeAngle = terrain.GetSlopeAngleAtPosition(characterPosition.X, characterPosition.Z);
            bool canWalk = slopeAngle < 45f; // Max walkable slope: 45 degrees

            if (!canWalk)
            {
                // Too steep! Character slides down or cannot move here
                Vector3 normal = terrain.GetNormalAtPosition(characterPosition.X, characterPosition.Z);
                // Apply sliding physics using the normal...
            }
        }

        // =======================
        // EXAMPLE 2: Object Placement
        // =======================

        public static bool Example_PlaceObject(Terrain terrain, Vector3 spawnPosition, out Vector3 finalPosition)
        {
            finalPosition = spawnPosition;

            // Check if position is within terrain bounds
            if (!terrain.IsPositionOnTerrain(spawnPosition.X, spawnPosition.Z))
            {
                System.Console.WriteLine("Cannot place object: outside terrain bounds");
                return false;
            }

            // Snap object to terrain surface
            float terrainHeight = terrain.GetHeightAtPosition(spawnPosition.X, spawnPosition.Z);
            finalPosition.Y = terrainHeight;

            // Check if slope is suitable for placement
            float slope = terrain.GetSlopeAngleAtPosition(spawnPosition.X, spawnPosition.Z);
            if (slope > 30f)
            {
                System.Console.WriteLine($"Slope too steep for placement: {slope:F1}°");
                return false;
            }

            // Get surface normal for orienting the object
            Vector3 normal = terrain.GetNormalAtPosition(spawnPosition.X, spawnPosition.Z);
            // Use normal to align object with terrain surface...

            return true;
        }

        // =======================
        // EXAMPLE 3: Raycast from Camera
        // =======================

        public static void Example_MouseClickOnTerrain(Terrain terrain, Vector3 cameraPos, Vector3 mouseRayDir)
        {
            // Raycast from camera to find terrain click point
            if (terrain.RaycastTerrain(cameraPos, mouseRayDir, 1000f, out RaycastHit hit))
            {
                System.Console.WriteLine($"Clicked on terrain at: {hit.Point}");
                System.Console.WriteLine($"Surface normal: {hit.Normal}");
                System.Console.WriteLine($"Distance: {hit.Distance:F2}m");

                // Spawn particle effect at hit point
                // SpawnEffect(hit.Point, hit.Normal);

                // Get slope at click point
                float slope = terrain.GetSlopeAngleAtPosition(hit.Point.X, hit.Point.Z);
                System.Console.WriteLine($"Slope: {slope:F1}°");
            }
            else
            {
                System.Console.WriteLine("Did not hit terrain");
            }
        }

        // =======================
        // EXAMPLE 4: Projectile Collision
        // =======================

        public static void Example_ProjectilePhysics(Terrain terrain, Vector3 projectilePos, Vector3 velocity, float deltaTime)
        {
            // Predict next position
            Vector3 nextPos = projectilePos + velocity * deltaTime;

            // Check if projectile will hit terrain
            Vector3 direction = Vector3.Normalize(nextPos - projectilePos);
            float distance = (nextPos - projectilePos).Length;

            if (terrain.RaycastTerrain(projectilePos, direction, distance, out RaycastHit hit))
            {
                // Projectile hit terrain!
                System.Console.WriteLine($"Projectile impact at: {hit.Point}");

                // Calculate bounce direction (reflection formula: V - 2 * (V · N) * N)
                float dotProduct = Vector3.Dot(velocity, hit.Normal);
                Vector3 reflectedDir = velocity - 2 * dotProduct * hit.Normal;
                velocity = reflectedDir * 0.5f; // 50% energy loss

                // Or: Create explosion crater, spawn particles, etc.
            }
        }

        // =======================
        // EXAMPLE 5: AI Pathfinding Height Check
        // =======================

        public static bool Example_AICanReachTarget(Terrain terrain, Vector3 start, Vector3 target)
        {
            // Sample heights along the path
            int samples = 10;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 samplePos = Vector3.Lerp(start, target, t);

                float terrainHeight = terrain.GetHeightAtPosition(samplePos.X, samplePos.Z);
                float slope = terrain.GetSlopeAngleAtPosition(samplePos.X, samplePos.Z);

                // Check if path crosses impassable terrain
                if (slope > 60f) // Too steep for AI
                {
                    System.Console.WriteLine($"Path blocked at {samplePos}: slope {slope:F1}°");
                    return false;
                }

                // Check if path goes underwater (if terrain has water level)
                // if (terrainHeight < waterLevel) return false;
            }

            return true;
        }

        // =======================
        // EXAMPLE 6: Vehicle Physics
        // =======================

        public static void Example_VehicleGroundContact(Terrain terrain, Vector3[] wheelPositions)
        {
            // Check each wheel's contact with terrain
            foreach (var wheelPos in wheelPositions)
            {
                float terrainHeight = terrain.GetHeightAtPosition(wheelPos.X, wheelPos.Z);
                Vector3 normal = terrain.GetNormalAtPosition(wheelPos.X, wheelPos.Z);

                // Calculate wheel compression
                float compression = wheelPos.Y - terrainHeight;

                if (compression < 0.5f) // Wheel is touching ground
                {
                    // Apply suspension force based on normal and compression
                    float suspensionForce = (0.5f - compression) * 1000f;
                    // ApplyForce(wheelPos, normal * suspensionForce);
                }

                // Adjust vehicle tilt based on terrain slope
                // ...
            }
        }

        // =======================
        // EXAMPLE 7: Dynamic Height Queries (Update Loop)
        // =======================

        public static void Example_UpdateLoop(Terrain terrain, Entity entity)
        {
            // Called every frame for entities that need to follow terrain

            Vector3 pos = entity.Transform.Position;

            // Keep entity on terrain surface
            if (terrain.IsPositionOnTerrain(pos.X, pos.Z))
            {
                float terrainHeight = terrain.GetHeightAtPosition(pos.X, pos.Z);
                pos.Y = terrainHeight + 1.0f; // 1m above ground
                entity.Transform.Position = pos;

                // Align entity with terrain slope (optional)
                Vector3 normal = terrain.GetNormalAtPosition(pos.X, pos.Z);
                // entity.Transform.Rotation = Quaternion.FromAxisAngle(...)
            }
        }

        // =======================
        // EXAMPLE 8: Terrain Navigation Query
        // =======================

        public static Vector3? Example_FindNearestWalkablePoint(Terrain terrain, Vector3 targetPos, float maxSlopeAngle)
        {
            // Search in a spiral around target position
            float searchRadius = 10f;
            int searchSteps = 20;

            for (int i = 0; i < searchSteps; i++)
            {
                float angle = i * (360f / searchSteps) * (MathF.PI / 180f);
                float x = targetPos.X + MathF.Cos(angle) * searchRadius;
                float z = targetPos.Z + MathF.Sin(angle) * searchRadius;

                if (terrain.IsPositionOnTerrain(x, z))
                {
                    float slope = terrain.GetSlopeAngleAtPosition(x, z);
                    if (slope <= maxSlopeAngle)
                    {
                        float height = terrain.GetHeightAtPosition(x, z);
                        return new Vector3(x, height, z);
                    }
                }
            }

            return null; // No walkable point found
        }
    }
}
