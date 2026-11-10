using System;

namespace GearPanel
{
    // Free of game and Unity types so the rules can be tested outside the game.
    internal static class GearRules
    {
        public const int MaxStack = 9999;

        // A blank or malformed amount means 1, so the +/- buttons always do something sensible.
        public static int ParseAmount(string text)
        {
            if (!int.TryParse(text, out var amount)) return 1;
            return ClampStack(amount);
        }

        public static int ClampStack(long count) => (int)Math.Max(0L, Math.Min(MaxStack, count));

        public static int Adjusted(int current, int change) => ClampStack((long)current + change);

        public static bool Matches(string query, string displayName, string internalName)
        {
            var needle = query == null ? "" : query.Trim();
            return needle.Length == 0 || Contains(displayName, needle) || Contains(internalName, needle);
        }

        private static bool Contains(string text, string needle) =>
            text != null && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
