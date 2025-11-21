using Engine.Audio.Core;
using Engine.Inspector;
using OpenTK.Mathematics;

namespace Engine.Audio.Components
{
    /// <summary>
    /// Composant AudioListener - Représente l'oreille du joueur (généralement sur la caméra)
    /// Seul un listener peut être actif à la fois
    /// </summary>
    public sealed class AudioListenerComponent : Engine.Components.Component
    {
        private static AudioListenerComponent? _activeListener;

        private Vector3 _lastPosition;
        private Vector3 _velocity;

        [Editable("Velocity Update Mode")]
        public VelocityUpdateMode VelocityMode { get; set; } = VelocityUpdateMode.Auto;

        public enum VelocityUpdateMode
        {
            Auto,   // Calcule automatiquement depuis la position
            Manual  // Doit être défini manuellement
        }

        public static AudioListenerComponent? ActiveListener => _activeListener;

        public override void OnEnable()
        {
            base.OnEnable();

            // Check if AudioEngine is initialized
            if (!AudioEngine.Instance.IsInitialized)
            {
                Serilog.Log.Warning("[AudioListener] AudioEngine not initialized - listener will activate when engine is ready");
                return;
            }

            // Un seul listener actif à la fois
            if (_activeListener != null && _activeListener != this)
            {
                Serilog.Log.Warning($"[AudioListener] Multiple listeners detected - switching active listener from {_activeListener.Entity?.Name} to {Entity?.Name}");
            }

            // Set this as the active listener (do not forcibly disable the previous component)
            _activeListener = this;
            Serilog.Log.Information($"[AudioListener] Activated on entity: {Entity?.Name}");

            if (Entity?.Transform != null)
            {
                Entity.GetWorldTRS(out var pos, out _, out _);
                _lastPosition = pos;
            }
        }

        /// <summary>
        /// Make this listener the active listener at runtime without toggling Enabled on others
        /// </summary>
        public void Activate()
        {
            if (_activeListener == this) return;
            Serilog.Log.Information($"[AudioListener] Activating listener on entity: {Entity?.Name}");
            _activeListener = this;
        }

        public override void OnDisable()
        {
            base.OnDisable();

            if (_activeListener == this)
            {
                _activeListener = null;
            }
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            if (!AudioEngine.Instance.IsInitialized)
                return;

            // If this listener should be active but isn't (e.g., AudioEngine was initialized after OnEnable)
            if (_activeListener != this && Enabled)
            {
                OnEnable(); // Re-activate
                return;
            }

            if (Entity?.Transform == null)
                return;

            Entity.GetWorldTRS(out var position, out var rotation, out _);

            // Calculer la vélocité automatiquement
            if (VelocityMode == VelocityUpdateMode.Auto && dt > 0f)
            {
                _velocity = (position - _lastPosition) / dt;
                _lastPosition = position;
            }

            // Mettre à jour la position et l'orientation du listener
            AudioEngine.Instance.SetListenerPosition(position);
            AudioEngine.Instance.SetListenerVelocity(_velocity);

            // Calculer forward et up depuis la rotation
            // Note: négatif sur forward pour correspondre à la convention "forward = -Z"
            // qui est utilisée par OpenAL / notre pipeline de caméras.
            var forward = -Vector3.Transform(Vector3.UnitZ, rotation);
            var up = Vector3.Transform(Vector3.UnitY, rotation);
            AudioEngine.Instance.SetListenerOrientation(forward, up);
            Serilog.Log.Debug($"[AudioListener] Orientation set. forward={forward}, up={up}");
        }

        /// <summary>
        /// Définit manuellement la vélocité (utile pour un meilleur effet Doppler)
        /// </summary>
        public void SetVelocity(Vector3 velocity)
        {
            _velocity = velocity;
        }
    }
}
