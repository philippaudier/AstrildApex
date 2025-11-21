using Engine.Components;
using Engine.Scene;
using OpenTK.Mathematics;
using System;

namespace Engine.Components
{
    /// <summary>
    /// Component de test pour diagnostiquer les problèmes de CharacterController
    /// Ajoutez ce component à votre Player pour voir des infos de debug
    /// </summary>
    public class CharacterControllerDebug : Component
    {
        private CharacterController? _controller;
        private int _frameCount = 0;

        public override void Start()
        {
            _controller = Entity?.GetComponent<CharacterController>();
            
            if (_controller == null)
            {
                Console.WriteLine("[DEBUG] ❌ CharacterController NOT FOUND on entity!");
                return;
            }

            Console.WriteLine("[DEBUG] ✅ CharacterController found");
            Console.WriteLine($"[DEBUG] Height: {_controller.Height}, Radius: {_controller.Radius}");
            Console.WriteLine($"[DEBUG] Position: {Entity?.Transform.Position}");
            
            // Activer le debug du controller
            _controller.DebugPhysics = true;
        }

        public override void Update(float dt)
        {
            if (_controller == null || Entity == null) return;

            _frameCount++;
            
            // Log toutes les 60 frames (environ 1 seconde)
            if (_frameCount % 60 == 0)
            {
                Console.WriteLine($"[DEBUG] === Frame {_frameCount} ===");
                Console.WriteLine($"[DEBUG] Position: {Entity.Transform.Position:F3}");
                Console.WriteLine($"[DEBUG] IsGrounded: {_controller.IsGrounded}");
                Console.WriteLine($"[DEBUG] Velocity: {_controller.Velocity:F3}");
                
                // Test raycast vers le bas
                var pos = Entity.Transform.Position;
                var ray = new Physics.Ray { Origin = pos, Direction = Vector3.UnitY * -1 };
                
                if (Physics.Physics.Raycast(ray, out var hit, 100f))
                {
                    Console.WriteLine($"[DEBUG] ✅ Raycast hit: {hit.Entity?.Name} at distance {hit.Distance:F3}");
                    Console.WriteLine($"[DEBUG] Hit collider type: {hit.ColliderComponent?.GetType().Name}");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] ❌ NO GROUND FOUND (raycast down 100m)");
                }
                
                Console.WriteLine($"[DEBUG] ===");
            }
        }
    }
}
