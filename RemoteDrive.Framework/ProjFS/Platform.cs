using System;

namespace RemoteDrive.Framework.ProjFS
{
    public static class Platform
    {
        public static bool IsCaseSensitive { get; internal set; } = false;

        public static StringComparer PathComparer => IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        public static StringComparison PathComparison => IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }
}
