using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;

namespace Engine.Core
{
    /// <summary>
    /// Defines the phase in the engine update cycle where a system executes
    /// </summary>
    public enum UpdatePhase
    {
        /// <summary>Pre-update phase - before any game logic (input handling, etc.)</summary>
        PreUpdate = 0,

        /// <summary>Early update phase - early game logic that other systems depend on</summary>
        EarlyUpdate = 100,

        /// <summary>Fixed update phase - physics simulation at fixed timestep</summary>
        FixedUpdate = 200,

        /// <summary>Update phase - main game logic</summary>
        Update = 300,

        /// <summary>Late update phase - after main logic (camera follow, final transforms)</summary>
        LateUpdate = 400,

        /// <summary>Pre-render phase - prepare rendering data</summary>
        PreRender = 500,

        /// <summary>Render phase - actual rendering</summary>
        Render = 600,

        /// <summary>Post-render phase - UI, post-processing, cleanup</summary>
        PostRender = 700
    }

    /// <summary>
    /// Interface for systems that execute during the engine update cycle
    /// </summary>
    public interface IUpdateSystem
    {
        /// <summary>Name of the system for debugging and profiling</summary>
        string Name { get; }

        /// <summary>Whether this system is currently enabled</summary>
        bool Enabled { get; }

        /// <summary>Phase in which this system executes</summary>
        UpdatePhase Phase { get; }

        /// <summary>Priority within the phase (lower = earlier)</summary>
        int Priority { get; }

        /// <summary>
        /// Execute the system update
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds</param>
        void Update(float deltaTime);
    }

    /// <summary>
    /// Manages the engine's update pipeline with crash-proof system execution
    /// Inspired by Unity's Player Loop and Unreal's Tick Groups
    /// </summary>
    public sealed class EngineUpdatePipeline
    {
        private static EngineUpdatePipeline? _instance;
        public static EngineUpdatePipeline Instance => _instance ??= new EngineUpdatePipeline();

        private readonly List<IUpdateSystem> _systems = new();
        private readonly Dictionary<UpdatePhase, List<IUpdateSystem>> _systemsByPhase = new();
        private readonly Dictionary<string, SystemMetrics> _metrics = new();
        private bool _isDirty = true;

        // Fixed timestep for physics
        private double _fixedTimeAccumulator = 0.0;
        private const double FixedTimeStep = 1.0 / 60.0; // 60 Hz physics
        
        // Track last frame time for Time.DeltaTime
        private DateTime _lastFrameTime = DateTime.UtcNow;

        // PERFORMANCE: Cache UpdatePhase enum values to avoid Enum.GetValues() allocation every frame
        private static readonly UpdatePhase[] AllPhases = (UpdatePhase[])Enum.GetValues(typeof(UpdatePhase));

        public bool ProfilingEnabled { get; set; } = false;

        private class SystemMetrics
        {
            public long TotalTicks { get; set; }
            public int ExecutionCount { get; set; }
            public long MaxTicks { get; set; }
            public long MinTicks { get; set; } = long.MaxValue;

            public double AverageMs => ExecutionCount > 0
                ? (TotalTicks / (double)ExecutionCount) / Stopwatch.Frequency * 1000.0
                : 0.0;

            public double MaxMs => MaxTicks / (double)Stopwatch.Frequency * 1000.0;
            public double MinMs => MinTicks != long.MaxValue
                ? MinTicks / (double)Stopwatch.Frequency * 1000.0
                : 0.0;
        }

        private EngineUpdatePipeline()
        {
            // Initialize phase buckets
            foreach (UpdatePhase phase in Enum.GetValues(typeof(UpdatePhase)))
            {
                _systemsByPhase[phase] = new List<IUpdateSystem>();
            }
        }

        /// <summary>
        /// Register a system to be executed during the update cycle
        /// </summary>
        public void RegisterSystem(IUpdateSystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            if (_systems.Any(s => s.Name == system.Name))
            {
                Log.Warning($"[EngineUpdatePipeline] System '{system.Name}' is already registered");
                return;
            }

            _systems.Add(system);
            _isDirty = true;

            Log.Information($"[EngineUpdatePipeline] Registered system '{system.Name}' " +
                          $"(Phase: {system.Phase}, Priority: {system.Priority})");
        }

        /// <summary>
        /// Unregister a system from the update cycle
        /// </summary>
        public void UnregisterSystem(IUpdateSystem system)
        {
            if (system == null) return;

            if (_systems.Remove(system))
            {
                _isDirty = true;
                Log.Information($"[EngineUpdatePipeline] Unregistered system '{system.Name}'");
            }
        }

        /// <summary>
        /// Unregister a system by name
        /// </summary>
        public void UnregisterSystem(string systemName)
        {
            var system = _systems.FirstOrDefault(s => s.Name == systemName);
            if (system != null)
            {
                UnregisterSystem(system);
            }
        }

        /// <summary>
        /// Rebuild the phase buckets if systems were added/removed
        /// </summary>
        private void RebuildPipeline()
        {
            if (!_isDirty) return;

            // Clear all phase buckets
            foreach (var bucket in _systemsByPhase.Values)
            {
                bucket.Clear();
            }

            // Sort systems by phase and priority
            var sortedSystems = _systems
                .OrderBy(s => (int)s.Phase)
                .ThenBy(s => s.Priority)
                .ToList();

            // Distribute into phase buckets
            foreach (var system in sortedSystems)
            {
                _systemsByPhase[system.Phase].Add(system);
            }

            _isDirty = false;

            Log.Debug($"[EngineUpdatePipeline] Pipeline rebuilt with {_systems.Count} systems");
        }

        /// <summary>
        /// Execute all systems for the current frame
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds</param>
        public void ExecuteFrame(float deltaTime)
        {
            // Update Time class with current frame delta
            Time.Update(deltaTime);
            
            RebuildPipeline();

            // Execute each phase in order - use cached enum values to avoid allocation
            foreach (UpdatePhase phase in AllPhases)
            {
                var systems = _systemsByPhase[phase];
                if (systems.Count == 0) continue;

                // Special handling for FixedUpdate phase
                if (phase == UpdatePhase.FixedUpdate)
                {
                    ExecuteFixedUpdatePhase(deltaTime, systems);
                }
                else
                {
                    ExecutePhase(phase, deltaTime, systems);
                }
            }
        }

        /// <summary>
        /// Execute a single phase with crash protection
        /// </summary>
        private void ExecutePhase(UpdatePhase phase, float deltaTime, List<IUpdateSystem> systems)
        {
            foreach (var system in systems)
            {
                if (!system.Enabled) continue;

                try
                {
                    if (ProfilingEnabled)
                    {
                        ExecuteSystemWithProfiling(system, deltaTime);
                    }
                    else
                    {
                        system.Update(deltaTime);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[EngineUpdatePipeline] System '{system.Name}' crashed in phase {phase}");

                    // Continue executing other systems - crash isolation
                    // The system will still be called next frame unless it's disabled/removed
                }
            }
        }

        /// <summary>
        /// Execute fixed timestep physics phase
        /// </summary>
        private void ExecuteFixedUpdatePhase(float deltaTime, List<IUpdateSystem> systems)
        {
            _fixedTimeAccumulator += deltaTime;

            // Execute physics at fixed timestep
            while (_fixedTimeAccumulator >= FixedTimeStep)
            {
                foreach (var system in systems)
                {
                    if (!system.Enabled) continue;

                    try
                    {
                        if (ProfilingEnabled)
                        {
                            ExecuteSystemWithProfiling(system, (float)FixedTimeStep);
                        }
                        else
                        {
                            system.Update((float)FixedTimeStep);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"[EngineUpdatePipeline] System '{system.Name}' crashed in FixedUpdate");
                    }
                }

                _fixedTimeAccumulator -= FixedTimeStep;
            }
        }

        /// <summary>
        /// Execute a system with performance profiling
        /// </summary>
        private void ExecuteSystemWithProfiling(IUpdateSystem system, float deltaTime)
        {
            var sw = Stopwatch.StartNew();
            system.Update(deltaTime);
            sw.Stop();

            if (!_metrics.ContainsKey(system.Name))
            {
                _metrics[system.Name] = new SystemMetrics();
            }

            var metrics = _metrics[system.Name];
            metrics.TotalTicks += sw.ElapsedTicks;
            metrics.ExecutionCount++;

            if (sw.ElapsedTicks > metrics.MaxTicks)
                metrics.MaxTicks = sw.ElapsedTicks;

            if (sw.ElapsedTicks < metrics.MinTicks)
                metrics.MinTicks = sw.ElapsedTicks;
        }

        /// <summary>
        /// Get profiling metrics for a system
        /// </summary>
        public (double avgMs, double maxMs, double minMs, int executions) GetSystemMetrics(string systemName)
        {
            if (_metrics.TryGetValue(systemName, out var metrics))
            {
                return (metrics.AverageMs, metrics.MaxMs, metrics.MinMs, metrics.ExecutionCount);
            }
            return (0, 0, 0, 0);
        }

        /// <summary>
        /// Get all registered systems
        /// </summary>
        public IReadOnlyList<IUpdateSystem> GetAllSystems() => _systems.AsReadOnly();

        /// <summary>
        /// Get systems in a specific phase
        /// </summary>
        public IReadOnlyList<IUpdateSystem> GetSystemsInPhase(UpdatePhase phase)
        {
            RebuildPipeline();
            return _systemsByPhase[phase].AsReadOnly();
        }

        /// <summary>
        /// Clear all profiling metrics
        /// </summary>
        public void ClearMetrics()
        {
            _metrics.Clear();
        }

        /// <summary>
        /// Reset the pipeline (for Play Mode transitions)
        /// </summary>
        public void Reset()
        {
            _fixedTimeAccumulator = 0.0;
            ClearMetrics();
        }

        /// <summary>
        /// Get a snapshot of all current metrics for debugging
        /// </summary>
        public Dictionary<string, (double avgMs, double maxMs, int count)> GetAllMetrics()
        {
            var result = new Dictionary<string, (double, double, int)>();
            foreach (var kvp in _metrics)
            {
                result[kvp.Key] = (kvp.Value.AverageMs, kvp.Value.MaxMs, kvp.Value.ExecutionCount);
            }
            return result;
        }
    }
}
