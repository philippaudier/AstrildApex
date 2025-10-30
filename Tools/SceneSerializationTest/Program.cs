using System;
using System.IO;
using Editor.Serialization;
using Engine.Scene;
using Engine.Components;

class Program
{
    static void Main()
    {
        var scene = new Scene();
        var envEntity = new Engine.Scene.Entity { Id = scene.GetNextEntityId(), Name = "Environment", Guid = Guid.NewGuid(), Active = true };
    var env = new EnvironmentSettings();
    env.SkyboxMaterialPath = Guid.NewGuid().ToString();
    env.SkyboxTint = new OpenTK.Mathematics.Vector3(0.2f, 0.4f, 0.6f);
    env.SkyboxExposure = 2.0f;
    env.SunLightEntityId = 42;
    env.MoonLightEntityId = 99;
    env.TimeOfDay = 18.5f;
    env.AmbientMode = Engine.Components.AmbientMode.Color;
    env.AmbientColor = new OpenTK.Mathematics.Vector3(0.1f, 0.2f, 0.3f);
    env.FogEnabled = true;
    env.FogColor = new OpenTK.Mathematics.Vector3(0.7f, 0.6f, 0.5f);
        envEntity.AddComponent(env);
        scene.Entities.Add(envEntity);

        var tmpPath = Path.Combine(Directory.GetCurrentDirectory(), "test.scene");
        Console.WriteLine($"Saving scene to: {tmpPath}");
        var res = SceneSerializer.Save(scene, tmpPath);
        Console.WriteLine($"Save success: {res.Success}, msg: {res.ErrorMessage}");

        var newScene = new Scene();
        var loadRes = SceneSerializer.Load(newScene, tmpPath);
        Console.WriteLine($"Load success: {loadRes.Success}, warnings: {loadRes.Warnings.Count}");

        var createdEnv = newScene.Entities.Find(e => e.HasComponent<EnvironmentSettings>());
        if (createdEnv != null)
        {
            var loadedEnv = createdEnv.GetComponent<EnvironmentSettings>();
            Console.WriteLine($"Loaded SkyboxMaterialPath: {loadedEnv.SkyboxMaterialPath}");
            Console.WriteLine($"Loaded SkyboxTint: {loadedEnv.SkyboxTint}");
            Console.WriteLine($"Loaded SkyboxExposure: {loadedEnv.SkyboxExposure}");
            Console.WriteLine($"Loaded SunLightEntityId: {loadedEnv.SunLightEntityId}");
            Console.WriteLine($"Loaded MoonLightEntityId: {loadedEnv.MoonLightEntityId}");
            Console.WriteLine($"Loaded TimeOfDay: {loadedEnv.TimeOfDay}");
            Console.WriteLine($"Loaded AmbientMode: {loadedEnv.AmbientMode}");
            Console.WriteLine($"Loaded AmbientColor: {loadedEnv.AmbientColor}");
            Console.WriteLine($"Loaded FogEnabled: {loadedEnv.FogEnabled}");
            Console.WriteLine($"Loaded FogColor: {loadedEnv.FogColor}");
        }
        else
        {
            Console.WriteLine("EnvironmentSettings not found after load");
        }

        // Cleanup
        try { File.Delete(tmpPath); } catch { }
    }
}
