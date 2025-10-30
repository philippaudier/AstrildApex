using System.Collections.Generic;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Engine.Input
{
    /// <summary>
    /// Contexte d'input pour le mode de jeu (gameplay)
    /// Priority: 0 (plus basse - ne consomme que si aucun autre contexte ne veut l'input)
    /// </summary>
    public class PlayModeInputContext : InputContext
    {
        private readonly Dictionary<string, InputActionMap> _actionMaps;

        public PlayModeInputContext(Dictionary<string, InputActionMap> actionMaps) 
            : base("PlayMode", (int)InputContextType.PlayMode)
        {
            _actionMaps = actionMaps;
        }

        public override bool HandleKeyDown(Keys key, bool isRepeat)
        {
            if (!CanProcessInput() || isRepeat) return false;

            // Traiter les ActionMaps (WASD, Jump, etc.)
            // Ces inputs ne sont actifs qu'en mode jeu
            bool consumed = false;
            foreach (var actionMap in _actionMaps.Values)
            {
                if (actionMap.IsEnabled && actionMap.ProcessKeyInput(key))
                {
                    consumed = true;
                    break; // Premier ActionMap qui consomme l'input gagne
                }
            }

            return consumed;
        }

        public override bool HandleMouseDown(MouseButton button)
        {
            if (!CanProcessInput()) return false;

            bool consumed = false;
            foreach (var actionMap in _actionMaps.Values)
            {
                if (actionMap.IsEnabled && actionMap.ProcessMouseInput(button))
                {
                    consumed = true;
                    break;
                }
            }

            return consumed;
        }

        public override void Update(float deltaTime)
        {
            if (!CanProcessInput()) return;

            foreach (var actionMap in _actionMaps.Values)
            {
                if (actionMap.IsEnabled)
                {
                    actionMap.Update();
                }
            }
        }

        public override bool CanProcessInput()
        {
            // PlayMode input n'est actif que si IsEnabled est true (configuré par SetPlayModeActive)
            return IsEnabled;
        }

        private bool IsInPlayMode()
        {
            // Cette méthode n'est plus utilisée - la logique est maintenant dans IsEnabled
            return true;
        }
    }
}