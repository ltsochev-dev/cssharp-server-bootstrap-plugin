using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ServerBootstrap.Config.Dto;
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
        private CSSTimer? fillTimer;
        private int fillTimeRemaining;
        private bool knifeRoundStarted = false;
        private const int RequiredPlayers = 10;
        private MatchPhase phase = MatchPhase.Warmup;

        public CompetitiveController(ServerBootstrap plugin, ServerAnnotationsDto dto) : base(plugin, dto) { }

        public override void Activate()
        {
            Logger.LogInformation("[Competitive] Activated.");

            Plugin.RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            Plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            Plugin.RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);
            Plugin.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnected);

            Plugin.AddCommandListener("jointeam", OnJoinTeamCommand);
            Plugin.AddCommandListener("spectate", OnSpectateCommand);
            Plugin.AddCommandListener("teammenu", OnTeamMenuCommand);

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand("sv_disable_show_team_select_menu 1");
                Server.ExecuteCommand("mp_force_assign_teams 1");
                Server.ExecuteCommand("mp_friendlyfire 0");
                Server.ExecuteCommand("mp_autoteambalance 0");

                // Control the warmup time
                Server.ExecuteCommand("mp_do_warmup_period 1");
                Server.ExecuteCommand("mp_warmuptime 180");
                Server.ExecuteCommand("mp_endwarmup_player_count 0");
                Server.ExecuteCommand("mp_warmup_pausetimer 1");
            });

            Plugin.AddCommand("css_next", "Progressed to knife phase", (CCSPlayerController? player, CommandInfo commandInfo) => { TryStartKnifeRound("userControlled"); });
            Plugin.AddCommand("css_curphase", "Echoes the current server phase", (CCSPlayerController? player, CommandInfo commandInfo) => commandInfo.ReplyToCommand($"Current phase: {phase.ToString()}"));
            Plugin.AddCommand("css_phase", "For admins only. Sets the phase for easier debug.", (CCSPlayerController? player, CommandInfo commandInfo) => 
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
                        knifeRoundStarted = false;
                        StartFillPhase();
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
                        EnterMatchEndPhase();
                        break;
                }
            });

            StartFillPhase();
        }

        private void StartFillPhase()
        {
            phase = MatchPhase.Warmup;

            fillTimeRemaining = 180;

            fillTimer?.Kill();
            fillTimer = Plugin.AddTimer(1, () =>
            {
                if (phase != MatchPhase.Warmup)
                {
                    fillTimer?.Kill();
                    fillTimer = null;
                    return;
                }

                var connectedPlayers = Utils.GetConnectedHumanPlayers();

                ShowFillTimeToPlayers(fillTimeRemaining);

                if (fillTimeRemaining <= 0)
                {
                    fillTimer?.Kill();
                    fillTimer = null;

                    Logger.LogInformation("[Competitive] Fill timer expired.");

                    // your own policy:
                    // either start knife anyway
                    // or check player count and shutdown
                    TryStartKnifeRound("fill_timer_expired");
                    return;
                }

                fillTimeRemaining--;
            }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

            Logger.LogInformation("[Competitive] Fill phase started. Waiting up to 180 seconds for players.");
        }

        private void TryStartKnifeRound(string reason)
        {
            if (knifeRoundStarted)
                return;

            if (phase != MatchPhase.Warmup)
                return;

            knifeRoundStarted = true;
            fillTimer?.Kill();
            fillTimer = null;

            Logger.LogInformation("[Competitive] Starting knife round. Reason: {Reason}", reason);

            StartKnifeRound();
        }

        private void StartKnifeRound()
        {
            if (phase != MatchPhase.Warmup)
                return;

            Logger.LogInformation("[Competitive] Ending warmup and starting knife round.");

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand("mp_warmup_pausetimer 0");
                Server.ExecuteCommand("mp_warmup_end");

                Server.NextFrame(() =>
                {
                    EnterKnifePhase();
                });
            });
        }

        private HookResult OnPlayerConnected(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (Utils.isInvalidPlayer(player))
            {
                return HookResult.Continue;
            }

            if (phase == MatchPhase.Game)
            {
                // @todo In the future we might ask the backend at /internal :) 
                string sidString = player?.SteamID.ToString()!;
                bool isAllowed = annotationsDto.Teams?.AllPlayers.Contains(sidString) ?? false;

                if (!isAllowed)
                {
                    Server.NextFrame(() => {
                        Server.ExecuteCommand($"kickid {player?.Slot} Not part of this match");
                    });

                    return HookResult.Continue;
                }
            }

            Server.NextFrame(() =>
            {
                var targetTeam = annotationsDto.Teams?.FindPlayerTeam(player?.SteamID);

                if (targetTeam is null)
                {
                    targetTeam = Utils.GetTeamWithLeastPlayers();
                }
                
                player?.SwitchTeam(targetTeam.Value);

                Server.NextWorldUpdate(() =>
                {
                    if (player is not null && player.IsValid && !player.PawnIsAlive)
                    {
                        player.Respawn();
                    }
                });

                if (phase == MatchPhase.Warmup)
                {
                    var connectedPlayers = Utils.GetConnectedHumanPlayers();

                    Logger.LogInformation(
                        "[Competitive] Player connected. Humans now connected: {Count}/{Required}",
                        connectedPlayers,
                        RequiredPlayers
                    );

                    if (connectedPlayers >= RequiredPlayers)
                    {
                        TryStartKnifeRound("server_filled");
                    }
                }
            });

            return HookResult.Continue;
        }

        public override void Deactivate()
        {
            knifeRoundStarted = false;
            phase = MatchPhase.Warmup;
            teamChoiceTimer?.Kill();
            teamChoiceTimer = null;
            fillTimer?.Kill();
            fillTimer = null;

            Plugin.RemoveCommand("css_switch", OnSwitchCommand);
            Plugin.RemoveCommand("css_t", onJoinTSideCommand);
            Plugin.RemoveCommand("css_ct", onJoinCtSideCommand);

            Plugin.RemoveCommandListener("jointeam", OnJoinTeamCommand, HookMode.Pre);
            Plugin.RemoveCommandListener("spectate", OnSpectateCommand, HookMode.Pre);
            Plugin.RemoveCommandListener("teammenu", OnTeamMenuCommand, HookMode.Pre);


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
            EnterMatchEndPhase();

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

            Server.NextWorldUpdate(() =>
            {
                if (player != null && player.IsValid && player.PlayerPawn.Value != null)
                {
                    try
                    {
                        // Remove all weapons
                        player.RemoveWeapons();

                        player.GiveNamedItem(CsItem.Knife);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "[Competitive] Failed enforcing knife-only loadout for {Player}", player?.PlayerName);
                        Logger.LogError(ex.Message);
                    }
                }
            });

            return HookResult.Continue;
        }

        private HookResult OnJoinTeamCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (Utils.isInvalidPlayer(player))
                return HookResult.Continue;

            player!.PrintToChat("[ClutchPoint] Team changing is disabled on this server.");
            return HookResult.Handled;
        }

        private HookResult OnSpectateCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (Utils.isInvalidPlayer(player))
                return HookResult.Continue;

            player!.PrintToChat("[ClutchPoint] Spectating is disabled on this server.");
            return HookResult.Handled;
        }

        private HookResult OnTeamMenuCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (Utils.isInvalidPlayer(player))
                return HookResult.Continue;

            return HookResult.Handled;
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
            if (phase == MatchPhase.Knife)
                return;

            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering Knife phase", Plugin.serverName);

            phase = MatchPhase.Knife;

            // @todo disable all weapons, only leave knives, remove round time limits, lower the buy time to 3 seconds and reset the match

            Server.NextFrame(() =>
            {
                // @todo perhaps create a profile/knife.cfg and just exec it? 
                Server.ExecuteCommand("mp_buytime 0");
                Server.ExecuteCommand("mp_buy_anywhere 0");
                Server.ExecuteCommand("mp_startmoney 0");
                Server.ExecuteCommand("mp_playercashawards 0");
                Server.ExecuteCommand("mp_teamcashawards 0");
                Server.ExecuteCommand("mp_free_armor 0");
                Server.ExecuteCommand("mp_teamcashawards 0");
                Server.ExecuteCommand("mp_weapons_allow_map_placed 0");
                Server.ExecuteCommand("mp_freezetime 5");

                Server.ExecuteCommand("mp_restartgame 1");
                
                // Wait 1 second before displaying the HTML message to users 
                Plugin.AddTimer(2f, () =>
                {
                    Server.RunOnTick(Server.TickCount + 1, () =>
                    {
                        foreach (var player in Utilities.GetPlayers())
                        {
                            if (Utils.isInvalidPlayer(player) && !player.IsBot)
                                continue;

                            player.PrintToCenterHtml(
                                "<b><font color='red'>Knife Round!</font></b><br/>Winner of this round gets to choose a team.",
                                15
                            );
                        }
                    });
                });
            });
        }

        private void EnterPostKnifePhase()
        {
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
                Server.ExecuteCommand("exec gamemode_competitive.cfg");
                Server.ExecuteCommand("mp_restartgame 1");
            });
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

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand("exec gamemode_competitive.cfg");
                Server.ExecuteCommand("sv_disable_show_team_select_menu 1");
                Server.ExecuteCommand("mp_friendlyfire 0");

                Server.PrintToChatAll("[ClutchPoint] Live match starting.");

                Server.ExecuteCommand("mp_restartgame 1");
            });
        }

        private void EnterMatchEndPhase()
        {
            Logger.LogInformation("[Bootstrap]: Phase change: {Pod} entering match_end phase", Plugin.serverName);

            string winner = lastRoundWinner switch
            {
                2 => "Terrorists",
                3 => "Counter-Terrorists",
                _ => "Unknown"
            };

            Logger.LogInformation("[Bootstrap] Competitive Match End: Winner: {Winner}", winner);

            Server.PrintToChatAll($"[ClutchPoint] Match finished. Winner: {winner}");

            Plugin.AddTimer(8.0f, async () => await Plugin.GracefulShutdown("map_end"));

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

        private void ShowFillTimeToPlayers(int secondsRemaining)
        {
            var minutes = secondsRemaining / 60;
            var seconds = secondsRemaining % 60;
            var formatted = $"{minutes:00}:{seconds:00}";

            foreach (var player in Utilities.GetPlayers())
            {
                if (Utils.isInvalidPlayer(player) || player!.IsBot)
                    continue;

                player.PrintToCenter($"Waiting for players...\nKnife round starts in {formatted}");
            }
        }
    }
}
