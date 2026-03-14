namespace ServerBootstrap;

using Agones;
using Agones.Dev.Sdk;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CSSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;
using Microsoft.Extensions.Logging;

[MinimumApiVersion(80)]
public class ServerBootstrap : BasePlugin
{
    public override string ModuleName => "ServerBootstrap";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "ltsochev-dev";
    public override string ModuleDescription => "Bootstraps the servers properly according to Agones annotations.";
    private AgonesSDK agones;
    private CSSTimer? idleTimer;
    private string oldGsState = "Unknown";
    private string serverName = "Unknown";
    private string? mode;
    private int lastWinner = 0;

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

        serverName = gs.ObjectMeta?.Name ?? "Unknown";

        if (state == "Unknown")
        {
            return;
        }

        Logger.LogInformation("[Bootstrap] GS Update: {Name}, state={State}", serverName, state);

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
                    ChangeMap(map);
                }
            }

            if (gs.ObjectMeta?.Annotations?.TryGetValue("mode", out var rawMode) == true)
            {
                var trimmedMode = rawMode.Trim();
                if (!string.IsNullOrWhiteSpace (trimmedMode))
                {
                    mode = trimmedMode;
                }
            }
        }

        if (state != oldGsState)
        {
            oldGsState = state;
        }
        
        // Kill allocated servers if nobody connects
        if (state == "Allocated")
        {
            Server.NextFrame(() =>
            {
                StartIdleTimer(60.0f);
            });
        }
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        lastWinner = ev.Winner;

        return HookResult.Continue;
    }


    [GameEventHandler]
    public HookResult OnMatchEnd(EventCsWinPanelMatch ev, GameEventInfo info)
    {
        string winner = lastWinner switch
        {
            2 => "Terrorists",
            3 => "Counter-Terrorists",
            _ => "Unknown"
        };

        Logger.LogInformation("[Bootstrap] Match End: Winner: {Winner}", winner);

        Server.PrintToChatAll($"[ClutchPoint] Match finished. Winner: {winner}");

        AddTimer(8.0f, async () => {
            await ShutdownServer("match_end");
        });

        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("[Bootstrap] Unload: Plugin unloading...");

        base.Unload(hotReload);
    }

    private void ChangeMap(string map)
    {
        Server.NextFrame(() =>
        {
            if (Server.IsMapValid(map))
            {
                Logger.LogInformation("[Bootstrap] Changing map to {Map}", map);
                Server.ExecuteCommand($"changelevel {map}");
            }
            else
            {
                Logger.LogWarning("[Bootstrap] Map Change: Failed to change map to {Map}. The map doesn't exist in the server map list", map);
            }
        });
    }

    private void StartIdleTimer(float seconds = 60)
    {
        Logger.LogInformation("[Bootstrap] Shutdown: Server allocated. Idle timer started.");

        idleTimer = AddTimer(seconds, async () => await ShutdownServer());
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || player.IsHLTV || player.IsBot || !player.IsValid)
        {
            return HookResult.Continue;
        }

        Logger.LogInformation("[Bootstrap] Shutdown: Player connected. Killing shutdown timer. AgonesPlugin will GC the server.");

        idleTimer?.Kill();
        idleTimer = null;

        return HookResult.Continue;
    }

    private async Task ShutdownServer(string reason = "inactivity")
    {
        Logger.LogInformation("[Bootstrap] Shutdown: Server {Name} is shutting down due to {Reason} after allocation.", serverName, reason);

        try
        {
            Logger.LogInformation("[Bootstrap] Shutdown: Requesting Agones shutdown...");

            await agones.ShutDownAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Bootstrap] Shutdown: Agones shutdown failed, forcing server quit");

            Server.NextFrame(() => {
                Server.ExecuteCommand("quit");
            });
        }
    }
}
