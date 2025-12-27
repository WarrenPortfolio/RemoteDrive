using Microsoft.Windows.ProjFS;
using RemoteDrive.Framework.ProjFS;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace RemoteDrive.Framework
{
    public abstract class VirtualFileProvider
    {
        protected object FolderCacheLock { get; } = new object();
        protected ConcurrentDictionary<string, VirtualFolder> FolderCache { get; } = new ConcurrentDictionary<string, VirtualFolder>();

        public VirtualFileSystem FileSystem { get; internal set; }
        public string RootPath { get; private set; }

        public VirtualFileProvider(string rootPath)
        {
            RootPath = rootPath;
        }

        public abstract bool StartVirtualizing();
        public abstract void StopVirtualizing();

        protected abstract bool RequestFileData(string relativePath, byte[] contentId, byte[] providerId, out byte[] fileData);
        protected abstract bool RequestFolderData(string relativePath, out VirtualFolder virtualFolder);

        protected internal virtual List<NotificationMapping> GetNotificationMapping()
        {
            return new List<NotificationMapping>();
        }

        protected string ConvertToRelavtivePath(string path)
        {
            path = Path.GetFullPath(path);
            path = path.Replace(RootPath, string.Empty);
            path = path.Trim(Path.DirectorySeparatorChar);
            return path;
        }

        protected static void SplitPath(string relativePath, out string folderPath, out string fileName)
        {
            int separatorIndex = relativePath.LastIndexOf(Path.PathSeparator);
            if (separatorIndex < 0)
            {
                folderPath = string.Empty;
                fileName = relativePath;
            }
            else
            {
                folderPath = relativePath.Substring(0, separatorIndex);
                fileName = relativePath.Substring(separatorIndex + 1);
            }
        }

        public bool FindFolder(string relativePath, out VirtualFolder virtualFolder)
        {
            if (FolderCache.TryGetValue(relativePath, out virtualFolder))
            {
                return true;
            }

            lock (FolderCacheLock)
            {
                if (FolderCache.TryGetValue(relativePath, out virtualFolder))
                {
                    return true;
                }

                if (RequestFolderData(relativePath, out virtualFolder))
                {
                    FolderCache.TryAdd(relativePath, virtualFolder);
                    return true;
                }
            }

            return false;
        }

        public bool FindPlaceholderInfo(string relativePath, out PlaceholderInfo placeholderInfo)
        {
            SplitPath(relativePath, out string folderPath, out string fileName);

            if (FindFolder(folderPath, out VirtualFolder virtualFolder))
            {
                return virtualFolder.TryGetPlaceholder(fileName, out placeholderInfo);
            }
            placeholderInfo = null;
            return false;
        }

        public bool FindPlaceholderData(string relativePath, byte[] contentId, byte[] providerId, out byte[] fileData)
        {
            return RequestFileData(relativePath, contentId, providerId, out fileData);
        }
    }
}
