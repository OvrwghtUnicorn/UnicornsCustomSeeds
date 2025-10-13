using MelonLoader;

namespace UnicornsCustomSeeds
{
    public static class Utility
    {
        public static void PrintException(Exception e)
        {
            MelonLogger.Msg(ConsoleColor.Red, e.Source);
            MelonLogger.Msg(ConsoleColor.DarkRed, e.Message);
        }

        public static void Error(string msg)
        {
            MelonLogger.Msg(ConsoleColor.Red, msg);
        }

        public static void Log(string msg)
        {
            MelonLogger.Msg(ConsoleColor.DarkMagenta, msg);
        }

        public static void Success(string msg)
        {
            MelonLogger.Msg(ConsoleColor.Green, msg);
        }
    }
}
