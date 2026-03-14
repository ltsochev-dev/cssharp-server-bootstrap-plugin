using CounterStrikeSharp.API.Core;

namespace ServerBootstrap.Contracts
{
    public interface IGameModeController
    {
        string Mode { get; }

        void Activate();
        void Deactivate();

        HookResult OnPlayerConnect(EventPlayerConnectFull ev, GameEventInfo info);
        HookResult OnWarmupEnd(EventWarmupEnd ev, GameEventInfo info);
        HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info);
        HookResult OnMatchEnd(EventCsWinPanelMatch ev, GameEventInfo info);
    }
}
