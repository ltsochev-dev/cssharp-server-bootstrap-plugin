using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace ServerBootstrap.Controllers
{
    public sealed class CompetitiveController : GameControllerBase
    {
        public override string Mode => "competitive";
        private int lastWinner;

        public CompetitiveController(ServerBootstrap plugin) : base(plugin) { }

        public override void Activate()
        {
            Logger.LogInformation("[Competitive] Activated.");

            Plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            Plugin.RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);
        }

        public override void Deactivate()
        {
            Logger.LogInformation("[Competitive] Deactivated.");
        }

        public HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
        {
            lastWinner = ev.Winner;

            return HookResult.Continue;
        }

        public HookResult OnMatchEnd(EventCsWinPanelMatch ev, GameEventInfo info)
        {
            string winner = lastWinner switch
            {
                2 => "Terrorists",
                3 => "Counter-Terrorists",
                _ => "Unknown"
            };

            Logger.LogInformation("[Bootstrap] Copmetitive Match End: Winner: {Winner}", winner);

            Server.PrintToChatAll($"[ClutchPoint] Match finished. Winner: {winner}");

            Plugin.AddTimer(8.0f, async () => await Plugin.ShutdownServer("map_end"));

            return HookResult.Continue;
        }
    }
}
