using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using CSSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace ServerBootstrap.Controllers
{
    public sealed class CompetitiveController : GameControllerBase
    {
        private enum MatchPhase
        {
            Warmup,
            Knife,
            PostKnife,
            Game,
            MatchEnd
        }

        public override string Mode => "competitive";
        private int lastRoundWinner;
        private int knifeRoundWinner;
        private CSSTimer? teamChoiceTimer;
        private MatchPhase phase = MatchPhase.Warmup;

        public CompetitiveController(ServerBootstrap plugin) : base(plugin) { }

        public override void Activate()
        {
            Logger.LogInformation("[Competitive] Activated.");

            Plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            Plugin.RegisterEventHandler<EventWarmupEnd>(OnWarmUpEnd);
            Plugin.RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);
        }
        

        public override void Deactivate()
        {
            phase = MatchPhase.Warmup;
            teamChoiceTimer?.Kill();
            teamChoiceTimer = null;

            Plugin.RemoveCommand("css_switch", OnSwitchCommand);
            Plugin.RemoveCommand("css_t", onJoinTSideCommand);
            Plugin.RemoveCommand("css_ct", onJoinCtSideCommand);

            Logger.LogInformation("[Competitive] Deactivated.");
        }

        private void OnSwitchCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!CannotChooseTeam(player))
            {
                return;
            }

            SwitchTeams();

            EnterGamePhase();
        }

        private void onJoinCtSideCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!CannotChooseTeam(player))
            {
                return;
            }

            if (player?.Team == CsTeam.Terrorist)
            {
                SwitchTeams();
            }

            EnterGamePhase();
        }

        private void onJoinTSideCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (!CannotChooseTeam(player))
            {
                return;
            }

            if (player?.Team == CsTeam.CounterTerrorist)
            {
                SwitchTeams();
            }

            EnterGamePhase();
        }

        public HookResult OnWarmUpEnd(EventWarmupEnd ev, GameEventInfo info)
        {
            EnterKnifePhase();

            return HookResult.Continue;
        }

        public HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
        {
            lastRoundWinner = ev.Winner;

            if (phase == MatchPhase.Knife)
            {
                knifeRoundWinner = ev.Winner;

                var winnerTeam = GetTeamName(ev.Winner);

                Server.PrintToChatAll($"Knife round winner team: {winnerTeam}!");

                EnterPostKnifePhase();
            }

            return HookResult.Continue;
        }

        public HookResult OnMatchEnd(EventCsWinPanelMatch ev, GameEventInfo info)
        {
            string winner = lastRoundWinner switch
            {
                2 => "Terrorists",
                3 => "Counter-Terrorists",
                _ => "Unknown"
            };

            Logger.LogInformation("[Bootstrap] Competitive Match End: Winner: {Winner}", winner);

            Server.PrintToChatAll($"[ClutchPoint] Match finished. Winner: {winner}");

            Plugin.AddTimer(8.0f, async () => await Plugin.ShutdownServer("map_end"));

            return HookResult.Continue;
        }

        private bool IsKnifeRoundWinner(CCSPlayerController? player)
        {
            return player is not null && player.TeamNum == knifeRoundWinner;
        }

        private void SwitchTeams()
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (Utils.isInvalidPlayer(player)) continue;

                switch (player.Team)
                {
                    case CsTeam.Terrorist:
                        player.SwitchTeam(CsTeam.CounterTerrorist);
                        break;
                    case CsTeam.CounterTerrorist:
                        player.SwitchTeam(CsTeam.Terrorist);
                        break;
                }
            }
        }

        private void EnterKnifePhase()
        {
            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering Knife phase", Plugin.serverName);

            phase = MatchPhase.Knife;

            // @todo disable all weapons, only leave knives, remove round time limits, lower the buy time to 3 seconds and reset the match
        }

        private void EnterPostKnifePhase()
        {
            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering Post-Knife phase", Plugin.serverName);

            phase = MatchPhase.PostKnife;

            Plugin.AddCommand("css_switch", "Switches teams before match start.", OnSwitchCommand);
            Plugin.AddCommand("css_t", "Switch to Terrorist team before match start.", onJoinTSideCommand);
            Plugin.AddCommand("css_ct", "Switch to Counter-Terrorist team before match start.", onJoinCtSideCommand);

            Server.PrintToChatAll("Choose your team by typing !CT or !T in chat");
            Server.PrintToChatAll("You have 30 seconds to decide.");

            teamChoiceTimer = Plugin.AddTimer(30, () => EnterGamePhase());

            // @todo remove all weapon restrictions, switch back to warmup mode with all the weapons
        }

        private void EnterGamePhase()
        {
            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering Game phase", Plugin.serverName);

            Plugin.RemoveCommand("css_switch", OnSwitchCommand);
            Plugin.RemoveCommand("css_t", onJoinTSideCommand);
            Plugin.RemoveCommand("css_ct", onJoinCtSideCommand);

            teamChoiceTimer?.Kill();
            teamChoiceTimer = null;

            phase = MatchPhase.Game;

            // @todo remove all weapon restrictions, revert to regular round time limits and revert buy time limit and reset/start the match

            Server.ExecuteCommand("mp_restartgame 1");
        }

        private string GetTeamName(int teamId)
        {
            string winner = teamId switch
            {
                2 => "Terrorists",
                3 => "Counter-Terrorists",
                _ => "Unknown"
            };

            return winner;
        }

        private bool CannotChooseTeam(CCSPlayerController? player)
        {
            return Utils.isInvalidPlayer(player) ||
                   phase != MatchPhase.PostKnife ||
                   !IsKnifeRoundWinner(player);
        }
    }
}
