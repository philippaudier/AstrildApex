using System;
using System.Linq;
using System.Reflection;
using Engine.Scripting;
using Engine.Scene;
using Engine.Components;

namespace Editor.Scripting
{
    /// <summary>
    /// Gère les types MonoBehaviour disponibles (depuis l’assembly compilée) et instancie les scripts sur des entities.
    /// </summary>
    public sealed class ScriptHost
    {
        private Assembly? _asm;
        public Type[] AvailableScripts { get; private set; } = Array.Empty<Type>();

        public void BindAssembly(Assembly? asm)
        {
            _asm = asm;
            AvailableScripts = asm == null
                ? Array.Empty<Type>()
                : asm.GetTypes().Where(t => !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t)).ToArray();
            
            if (AvailableScripts.Length > 0)
            {
                Console.WriteLine($"[ScriptHost] 📜 {AvailableScripts.Length} script(s) available:");
                foreach (var script in AvailableScripts)
                {
                    Console.WriteLine($"  - {script.FullName}");
                }
            }
            else
            {
                Console.WriteLine("[ScriptHost] ⚠️ No MonoBehaviour scripts found in assembly!");
            }
        }

        public MonoBehaviour? AddScriptToEntity(Entity e, Type scriptType)
        {
            if (_asm == null || e == null || scriptType == null) return null;
            if (!typeof(MonoBehaviour).IsAssignableFrom(scriptType)) return null;

            var script = (MonoBehaviour?)Activator.CreateInstance(scriptType);
            if (script == null) return null;

            e.AddComponent(script);
            script.Awake(); // pattern Unity
            return script;
        }
    }
}
