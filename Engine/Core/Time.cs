using System;

namespace Engine.Core
{
    /// <summary>
    /// Provides time-related information for the game engine.
    /// Similar to Unity's Time class but adapted for AstrildApex.
    /// 
    /// USAGE:
    /// - Time.DeltaTime: Frame time (variable, use in Update())
    /// - Time.FixedDeltaTime: Physics timestep (fixed 60Hz, use in FixedUpdate())
    /// - Time.Time: Total elapsed time since game start
    /// - Time.UnscaledDeltaTime: Real frame time (ignores TimeScale)
    /// - Time.TimeScale: Game speed multiplier (0.5 = half speed, 2.0 = double speed)
    /// 
    /// FRAME COUNTING:
    /// - Time.FrameCount: Total frames rendered
    /// - Time.FixedFrameCount: Total fixed physics steps
    /// 
    /// PERFORMANCE:
    /// - Time.SmoothDeltaTime: Smoothed deltaTime (reduces spikes)
    /// - Time.FPS: Current frames per second
    /// </summary>
    public static class Time
    {
        // ============================================
        // TIME VALUES
        // ============================================

        /// <summary>
        /// Time in seconds since the last frame (variable framerate).
        /// Use this in Update() for frame-rate independent movement.
        /// </summary>
        public static float DeltaTime { get; internal set; } = 0f;

        /// <summary>
        /// Fixed physics timestep in seconds (constant 60 Hz = 0.01666s).
        /// Use this in FixedUpdate() for consistent physics simulation.
        /// </summary>
        public static float FixedDeltaTime { get; } = 1.0f / 60.0f;

        /// <summary>
        /// Total time in seconds since the game started.
        /// Affected by TimeScale.
        /// </summary>
        public static float TimeValue { get; internal set; } = 0f;

        /// <summary>
        /// Real time in seconds since the game started.
        /// NOT affected by TimeScale (always real-world time).
        /// </summary>
        public static float UnscaledTime { get; internal set; } = 0f;

        /// <summary>
        /// Real frame time in seconds (ignores TimeScale).
        /// Use this for UI animations or when you need real-world time.
        /// </summary>
        public static float UnscaledDeltaTime { get; internal set; } = 0f;

        /// <summary>
        /// Smoothed deltaTime to reduce jitter/spikes.
        /// Use this for camera movement or other smooth animations.
        /// </summary>
        public static float SmoothDeltaTime { get; internal set; } = 0f;

        // ============================================
        // TIME SCALING
        // ============================================

        private static float _timeScale = 1.0f;

        /// <summary>
        /// Game speed multiplier (default: 1.0).
        /// - 0.0 = Paused
        /// - 0.5 = Half speed (slow motion)
        /// - 1.0 = Normal speed
        /// - 2.0 = Double speed (fast forward)
        /// 
        /// Does NOT affect UnscaledTime or UnscaledDeltaTime.
        /// </summary>
        public static float TimeScale
        {
            get => _timeScale;
            set => _timeScale = MathF.Max(0f, value); // Clamp to positive
        }

        // ============================================
        // FRAME COUNTING
        // ============================================

        /// <summary>
        /// Total number of frames rendered since game start.
        /// Incremented once per Update() cycle.
        /// </summary>
        public static int FrameCount { get; internal set; } = 0;

        /// <summary>
        /// Total number of fixed physics steps since game start.
        /// Incremented once per FixedUpdate() cycle (60 Hz).
        /// </summary>
        public static int FixedFrameCount { get; internal set; } = 0;

        // ============================================
        // PERFORMANCE METRICS
        // ============================================

        /// <summary>
        /// Current frames per second (smoothed over ~1 second).
        /// </summary>
        public static float FPS { get; internal set; } = 60f;

        /// <summary>
        /// Maximum recorded delta time in the last second (for performance monitoring).
        /// </summary>
        public static float MaxDeltaTime { get; internal set; } = 0f;

        // ============================================
        // INTERNAL STATE (for smoothing)
        // ============================================

        private const int SMOOTH_BUFFER_SIZE = 10;
        private static float[] _deltaTimeBuffer = new float[SMOOTH_BUFFER_SIZE];
        private static int _bufferIndex = 0;
        private static float _fpsTimer = 0f;
        private static int _fpsFrameCount = 0;

        // ============================================
        // INTERNAL UPDATE (called by EngineUpdatePipeline)
        // ============================================

        /// <summary>
        /// Update time values. Called once per frame by the engine.
        /// Game builds should also call this in their main loop.
        /// </summary>
        public static void Update(float realDeltaTime)
        {
            // Clamp to prevent huge jumps (pause, breakpoint, etc.)
            realDeltaTime = MathF.Min(realDeltaTime, 0.1f); // Max 100ms (10 FPS minimum)

            // Update unscaled time (real-world time)
            UnscaledDeltaTime = realDeltaTime;
            UnscaledTime += realDeltaTime;

            // Update scaled time (game time)
            DeltaTime = realDeltaTime * _timeScale;
            TimeValue += DeltaTime;

            // Update smooth delta time
            _deltaTimeBuffer[_bufferIndex] = realDeltaTime;
            _bufferIndex = (_bufferIndex + 1) % SMOOTH_BUFFER_SIZE;

            float sum = 0f;
            for (int i = 0; i < SMOOTH_BUFFER_SIZE; i++)
                sum += _deltaTimeBuffer[i];
            SmoothDeltaTime = (sum / SMOOTH_BUFFER_SIZE) * _timeScale;

            // Update FPS (calculate every second)
            _fpsTimer += realDeltaTime;
            _fpsFrameCount++;

            if (_fpsTimer >= 1.0f)
            {
                FPS = _fpsFrameCount / _fpsTimer;
                _fpsTimer = 0f;
                _fpsFrameCount = 0;
            }

            // Track max delta time (reset every second)
            if (realDeltaTime > MaxDeltaTime)
                MaxDeltaTime = realDeltaTime;
            
            if (_fpsTimer == 0f) // Reset every second
                MaxDeltaTime = 0f;

            // Increment frame count
            FrameCount++;
        }

        /// <summary>
        /// Increment fixed frame count. Called by PhysicsManager.
        /// Do NOT call this manually!
        /// </summary>
        internal static void IncrementFixedFrameCount()
        {
            FixedFrameCount++;
        }

        /// <summary>
        /// Reset all time values (called when entering/exiting play mode).
        /// </summary>
        internal static void Reset()
        {
            DeltaTime = 0f;
            TimeValue = 0f;
            UnscaledTime = 0f;
            UnscaledDeltaTime = 0f;
            SmoothDeltaTime = 0f;
            FrameCount = 0;
            FixedFrameCount = 0;
            FPS = 60f;
            MaxDeltaTime = 0f;
            _timeScale = 1.0f;

            // Reset buffers
            Array.Clear(_deltaTimeBuffer, 0, SMOOTH_BUFFER_SIZE);
            _bufferIndex = 0;
            _fpsTimer = 0f;
            _fpsFrameCount = 0;
        }

        // ============================================
        // UTILITY METHODS
        // ============================================

        /// <summary>
        /// Pause the game (set TimeScale to 0).
        /// </summary>
        public static void Pause()
        {
            TimeScale = 0f;
        }

        /// <summary>
        /// Resume the game (set TimeScale to 1).
        /// </summary>
        public static void Resume()
        {
            TimeScale = 1f;
        }

        /// <summary>
        /// Check if the game is paused.
        /// </summary>
        public static bool IsPaused => TimeScale == 0f;

        /// <summary>
        /// Set slow motion effect.
        /// </summary>
        /// <param name="speed">Speed multiplier (0.0 to 1.0). Example: 0.5 = half speed</param>
        public static void SetSlowMotion(float speed)
        {
            TimeScale = MathF.Max(0f, MathF.Min(1f, speed));
        }

        /// <summary>
        /// Set fast forward effect.
        /// </summary>
        /// <param name="speed">Speed multiplier (1.0+). Example: 2.0 = double speed</param>
        public static void SetFastForward(float speed)
        {
            TimeScale = MathF.Max(1f, speed);
        }
    }
}
