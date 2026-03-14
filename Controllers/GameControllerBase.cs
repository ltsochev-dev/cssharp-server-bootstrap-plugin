using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using ServerBootstrap.Contracts;

namespace ServerBootstrap.Controllers
{
    public abstract class GameControllerBase : IGameModeController
    {
        protected readonly ServerBootstrap Plugin;
        protected ILogger Logger => Plugin.Logger;

        protected GameControllerBase(ServerBootstrap plugin)
        {
            Plugin = plugin;
        }

        public abstract string Mode { get; }

        public virtual void Activate() { }
        public virtual void Deactivate() { }

        public virtual HookResult OnPlayerConnect(EventPlayerConnectFull ev, GameEventInfo info)
            => HookResult.Continue;

        public virtual HookResult OnWarmupEnd(EventWarmupEnd ev, GameEventInfo info)
            => HookResult.Continue;

        public virtual HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info) => HookResult.Continue;

        public virtual HookResult OnMatchEnd(EventCsWinPanelMatch ev, GameEventInfo info) => HookResult.Continue;
    }
}
