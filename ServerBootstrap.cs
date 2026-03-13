namespace ServerBootstrap;

using Agones;
using Agones.Dev.Sdk;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

[MinimumApiVersion(80)]
public class ServerBootstrap : BasePlugin
{
    public override string ModuleName => "ServerBootstrap";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "ltsochev-dev";
    public override string ModuleDescription => "Bootstraps the servers properly according to Agones annotations.";
    private AgonesSDK agones;
    
    public ServerBootstrap()
    {
        agones = new AgonesSDK(logger: Logger);
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("[Bootstrap] Load: Plugin loading...");

        agones.WatchGameServer(OnGameServerChange);
    }

    private void OnGameServerChange(GameServer gs)
    {
        if (gs == null || gs.Status?.State == null || gs.Status.State == "")
        {
            Logger.LogWarning("[Bootstrap] GS Update: GameServer update was null");
            return;
        }

        var state = gs.Status?.State ?? "Unknown";
        var name = gs.ObjectMeta?.Name ?? "Unknown";

        if (state == "Unknown")
        {
            return;
        }

        Logger.LogInformation("[Bootstrap] GS Update: {Name}, state={State}", name, state);

        // @todo Remove server password, whitelist etc etc

        if (gs.ObjectMeta?.Annotations != null)
        {
            foreach (var kv in gs.ObjectMeta.Annotations)
            {
                Logger.LogInformation("[Bootstrap] Annotation {Key}={Value}", kv.Key, kv.Value);
            }

            if (gs.ObjectMeta?.Annotations?.TryGetValue("map", out var rawMap) == true)
            {
                var map = rawMap?.Trim();
                if (!string.IsNullOrWhiteSpace(map))
                {
                    Logger.LogInformation("[Bootstrap] Changing map to {Map}", map);

                    Server.NextFrame(() => Server.ExecuteCommand($"changelevel {map}"));
                }
            }
        }

        Logger.LogInformation(state);
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("[Bootstrap] Unload: Plugin unloading...");

        base.Unload(hotReload);
    }
}
