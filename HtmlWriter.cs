using CounterStrikeSharp.API;

namespace ServerBootstrap
{

    internal sealed class Entry
    {
        public readonly string Message;
        public readonly int DurationSeconds;
        public int TicksCompleted { get; private set; } = 0;

        public Entry(string message, int duration)
        {
            Message = message;
            DurationSeconds = duration;
        }

        public Entry Tick()
        {
            this.TicksCompleted++;

            return this;
        }

        public bool IsComplete()
        {
            return this.TicksCompleted >= DurationSeconds * 64;
        }
    }

    public sealed class HtmlWriter
    {
        private List<Entry> Tags = new();

        public HtmlWriter() {
            Init();
        }

        private void Init()
        {
            Run();
        }

        private void Run()
        {
            Server.RunOnTick(Server.TickCount + 32, () =>
            {
                Tick();

                Run();
            });
        }

        private void Tick()
        {
            if (Tags.Count == 0)
                return;

            foreach (var player in Utilities.GetPlayers())
            {
                if (!player.IsValid)
                    continue;

                foreach (var entry in Tags)
                {
                    player.PrintToCenterHtml(entry.Message);
                }
            }

            for (int i = Tags.Count - 1; i >= 0; i--)
            {
                Tags[i].Tick();

                if (Tags[i].IsComplete())
                    Tags.RemoveAt(i);
            }
        }

        public void WrteSimpleText(string text, int duration = 10)
        {
            Tags.Add(new Entry(text, duration));
        }
    }
}
