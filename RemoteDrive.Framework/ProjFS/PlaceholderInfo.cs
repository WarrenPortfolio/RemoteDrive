using Microsoft.Windows.ProjFS;
using System;
using System.Diagnostics;
using System.IO;

namespace RemoteDrive.Framework.ProjFS
{
    public class PlaceholderInfo
    {
        public string Name { get; set; }
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public DateTime ChangeTime { get; set; }
        public FileAttributes FileAttributes { get; set; }
        public byte[] ContentId { get; set; }
        public byte[] ProviderId { get; set; }
    }

    public static class PlaceholderInfoExtensions
    {
        public static HResult WritePlaceholderInfo(this VirtualizationInstance instance, PlaceholderInfo info)
        {
            // validate the directory file attribute
            Debug.Assert(info.FileAttributes.HasFlag(FileAttributes.Directory) == info.IsDirectory);

            // validate the placeholder id lengths
            Debug.Assert(info.ContentId.Length <= instance.PlaceholderIdLength);
            Debug.Assert(info.ProviderId.Length <= instance.PlaceholderIdLength);

            return instance.WritePlaceholderInfo(
                relativePath: info.RelativePath,
                creationTime: info.CreationTime,
                lastAccessTime: info.LastAccessTime,
                lastWriteTime: info.LastWriteTime,
                changeTime: info.ChangeTime,
                fileAttributes: info.FileAttributes,
                endOfFile: info.Size,
                isDirectory: info.IsDirectory,
                contentId: info.ContentId,
                providerId: info.ProviderId);
        }

        public static bool Add(this IDirectoryEnumerationResults results, PlaceholderInfo info)
        {
            return results.Add(
                fileName: info.Name,
                fileSize: info.Size,
                isDirectory: info.IsDirectory,
                fileAttributes: info.FileAttributes,
                creationTime: info.CreationTime,
                lastAccessTime: info.LastAccessTime,
                lastWriteTime: info.LastWriteTime,
                changeTime: info.ChangeTime);
        }
    }
}
