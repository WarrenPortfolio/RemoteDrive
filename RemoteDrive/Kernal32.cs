using System.Runtime.InteropServices;

namespace RemoteDrive
{
    internal enum CtrlType
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6,
    }

    internal static class Kernal32
    {
        public delegate bool HandlerRoutine(CtrlType dwCtrlType);

        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine handlerRoutine, bool add);
    }
}
