using Serilog;
using Engine.Core;

Console.WriteLine($"{EngineInfo.Name} Sandbox runtime v{EngineInfo.Version}");
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
Log.Information("Sandbox boot OK.");
