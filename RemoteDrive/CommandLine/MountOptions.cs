using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteDrive.CommandLine
{
    internal class MountOptions
    {
        [Value(0, MetaName = "Virtualization Root Path", Required = true)]
        public string VirtualizationRootPath { get; set; }
    }

    [Verb("mount", HelpText ="")]
    internal class DirectoryMountOptions : MountOptions
    {
        [Value(1, MetaName = "Source Path", Required = true)]
        public string SourcePath { get; set; }
    }
}
