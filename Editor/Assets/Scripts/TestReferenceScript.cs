using System;
using Engine.Scripting;
using Engine.Components;
using Engine.Scene;
using Engine.Inspector;

namespace Engine.Scripting
{
    /// <summary>
    /// Script de test pour vérifier la sérialisation des références
    /// </summary>
    public class TestReferenceScript : MonoBehaviour
    {
        [Editable] public Entity? targetEntity;
        [Editable] public TransformComponent? targetTransform;
        [Editable] public CameraComponent? targetCamera;
        [Editable] public CharacterController? targetController;
        
        [Editable] public string testString = "Hello World";
        [Editable] public float testFloat = 1.0f;
        [Editable] public int testInt = 42;
        [Editable] public bool testBool = true;

        public override void Start()
        {
            base.Start();
            
        }

        public override void Update(float deltaTime)
        {
            // Nothing to do in Update for this test
        }
    }
}
