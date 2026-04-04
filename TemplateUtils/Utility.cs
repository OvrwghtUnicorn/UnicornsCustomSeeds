using MelonLoader;

namespace UnicornsCustomSeeds.TemplateUtils
{
    public static class Utility
    {
        public static void PrintException(Exception e)
        {
            MelonLogger.Msg(ConsoleColor.Red, e.GetType().FullName);
            MelonLogger.Msg(ConsoleColor.DarkRed, e.Message);

            if (!string.IsNullOrWhiteSpace(e.StackTrace))
            {
                MelonLogger.Msg(ConsoleColor.DarkRed, "StackTrace:");
                foreach (var line in e.StackTrace.Split('\n'))
                    MelonLogger.Msg(ConsoleColor.DarkRed, $"    {line.Trim()}");
            }

            if (e.InnerException != null)
            {
                MelonLogger.Msg(ConsoleColor.DarkRed, "  -- Inner Exception --");
                PrintException(e.InnerException);
            }
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
