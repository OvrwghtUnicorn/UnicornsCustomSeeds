using MelonLoader;

namespace UnicornsCustomSeeds.TemplateUtils
{
    /// <summary>
    /// Mod logging. Everything here is silent unless debug logging is switched on, with the
    /// single exception of <see cref="Critical"/>.
    ///
    /// Players report anything red as a bug, even when the game is working. This mod
    /// legitimately recovers from vanilla faults — a Harmony finalizer catches an NRE from
    /// ChemistryStation's cook operation handler, parks the operation and replays it — and the
    /// stack trace it printed was read as the mod halting the game. So production says nothing
    /// unless the mod is actually broken.
    /// </summary>
    public static class Utility
    {
        /// <summary>
        /// Set from StashManager.InitializeConfig. Assigned rather than looked up so this class
        /// does not depend on StashManager — the dependency points one way.
        /// </summary>
        public static MelonPreferences_Entry<bool> DebugEntry;

        /// <summary>
        /// Read per call, so editing the config file takes effect without a restart. Null-safe:
        /// anything logged before InitializeConfig runs is treated as production.
        /// </summary>
        public static bool IsDebug => DebugEntry != null && DebugEntry.Value;

        /// <summary>
        /// Always prints, debug or not. Reserved for states where the mod is genuinely not
        /// working — a missing AssetBundle, a factory that never initialized — not for anything
        /// it recovers from. One line, no stack, and it tells the player what to do next.
        /// </summary>
        public static void Critical(string msg)
        {
            MelonLogger.Msg(ConsoleColor.Red,
                $"{msg} (enable DebugLogging in this mod's MelonPreferences for details)");
        }

        /// <summary>
        /// Prints an exception together with the mod method that caught it, and says whether it
        /// was handled.
        ///
        /// The plain overload prints only the exception's own type, message and stack, so every
        /// call site looks identical in origin — and indistinguishable from an unhandled crash.
        /// </summary>
        /// <param name="source">
        /// Where it was caught, e.g. "Patch_ChemistryStation_SetCookOperation.Finalizer".
        /// </param>
        /// <param name="handled">
        /// True when the mod recovered from it. False for a genuine failure being reported.
        /// </param>
        public static void PrintException(Exception e, string source, bool handled = true)
        {
            if (!IsDebug) return;

            MelonLogger.Msg(
                handled ? ConsoleColor.DarkYellow : ConsoleColor.Red,
                handled
                    ? $"Caught in {source} — handled by the mod, the game did not halt:"
                    : $"Unhandled in {source}:");
            PrintException(e);
        }

        public static void PrintException(Exception e)
        {
            if (!IsDebug) return;

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
            if (!IsDebug) return;
            MelonLogger.Msg(ConsoleColor.Red, msg);
        }

        public static void Warn(string msg)
        {
            if (!IsDebug) return;
            MelonLogger.Msg(ConsoleColor.Yellow, msg);
        }

        public static void Log(string msg)
        {
            if (!IsDebug) return;
            MelonLogger.Msg(ConsoleColor.DarkMagenta, msg);
        }

        public static void Success(string msg)
        {
            if (!IsDebug) return;
            MelonLogger.Msg(ConsoleColor.Green, msg);
        }
    }
}
