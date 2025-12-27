using Microsoft.Windows.ProjFS;
using RemoteDrive.Framework.ProjFS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteDrive.Framework
{
    public class VirtualFileSystem : IRequiredCallbacks
    {
        private const uint MaxFileStreamBufferSize = 64 * 1024;

        private ConcurrentDictionary<Guid, ActiveEnumeration> ActiveEnumerations { get; }
        private ConcurrentDictionary<int, CancellationTokenSource> ActiveCommands { get; }

        public VirtualFileProvider Provider { get; }
        public VirtualizationInstance VirtualizationInstance { get; }

        public VirtualFileSystem(VirtualFileProvider provider)
        {
            ActiveEnumerations = new ConcurrentDictionary<Guid, ActiveEnumeration>();
            ActiveCommands = new ConcurrentDictionary<int, CancellationTokenSource>();

            Provider = provider;
            Provider.FileSystem = this;

            // Optional Notification Mappings
            List<NotificationMapping> notificationMappings = provider.GetNotificationMapping();

            // Create the virtualization root directory if it doesn't already exist
            VirtualizationInstance = new VirtualizationInstance(
                virtualizationRootPath: provider.RootPath,
                poolThreadCount: 0,
                concurrentThreadCount: 0,
                enableNegativePathCache: false,
                notificationMappings)
            {
                OnCancelCommand = OnCancelCommand
            };
        }

        public bool Start()
        {
            if (Provider.StartVirtualizing())
            {
                HResult hr = VirtualizationInstance.StartVirtualizing(this);
                return hr == HResult.Ok;
            }
            return false;
        }

        public void Stop()
        {
            VirtualizationInstance.StopVirtualizing();
        }

        #region [    Async Tasks    ]

        private bool TryRegisterCommand(int commandId, out CancellationTokenSource cancellationTokenSource)
        {
            cancellationTokenSource = new CancellationTokenSource();
            return ActiveCommands.TryAdd(commandId, cancellationTokenSource);
        }

        private bool TryCompleteCommand(int commandId, HResult result)
        {
            if (ActiveCommands.TryGetValue(commandId, out CancellationTokenSource cancellationTokenSource))
            {
                VirtualizationInstance.CompleteCommand(commandId, result);
                cancellationTokenSource.Dispose();
                return true;
            }
            return false;
        }

        private void StartDirectoryEnumerationTask(int commandId, Guid enumerationId, string relativePath)
        {
            HResult hr = HResult.Ok;
            try
            {
                if (Provider.FindFolder(relativePath, out VirtualFolder virtualFolder))
                {
                    ActiveEnumeration activeEnumeration = new ActiveEnumeration(virtualFolder.Items);
                    if (!ActiveEnumerations.TryAdd(enumerationId, activeEnumeration))
                    {
                        hr = HResult.InternalError;
                    }
                }
                else
                {
                    hr = HResult.PathNotFound;
                }
            }
            catch (Exception)
            {
                hr = HResult.InternalError;
            }
            finally
            {
                TryCompleteCommand(commandId, hr);
            }
        }

        private void GetPlaceholderInfoTask(int commandId, string relativePath)
        {
            HResult hr = HResult.Ok;
            try
            {
                if (Provider.FindPlaceholderInfo(relativePath, out PlaceholderInfo placeholderInfo))
                {
                    hr = VirtualizationInstance.WritePlaceholderInfo(placeholderInfo);
                }
                else
                {
                    hr = HResult.PathNotFound;
                }
            }
            catch (Exception)
            {
                hr = HResult.InternalError;
            }
            finally
            {
                TryCompleteCommand(commandId, hr);
            }
        }

        private void GetFileDataTask(int commandId, string relativePath, ulong byteOffset, uint length, Guid dataStreamId, byte[] contentId, byte[] providerId)
        {
            HResult hr = HResult.Ok;
            try
            {
                if (Provider.FindPlaceholderData(relativePath, contentId, providerId, out byte[] byteArray))
                {
                    Debug.Assert(byteOffset == 0);
                    Debug.Assert(byteArray.Length == length);

                    uint bufferSize = Math.Min(length, MaxFileStreamBufferSize);
                    try
                    {
                        using (IWriteBuffer writeBuffer = VirtualizationInstance.CreateWriteBuffer(
                            byteOffset,
                            bufferSize,
                            out ulong alignedByteOffset,
                            out uint alignedLength))
                        {
                            int bufferOffset = 0;

                            int remainingDataSize = byteArray.Length;
                            while (remainingDataSize > 0)
                            {
                                int bytesToCopy = Math.Min(remainingDataSize, (int)bufferSize);

                                writeBuffer.Stream.Seek(0, System.IO.SeekOrigin.Begin);
                                writeBuffer.Stream.Write(byteArray, bufferOffset, bytesToCopy);

                                HResult writeResult = VirtualizationInstance.WriteFileData(dataStreamId, writeBuffer, alignedByteOffset, (uint)bytesToCopy);
                                if (writeResult != HResult.Ok)
                                {
                                    hr = HResult.InternalError;
                                    break;
                                }

                                alignedByteOffset += (uint)bytesToCopy;
                                bufferOffset += bytesToCopy;
                                remainingDataSize -= bytesToCopy;
                            }
                        }
                    }
                    catch (OutOfMemoryException)
                    {
                        hr = HResult.OutOfMemory;
                    }
                    catch (Exception)
                    {
                        hr = HResult.InternalError;
                    }
                }
            }
            catch (Exception)
            {
                hr = HResult.InternalError;
            }
            finally
            {
                TryCompleteCommand(commandId, hr);
            }
        }

        #endregion

        #region  [    Required Callbacks    ]

        public HResult StartDirectoryEnumerationCallback(int commandId, Guid enumerationId, string relativePath, uint triggeringProcessId, string triggeringProcessImageFileName)
        {
            if (TryRegisterCommand(commandId, out CancellationTokenSource cancellationTokenSource))
            {
                Task.Run(() => { StartDirectoryEnumerationTask(commandId, enumerationId, relativePath); });
                return HResult.Pending;
            }
            return HResult.InternalError;
        }

        public HResult EndDirectoryEnumerationCallback(Guid enumerationId)
        {
            if (ActiveEnumerations.TryRemove(enumerationId, out ActiveEnumeration activeEnumeration))
            {
                return HResult.Ok;
            }
            return HResult.InternalError;
        }

        public HResult GetDirectoryEnumerationCallback(int commandId, Guid enumerationId, string filterFileName, bool restartScan, IDirectoryEnumerationResults result)
        {
            if (ActiveEnumerations.TryGetValue(enumerationId, out ActiveEnumeration activeEnumeration))
            {
                if (restartScan)
                {
                    activeEnumeration.RestartEnumeration(filterFileName);
                }
                else
                {
                    activeEnumeration.TrySaveFilterString(filterFileName);
                }

                HResult hr = HResult.Ok;

                int enumerationResultCount = 0;
                while (activeEnumeration.IsCurrentValid)
                {
                    if (result.Add(activeEnumeration.Current))
                    {
                        activeEnumeration.MoveNext();
                        ++enumerationResultCount;
                    }
                    else
                    {
                        if (enumerationResultCount == 0)
                        {
                            hr = HResult.InsufficientBuffer;
                        }
                        break;
                    }
                }

                return hr;
            }
            return HResult.InternalError;
        }

        public HResult GetPlaceholderInfoCallback(int commandId, string relativePath, uint triggeringProcessId, string triggeringProcessImageFileName)
        {
            if (TryRegisterCommand(commandId, out CancellationTokenSource cancellationTokenSource))
            {
                Task.Run(() => { GetPlaceholderInfoTask(commandId, relativePath); });
                return HResult.Pending;
            }
            return HResult.InternalError;
        }

        public HResult GetFileDataCallback(int commandId, string relativePath, ulong byteOffset, uint length, Guid dataStreamId, byte[] contentId, byte[] providerId, uint triggeringProcessId, string triggeringProcessImageFileName)
        {
            if (TryRegisterCommand(commandId, out CancellationTokenSource cancellationTokenSource))
            {
                Task.Run(() => { GetFileDataTask(commandId, relativePath, byteOffset, length, dataStreamId, contentId, providerId); });
                return HResult.Pending;
            }
            return HResult.InternalError;
        }

        #endregion

        #region [    Optional Callbacks    ]

        private void OnCancelCommand(int commandId)
        {
            if (ActiveCommands.TryRemove(commandId, out CancellationTokenSource cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
        }

        #endregion

        public HResult UpdatePlaceholderFile(PlaceholderInfo fileInfo)
        {
            UpdateType updateFlags = UpdateType.AllowReadOnly | UpdateType.AllowDirtyData | UpdateType.AllowDirtyMetadata | UpdateType.AllowTombstone;
            HResult result = VirtualizationInstance.UpdateFileIfNeeded(
                 relativePath: fileInfo.RelativePath,
                 creationTime: fileInfo.CreationTime,
                 lastAccessTime: fileInfo.LastAccessTime,
                 lastWriteTime: fileInfo.LastWriteTime,
                 changeTime: fileInfo.ChangeTime,
                 fileAttributes: fileInfo.FileAttributes,
                 endOfFile: fileInfo.Size,
                 contentId: fileInfo.ContentId,
                 providerId: fileInfo.ProviderId,
                 updateFlags,
                 out UpdateFailureCause failureReason);
            if (result != HResult.Ok)
            {
                throw new NotSupportedException(failureReason.ToString());
            }
            return result;
        }

        public HResult DeletePlaceholderFile(string relativePath)
        {
            UpdateType updateFlags = UpdateType.AllowReadOnly | UpdateType.AllowDirtyData | UpdateType.AllowDirtyMetadata | UpdateType.AllowTombstone;
            HResult result = VirtualizationInstance.DeleteFile(relativePath, updateFlags, out UpdateFailureCause failureReason);
            if (result != HResult.Ok)
            {
                throw new NotSupportedException(failureReason.ToString());
            }
            return result;
        }

        public void UpdatePalceholderDirectories(string relativePath, Dictionary<string, HResult> updatedDirectories)
        {
            string parentDirectory = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                if (!updatedDirectories.ContainsKey(parentDirectory))
                {
                    updatedDirectories[parentDirectory] = UpdatePalceholderDirectory(parentDirectory);
                }
                parentDirectory = Path.GetDirectoryName(parentDirectory);
            }
        }

        public HResult UpdatePalceholderDirectory(string relativePath)
        {
            string path = Path.Combine(Provider.RootPath, relativePath);

            Win32.SafeFindHandle fileHandle = Win32.FindFirstFileEx(
                path,
                Win32.FINDEX_INFO_LEVELS.FindExInfoStandard,
                out Win32.WIN32_FIND_DATA fileData,
                Win32.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                Win32.FindFirstFileExFlags.FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY
            );

            if (!fileHandle.IsInvalid)
            {
                if (fileData.dwFileAttributes.HasFlag(Win32.FILE_ATTRIBUTE.REPARSE_POINT))
                {
                    if (fileData.dwReserved0 == Win32.IO_REPARSE_TAG_PROJFS_TOMBSTONE)
                    {
                        UpdateType updateFlags = UpdateType.AllowReadOnly | UpdateType.AllowDirtyData | UpdateType.AllowDirtyMetadata | UpdateType.AllowTombstone;
                        HResult result = VirtualizationInstance.DeleteFile(relativePath, updateFlags, out UpdateFailureCause failureReason);
                        if (result != HResult.Ok)
                        {
                        }
                        return result;
                    }
                    else
                    {
                        return MarkDirectoryAsPlaceholder(path);
                    }
                }
            }

            return HResult.Ok;
        }

        public HResult MarkDirectoryAsPlaceholder(string targetDirectoryPath)
        {
            HResult result = VirtualizationInstance.MarkDirectoryAsPlaceholder(targetDirectoryPath, null, null);
            if (result != HResult.Ok)
            {
            }
            return result;
        }
    }
}
