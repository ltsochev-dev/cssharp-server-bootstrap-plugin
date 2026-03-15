using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
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

            Plugin.RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            Plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            Plugin.RegisterEventHandler<EventWarmupEnd>(OnWarmUpEnd, HookMode.Pre);
            Plugin.RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);

            Plugin.AddCommand("css_setphase", "For admins only. Sets the phase for easier debug.", (CCSPlayerController? player, CommandInfo commandInfo) => 
            {
                if (player is null || !player.IsValid)
                {
                    return;
                }

                var newPhaseRaw = commandInfo.GetArg(1).Trim().ToLower();

                MatchPhase? newPhase = newPhaseRaw switch
                {
                    "warmup" => MatchPhase.Warmup,
                    "knife" => MatchPhase.Knife,
                    "postknife" => MatchPhase.PostKnife,
                    "game" => MatchPhase.Game,
                    "matchend" => MatchPhase.MatchEnd,
                    _ => null
                };

                if (newPhase is null)
                {
                    commandInfo.ReplyToCommand($"Unknown phase \"{newPhaseRaw}\". Valid options: Warmup, Knife, PostKnife, Game, MatchEnd");
                    return;
                }

                switch (newPhase)
                {
                    case MatchPhase.Warmup:
                        phase = MatchPhase.Warmup;
                        break;
                    case MatchPhase.Knife:
                        EnterKnifePhase();
                        break;
                    case MatchPhase.PostKnife:
                        knifeRoundWinner = player.TeamNum;
                        EnterPostKnifePhase();
                        break;
                    case MatchPhase.Game:
                        EnterGamePhase();
                        break;
                    case MatchPhase.MatchEnd:
                        EnterGamePhase();
                        break;
                }
            });
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
            if (CannotChooseTeam(player))
            {
                return;
            }

            SwitchTeams();

            EnterGamePhase();
        }

        private void onJoinCtSideCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (CannotChooseTeam(player))
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
            if (CannotChooseTeam(player))
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
            if (phase != MatchPhase.Warmup)
            {
                return HookResult.Continue;
            }

            EnterKnifePhase();

            return HookResult.Continue;
        }

        public HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
        {
            lastRoundWinner = ev.Winner;

            if (phase != MatchPhase.Knife)
            {
                return HookResult.Continue;
            }

            if (ev.Winner == (int)CsTeam.None)
            {
                Server.PrintToChatAll($"No knife round winner! Marking the game as a tie and shutting down the server.");
                Task.Run(async () => await Plugin.GracefulShutdown("kniferound_tie"));
            }

            knifeRoundWinner = ev.Winner;

            var winnerTeam = GetTeamName(ev.Winner);

            Server.PrintToChatAll($"Knife round winner team: {winnerTeam}!");

            EnterPostKnifePhase();

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

            EnterMatchEndPhase();

            Plugin.AddTimer(8.0f, async () => await Plugin.GracefulShutdown("map_end"));

            return HookResult.Continue;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            if (phase != MatchPhase.Knife)
            {
                return HookResult.Continue;
            }

            var player = @event.Userid;
            if (Utils.isInvalidPlayer(player))
            {
                return HookResult.Continue;
            }

            try
            {
                player?.RemoveWeapons();
            } catch (Exception ex)
            {
                Logger.LogError(ex, "[Competitive] Failed enforcing knife-only loadout for {Player}", player?.PlayerName);
                Logger.LogError(ex.Message);
            }
            

            return HookResult.Continue;
        }

        private bool IsKnifeRoundWinner(CCSPlayerController? player)
        {
            return player is not null && player.TeamNum == knifeRoundWinner;
        }

        private void SwitchTeams()
        {
            Server.RunOnTick(Server.TickCount + 1, () =>
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
            });
        }

        private void EnterKnifePhase()
        {
            if (phase != MatchPhase.Warmup)
            {
                return;
            }

            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering Knife phase", Plugin.serverName);

            phase = MatchPhase.Knife;

            // @todo disable all weapons, only leave knives, remove round time limits, lower the buy time to 3 seconds and reset the match

            htmlWriter.WrteSimpleText($"<b><font color='red'>Knife Round!</font></b><span>Winner of this round gets to choose a team.</span>!");
        }

        private void EnterPostKnifePhase()
        {
            if (phase != MatchPhase.Knife)
            {
                return;
            }

            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering Post-Knife phase", Plugin.serverName);

            phase = MatchPhase.PostKnife;

            Plugin.AddCommand("css_switch", "Switches teams before match start.", OnSwitchCommand);
            Plugin.AddCommand("css_t", "Switch to Terrorist team before match start.", onJoinTSideCommand);
            Plugin.AddCommand("css_ct", "Switch to Counter-Terrorist team before match start.", onJoinCtSideCommand);

            Server.PrintToChatAll("[ClutchPoint] Knife winner: choose your side with !ct or !t");
            Server.PrintToChatAll("[ClutchPoint] Or use !switch to swap immediately.");
            Server.PrintToChatAll("[ClutchPoint] You have 30 seconds.");

            teamChoiceTimer = Plugin.AddTimer(30.0f, () =>
            {
                Logger.LogInformation("[Competitive] Team choice timer expired. Starting game with current sides.");
                EnterGamePhase();
            });

            // @todo remove all weapon restrictions, switch back to warmup mode with all the weapons
            Server.NextFrame(() =>
            {
                Server.ExecuteCommand("mp_restartgame 0");
            });
        }

        private void EnterGamePhase()
        {
            if (phase != MatchPhase.PostKnife)
            {
                return;
            }

            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering Game phase", Plugin.serverName);

            Plugin.RemoveCommand("css_switch", OnSwitchCommand);
            Plugin.RemoveCommand("css_t", onJoinTSideCommand);
            Plugin.RemoveCommand("css_ct", onJoinCtSideCommand);

            teamChoiceTimer?.Kill();
            teamChoiceTimer = null;

            phase = MatchPhase.Game;

            // @todo remove all weapon restrictions, revert to regular round time limits and revert buy time limit and reset/start the match

            Server.PrintToChatAll("[ClutchPoint] Live match starting.");
            Server.ExecuteCommand("mp_restartgame 1");
        }

        private void EnterMatchEndPhase()
        {
            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering match_end phase", Plugin.serverName);

            phase = MatchPhase.MatchEnd;
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
