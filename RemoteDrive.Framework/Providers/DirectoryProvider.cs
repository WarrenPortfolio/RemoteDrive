using RemoteDrive.Framework.ProjFS;
using System.Collections.Generic;
using System.IO;

namespace RemoteDrive.Framework.Providers
{
    public class DirectoryProvider : VirtualFileProvider
    {
        public string SourceDirectoryPath { get; }
        public string TargetDirectoryPath { get; }

        public DirectoryProvider(string sourceDirectoryPath, string targetDirectoryPath) : base(targetDirectoryPath)
        {
            SourceDirectoryPath = sourceDirectoryPath;
            TargetDirectoryPath = targetDirectoryPath;
        }

        public override bool StartVirtualizing()
        {
            return Directory.Exists(SourceDirectoryPath);
        }

        public override void StopVirtualizing()
        {

        }

        private string GetFullSourcePath(string relativePath)
        {
            return Path.Combine(SourceDirectoryPath, relativePath);
        }

        protected override bool RequestFolderData(string relativePath, out VirtualFolder virtualFolder)
        {
            string sourcePath = GetFullSourcePath(relativePath);

            DirectoryInfo sourceDirectoryInfo = new DirectoryInfo(sourcePath);
            if (sourceDirectoryInfo.Exists)
            {
                List<PlaceholderInfo> placeholders = new List<PlaceholderInfo>();

                foreach (FileSystemInfo fileSystemInfo in sourceDirectoryInfo.GetFileSystemInfos())
                {
                    string placeholderPath = Path.Combine(relativePath, fileSystemInfo.Name);
                    bool isDirectory = fileSystemInfo.Attributes.HasFlag(FileAttributes.Directory);
                    long size = 0;

                    if (fileSystemInfo is FileInfo)
                    {
                        FileInfo fileInfo = fileSystemInfo as FileInfo;

                        size = fileInfo.Length;
                    }

                    placeholders.Add(new PlaceholderInfo()
                    {
                        Name = fileSystemInfo.Name,
                        RelativePath = placeholderPath,
                        IsDirectory = isDirectory,
                        Size = size,
                        CreationTime = fileSystemInfo.CreationTime,
                        LastAccessTime = fileSystemInfo.LastAccessTime,
                        LastWriteTime = fileSystemInfo.LastWriteTime,
                        ChangeTime = fileSystemInfo.LastWriteTime,
                        FileAttributes = fileSystemInfo.Attributes,
                    });
                }

                virtualFolder = new VirtualFolder(placeholders);
                return true;
            }

            virtualFolder = null;
            return false;
        }

        protected override bool RequestFileData(string relativePath, byte[] contentId, byte[] providerId, out byte[] fileData)
        {
            string sourcePath = GetFullSourcePath(relativePath);
            if (File.Exists(sourcePath))
            {
                using (FileStream fileStream = File.OpenRead(sourcePath))
                {
                    fileData = new byte[fileStream.Length];
                    fileStream.Read(fileData, 0, fileData.Length);
                }
                return true;
            }

            fileData = null;
            return false;
        }
    }
}
