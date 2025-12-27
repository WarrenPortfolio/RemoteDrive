using Microsoft.Windows.ProjFS;
using Perforce.P4;
using RemoteDrive.Framework;
using RemoteDrive.Framework.ProjFS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using P4File = Perforce.P4.File;

namespace RemoteDrive.Provider.Perforce
{
    public class PerforceProvider : VirtualFileProvider
    {
        private string DepotPath { get; }
        private PerforceSettings Settings {  get; }

        private Server Server { get; }
        private Repository Repo { get; }
        private Client Client { get; }

        public PerforceProvider(PerforceSettings settings, string depotPath, string targetPath) : base(targetPath)
        {
            DepotPath = depotPath;

            Settings = settings;

            Server = new Server(new ServerAddress(settings.ServerAndPort));
            Repo = new Repository(Server);
            Client = Repo.GetClient(settings.ClientName);
        }

        public override bool StartVirtualizing()
        {
            Options options = new Options();
            options["ProgramName"] = Settings.AppName;
            options["ProgramVersion"] = Settings.AppVersion;

            return Repo.Connection.Connect(options);
        }

        public override void StopVirtualizing()
        {
            Repo.Connection.Disconnect();
        }

        protected string GetDepotPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return DepotPath;
            }

            return string.Concat(DepotPath, "/", relativePath.Replace("\\", "/"));
        }

        protected string GetRelativePath(FileSpec fileSpec)
        {
            string relativePath = fileSpec.DepotPath.Path.Replace(DepotPath, "");
            relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            relativePath = relativePath.Trim(Path.DirectorySeparatorChar);

            return relativePath;
        }

        protected override bool RequestFolderData(string relativePath, out VirtualFolder virtualFolder)
        {
            List<PlaceholderInfo> placeholders = new List<PlaceholderInfo>();

            FileSpec fileSpec = new DepotPath(GetDepotPath(relativePath) + "/*");

            IList<string> depotDirectories = Repo.GetDepotDirs(null, fileSpec.ToString());
            if (depotDirectories.Count > 0)
            {
                foreach (string depotDir in depotDirectories)
                {
                    string name = Path.GetFileName(depotDir);
                    string placeholderPath = Path.Combine(relativePath, name);

                    DateTime changeTime = DateTime.Now;
                    placeholders.Add(new PlaceholderInfo()
                    {
                        Name = name,
                        RelativePath = placeholderPath,
                        Size = 0,
                        IsDirectory = true,
                        CreationTime = changeTime,
                        LastAccessTime = changeTime,
                        LastWriteTime = changeTime,
                        ChangeTime = changeTime,
                        FileAttributes = FileAttributes.Directory,
                    });
                }
            }

            Options opts = new GetFileMetaDataCmdOptions(GetFileMetadataCmdFlags.FileSize, string.Empty, string.Empty, -1, string.Empty, string.Empty, string.Empty);
            IList<FileMetaData> depotFiles = Repo.GetFileMetaData(opts, fileSpec);
            if (depotFiles != null)
            {
                foreach (FileMetaData file in depotFiles)
                {
                    string name = Path.GetFileName(file.DepotPath.Path);
                    string placeholderPath = Path.Combine(relativePath, name);

                    DateTime changeTime = DateTime.Now;
                    placeholders.Add(new PlaceholderInfo()
                    {
                        Name = name,
                        RelativePath = placeholderPath,
                        Size = file.FileSize,
                        IsDirectory = false,
                        CreationTime = file.HeadTime,
                        LastAccessTime = changeTime,
                        LastWriteTime = file.HeadTime,
                        ChangeTime = changeTime,
                        FileAttributes = FileAttributes.ReadOnly,
                    });
                }
            }

            if (placeholders.Count > 0)
            {
                virtualFolder = new VirtualFolder(placeholders);
                return true;
            }

            virtualFolder = null;
            return false;
        }

        protected override bool RequestFileData(string relativePath, byte[] contentId, byte[] providerId, out byte[] fileData)
        {
            FileSpec fileSpec = new DepotPath(GetDepotPath(relativePath));

            Options options = new GetFileContentsCmdOptions(GetFileContentsCmdFlags.Suppress, null);
            IList<object> fileContents = Repo.GetFileContentsEx(options, fileSpec);
            if (fileContents != null && fileContents.Count > 0)
            {
                object fileContent = fileContents[0];
                if (fileContent is string)
                {
                    string text = fileContent as string;
                    fileData = Encoding.UTF8.GetBytes(text);
                }
                else if (fileContent is byte[])
                {
                    fileData = fileContent as byte[];
                }
                else
                {
                    throw new NotImplementedException();
                }
                return true;
            }

            fileData = null;
            return false;
        }

        #region [    Perforce Operations    ]

        public Task SyncAsync(int changeNumber)
        {
            return Task.Run(() =>
            {
                IList<FileSpec> syncFiles = null;

                lock (FolderCacheLock)
                {
                    FolderCache.Clear();
                    FileSystem.VirtualizationInstance.ClearNegativePathCache(out uint totalEntryNumber);

                    VersionSpec versionSpec = new ChangelistIdVersion(changeNumber);
                    FileSpec fileSpec = FileSpec.ClientSpec(DepotPath + "/...", versionSpec);

                    Options opts = new SyncFilesCmdOptions(SyncFilesCmdFlags.ServerOnly);
                    syncFiles = Client.SyncFiles(opts, fileSpec);
                }

                if (syncFiles != null && syncFiles.Count > 0)
                {
                    Dictionary<string, HResult> updatedDirectories = new Dictionary<string, HResult>();

                    foreach (FileSpec file in syncFiles)
                    {
                        Options opts = new GetFileMetaDataCmdOptions(GetFileMetadataCmdFlags.FileSize, string.Empty, string.Empty, -1, string.Empty, string.Empty, string.Empty);
                        IList<FileMetaData> fileMetaData = Repo.GetFileMetaData(opts, file);
                        if (fileMetaData != null && fileMetaData.Count == 1)
                        {
                            string placeholderPath = GetRelativePath(file);

                            if (fileMetaData[0].HeadAction == FileAction.Added || fileMetaData[0].HeadAction == FileAction.Updated)
                            {
                                string name = Path.GetFileName(placeholderPath);

                                DateTime changeTime = DateTime.Now;
                                PlaceholderInfo info = new PlaceholderInfo()
                                {
                                    Name = name,
                                    RelativePath = placeholderPath,
                                    Size = fileMetaData[0].FileSize,
                                    IsDirectory = false,
                                    CreationTime = fileMetaData[0].HeadTime,
                                    LastAccessTime = changeTime,
                                    LastWriteTime = fileMetaData[0].HeadTime,
                                    ChangeTime = changeTime,
                                    FileAttributes = FileAttributes.ReadOnly,
                                };

                                FileSystem.UpdatePalceholderDirectories(placeholderPath, updatedDirectories);
                                FileSystem.UpdatePlaceholderFile(info);
                            }
                            else if (fileMetaData[0].HeadAction == FileAction.Delete)
                            {
                                FileSystem.DeletePlaceholderFile(placeholderPath);
                            }
                        }
                    }
                }
            });
        }

        public Task ValidateAsync()
        {
            return Task.Run(() =>
            {
                DateTime currentTime = DateTime.Now;

                Stack<string> dirsToValidate = new Stack<string>();
                dirsToValidate.Push(RootPath);

                while (dirsToValidate.Count > 0)
                {
                    string path = dirsToValidate.Pop();
                    string searchPath = Path.Combine(path, "*");

                    Win32.SafeFindHandle fileHandle = Win32.FindFirstFileEx(
                        searchPath,
                        Win32.FINDEX_INFO_LEVELS.FindExInfoStandard,
                        out Win32.WIN32_FIND_DATA fileData,
                        Win32.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                        IntPtr.Zero,
                        Win32.FindFirstFileExFlags.FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY
                    );

                    if (!fileHandle.IsInvalid)
                    {
                        do
                        {
                            // ignore current and up directory aliases
                            if (fileData.cFileName == "." || fileData.cFileName == "..")
                                continue;

                            string fullPath = Path.Combine(path, fileData.cFileName);
                            string relativePath = ConvertToRelavtivePath(fullPath);

                            if (fileData.dwFileAttributes.HasFlag(Win32.FILE_ATTRIBUTE.DIRECTORY))
                            {
                                if (fileData.dwFileAttributes.Equals(Win32.FILE_ATTRIBUTE.REPARSE_POINT))
                                {
                                    if (fileData.dwReserved0 == Win32.IO_REPARSE_TAG_PROJFS)
                                    {
                                        dirsToValidate.Push(fullPath);
                                    }
                                    else if (fileData.dwReserved0 == Win32.IO_REPARSE_TAG_PROJFS_TOMBSTONE)
                                    {
                                        bool directoryHasFiles = false;

                                        FileSpec fileSpec = FileSpec.LocalSpec(Path.Combine(fullPath, "..."), VersionSpec.Have);
                                        IList<FileMetaData> fileMeataData = Repo.GetFileMetaData(new Options(), fileSpec);
                                        foreach (var item in fileMeataData)
                                        {
                                            if (item.Action != FileAction.Delete && item.Action != FileAction.MoveDelete)
                                            {
                                                directoryHasFiles = true;
                                                break;
                                            }
                                        }

                                        if (directoryHasFiles)
                                        {
                                            dirsToValidate.Push(fullPath);
                                            FileSystem.MarkDirectoryAsPlaceholder(fullPath);
                                        }
                                    }
                                }
                                else
                                {
                                    FileSpec fileSpec = FileSpec.LocalSpec(Path.Combine(fullPath, "..."), VersionSpec.Have);

                                    Options opts = new Options(FilesCmdFlags.None, 1);
                                    IList<P4File> files = Repo.GetFiles(opts, fileSpec);
                                    if (files != null && files.Count > 0)
                                    {
                                        dirsToValidate.Push(fullPath);
                                        FileSystem.MarkDirectoryAsPlaceholder(fullPath);
                                    }
                                }
                            }
                            else
                            {
                                if (fileData.dwFileAttributes.HasFlag(FileAttributes.ReadOnly))
                                {
                                    FileSpec fileSpec = FileSpec.LocalSpec(fullPath, VersionSpec.Have);
                                    IList<FileMetaData> fileMeataData = Repo.GetFileMetaData(new Options(), fileSpec);
                                    if (fileMeataData != null && fileMeataData.Count > 0)
                                    {
                                        DateTime lastAccessTime = fileData.ftLastAccessTime.ToDateTime();
                                        DateTime lastWriteTime = fileData.ftLastWriteTime.ToDateTime();
                                        DateTime creationTime = fileData.ftCreationTime.ToDateTime();

                                        const double daysToRetainFullFile = 21.0;
                                        if ((currentTime - lastAccessTime).TotalDays > daysToRetainFullFile &&
                                            (currentTime - lastWriteTime).TotalDays > daysToRetainFullFile &&
                                            (currentTime - creationTime).TotalDays > daysToRetainFullFile)
                                        {
                                            FileSystem.DeletePlaceholderFile(relativePath);
                                        }
                                    }
                                }
                            }
                        }
                        while (Win32.FindNextFile(fileHandle, out fileData));
                    }
                }
            });
        }

        #endregion
    }
}
