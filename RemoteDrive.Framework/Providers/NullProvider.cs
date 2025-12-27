using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteDrive.Framework.Providers
{
    public class NullProvider : VirtualFileProvider
    {
        public NullProvider(string rootPath) : base(rootPath)
        {
        }

        public override bool StartVirtualizing()
        {
            return true;
        }

        public override void StopVirtualizing()
        {
            
        }

        protected override bool RequestFolderData(string relativePath, out VirtualFolder virtualFolder)
        {
            virtualFolder = null;
            return false;
        }

        protected override bool RequestFileData(string relativePath, byte[] contentId, byte[] providerId, out byte[] fileData)
        {
            throw new NotImplementedException();
        }
    }
}
