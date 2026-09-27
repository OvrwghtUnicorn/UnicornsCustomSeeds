using MelonLoader;

namespace UnicornsCustomSeeds.TemplateUtils
{
    public static class Utility
    {
        /// <summary>
        /// Prints an exception together with the mod method that caught it, and states that it
        /// was handled.
        ///
        /// The plain overload prints only the exception's own type, message and stack, so
        /// every call site in the mod produces output that looks identical in origin — and
        /// indistinguishable from an unhandled crash. That cost real debugging time: a caught,
        /// recovered NullReferenceException from a Harmony finalizer was read as the mod
        /// halting the game.
        /// </summary>
        /// <param name="source">
        /// Where it was caught, e.g. "Patch_ChemistryStation_SetCookOperation.Finalizer".
        /// </param>
        /// <param name="handled">
        /// True when the mod recovered from it. False for a genuine failure being reported.
        /// </param>
        public static void PrintException(Exception e, string source, bool handled = true)
        {
            MelonLogger.Msg(
                handled ? ConsoleColor.DarkYellow : ConsoleColor.Red,
                handled
                    ? $"Caught in {source} — handled by the mod, the game did not halt:"
                    : $"Unhandled in {source}:");
            PrintException(e);
        }

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
