namespace ServerBootstrap
{
    public static class HudTemplates
    {
        public static string KnifeRound()
        {
            return
                "<font color='#52c7ff'><b>CLUTCHPOINT</b></font><br>" +
                "<font color='#ffd54a'><b>KNIFE ROUND</b></font><br>" +
                "<font color='#ffffff'>Winner gets to choose side</font><br>" +
                "<font color='#cfcfcf'>Use <b>!ct</b> or <b>!t</b> after the round</font>";
        }

        public static string WaitingForPlayers(int connected, int required)
        {
            return
                "<font color='#52c7ff'><b>CLUTCHPOINT</b></font><br>" +
                "<font color='#ffd54a'><b>WAITING FOR PLAYERS</b></font><br>" +
                $"<font color='#ffffff'>{connected}/{required} connected</font>";
        }

        public static string MatchLive()
        {
            return
                "<font color='#52c7ff'><b>CLUTCHPOINT</b></font><br>" +
                "<font color='#4caf50'><b>MATCH LIVE</b></font><br>" +
                "<font color='#ffffff'>Good luck, have fun</font>";
        }

        public static string SideChoice()
        {
            return
                "<font color='#52c7ff'><b>CLUTCHPOINT</b></font><br>" +
                "<font color='#ffd54a'><b>SIDE CHOICE</b></font><br>" +
                "<font color='#ffffff'>Knife round winner must choose</font><br>" +
                "<font color='#cfcfcf'>Commands: <b>!ct</b> or <b>!t</b></font>";
        }
    }
}