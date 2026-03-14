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
    }
}
