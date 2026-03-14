namespace ServerBootstrap;

using Agones;
using Agones.Dev.Sdk;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using global::ServerBootstrap.Config;
using global::ServerBootstrap.Contracts;
using Microsoft.Extensions.Logging;
using CSSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

[MinimumApiVersion(80)]
public class ServerBootstrap : BasePlugin
{
    public override string ModuleName => "ServerBootstrap";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "ltsochev-dev";
    public override string ModuleDescription => "Bootstraps the servers properly according to Agones annotations.";
    private AgonesSDK agones;
    private CSSTimer? idleTimer;
    private string serverName = "Unknown";
    private string prevState = "Scheduled";
    private IGameModeController? activeController;

    public ServerBootstrap()
    {
        agones = new AgonesSDK(logger: Logger);
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("[Bootstrap] Load: Plugin loading...");

        agones.WatchGameServer(OnGameServerChange);
    }

    public void SetController(IGameModeController controller)
    {
        activeController?.Deactivate();
        activeController = controller;
        activeController.Activate();
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
            Task.Run(async () => await agones.ReadyAsync());

            return;
        }

        Logger.LogInformation("[Bootstrap] GS Update: {Name}, state={State}", serverName, state);

        if (prevState == "Ready" && state == "Allocated")
        {
            InitGameServer(gs);
        }
    }

    private void InitGameServer(GameServer gs)
    {
        Logger.LogInformation("[Bootstrap] GS Update: Initializing allocated gameserver");

        StartIdleTimer(60.0f);

        var annotations = gs.ObjectMeta?.Annotations;

        var result = AnnotationsParser.Parse(annotations);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                Logger.LogError("[Bootstrap] Annotation parse error: {Error}", error);
            }

            return;
        }

        var dto = result.Data!;

        Logger.LogInformation(
            "[Bootstrap] Parsed annotations: mode={Mode}, map={Map}, hltv={Hltv}",
            dto.Mode,
            dto.Map ?? "<null>",
            dto.EnableHltv
        );

        var controller = GameModeControllerFactory.Create(this, dto);

        if (controller == null)
        {
            Logger.LogWarning(
                "[Bootstrap] Unsupported mode after parse: {Mode}",
                dto.Mode
            );

            return;
        }

        SetController(controller);
    }

    #region Game Event Handlers
    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController? player = @event.Userid;
        if (player == null || player.IsHLTV || player.IsBot || !player.IsValid)
        {
            return HookResult.Continue;
        }

        idleTimer?.Kill();
        idleTimer = null;

        return HookResult.Continue;
    }
    #endregion

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("[Bootstrap] Unload: Plugin unloading...");

        activeController?.Deactivate();

        base.Unload(hotReload);
    }

    public void ChangeMap(string map)
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

    public async Task ShutdownServer(string reason = "inactivity")
    {
        Logger.LogInformation("[Bootstrap] Shutdown: Server {Name} is shutting down due to {Reason}.", serverName, reason);

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
