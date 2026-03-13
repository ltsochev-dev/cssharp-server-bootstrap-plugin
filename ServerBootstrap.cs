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
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();

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
        if (gs == null)
        {
            Logger.LogWarning("[Bootstrap] GS Update: GameServer update was null");
            return;
        }

        var state = gs.Status?.State ?? "Unknown";
        var name = gs.ObjectMeta?.Name ?? "Unknown";

        Logger.LogInformation("[Bootstrap] GS Update: {Name}, state={State}", name, state);

        if (gs.ObjectMeta?.Annotations != null)
        {
            foreach (var kv in gs.ObjectMeta.Annotations)
            {
                Logger.LogInformation("Annotation {Key}={Value}", kv.Key, kv.Value);
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
