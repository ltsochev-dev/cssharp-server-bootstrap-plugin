using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Collections.Concurrent;

namespace ServerBootstrap
{
    public sealed class HtmlHud
    {
        private readonly BasePlugin plugin;

        private sealed class HudEntry
        {
            public required string Html { get; set; }
            public int RemainingTicks { get; set; }
        }

        private readonly ConcurrentDictionary<ulong, HudEntry> entries = new();
        private const int TickRate = 64;

        public HtmlHud(BasePlugin plugin)
        {
            this.plugin = plugin;
            StartLoop();
        }

        private void StartLoop()
        {
            Server.NextFrame(() => 
            {
                plugin.AddTimer(0.1f, () => 
                {
                    try 
                    {
                        Tick();
                    }
                    catch
                    {
                        // @todo logging, error handling
                    }
                }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
            });
        }

        private void Tick()
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot)
                    continue;

                if (!entries.TryGetValue(player.SteamID, out var entry))
                    continue;

                if (entry.RemainingTicks <= 0)
                {
                    entries.TryRemove(player.SteamID, out _);
                    continue;
                }

                player.PrintToCenterHtml(entry.Html, 1);

                entry.RemainingTicks -= (int)(0.1f * TickRate);
            }
        }

        public void Show(CCSPlayerController player, string html, float seconds)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return;

            entries[player.SteamID] = new HudEntry
            {
                Html = html,
                RemainingTicks = Math.Max(1, (int)(seconds * TickRate))
            };
        }

        public void ShowToAll(string html, float seconds, Func<CCSPlayerController, bool>? filter = null)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot)
                    continue;

                if (filter != null && !filter(player))
                    continue;

                Show(player, html, seconds);
            }
        }

        public void Clear(CCSPlayerController player)
        {
            if (player == null || !player.IsValid)
                return;

            entries.TryRemove(player.SteamID, out _);

            player.PrintToCenterHtml(" ", 1);
        }

        public void ClearAll()
        {
            entries.Clear();

            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot)
                    continue;

                player.PrintToCenterHtml(" ", 1);
            }
        }
    }
}