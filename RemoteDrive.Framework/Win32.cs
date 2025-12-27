using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace RemoteDrive.Framework
{
    public static class Win32
    {
        public const int MAX_PATH = 260;
        public const int MAX_ALTERNATE = 14;

        public const uint IO_REPARSE_TAG_PROJFS = 0x9000001C;
        public const uint IO_REPARSE_TAG_PROJFS_TOMBSTONE = 0xA0000022;

        public static DateTime ToDateTime(this FILETIME fileTime)
        {
            ulong utcTime = (ulong)fileTime.dwHighDateTime << 32;
            utcTime = utcTime | (uint)fileTime.dwLowDateTime;

            return DateTime.FromFileTimeUtc((long)utcTime);
        }

        // SafeHandle Class
        // https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle?view=net-8.0
        public sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeFindHandle() : base(true) { }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            override protected bool ReleaseHandle()
            {
                return FindClose(handle);
            }
        }

        public enum FILE_ATTRIBUTE : uint
        {
            READONLY = 0x00000001,
            HIDDEN = 0x00000002,
            SYSTEM = 0x00000004,
            DIRECTORY = 0x00000010,
            ARCHIVE = 0x00000020,
            DEVICE = 0x00000040,
            NORMAL = 0x00000080,
            TEMPORARY = 0x00000100,
            SPARSE_FILE = 0x00000200,
            REPARSE_POINT = 0x00000400,
            COMPRESSED = 0x00000800,
            OFFLINE = 0x00001000,
            NOT_CONTENT_INDEXED = 0x00002000,
            ENCRYPTED = 0x00004000,
            INTEGRITY_STREAM = 0x00008000,
            VIRTUAL = 0x00010000,
            NO_SCRUB_DATA = 0x00020000,
            EA = 0x00040000,
            PINNED = 0x00080000,
            UNPINNED = 0x00100000,
            RECALL_ON_OPEN = 0x00040000,
            RECALL_ON_DATA_ACCESS = 0x00400000,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WIN32_FIND_DATA
        {
            public FILE_ATTRIBUTE dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_ALTERNATE)]
            public string cAlternateFileName;
            public uint dwFileType;
            public uint dwCreatorType;
            public uint wFinderFlags;
        }

        public enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        public enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2
        }

        [Flags]
        public enum FindFirstFileExFlags : int
        {
            NONE = 0x0,
            FIND_FIRST_EX_CASE_SENSITIVE = 0x1,
            FIND_FIRST_EX_LARGE_FETCH = 0x2,
            FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 0x4,
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFindHandle FindFirstFileEx(
            string lpFileName,
            FINDEX_INFO_LEVELS fInfoLevelId,
            out WIN32_FIND_DATA lpFindFileData,
            FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            FindFirstFileExFlags dwAdditionalFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool FindNextFile(SafeFindHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool FindClose(IntPtr hFindFile);
    }
}
