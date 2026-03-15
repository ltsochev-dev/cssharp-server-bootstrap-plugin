using Microsoft.Extensions.Logging;
using ServerBootstrap.Config.Dto;
using ServerBootstrap.Contracts;

namespace ServerBootstrap.Controllers
{
    public abstract class GameControllerBase : IGameModeController
    {
        protected readonly ServerBootstrap Plugin;
        protected readonly HtmlWriter htmlWriter;
        protected readonly ServerAnnotationsDto annotationsDto;

        protected ILogger Logger => Plugin.Logger;

        protected GameControllerBase(ServerBootstrap plugin, ServerAnnotationsDto dto)
        {
            Plugin = plugin;
            htmlWriter = new HtmlWriter();
            annotationsDto = dto;
        }

        public abstract string Mode { get; }

        public virtual void Activate() { }
        public virtual void Deactivate() { }
    }
}
