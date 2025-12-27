using Microsoft.Windows.ProjFS;
using System.Collections.Generic;

namespace RemoteDrive.Framework.ProjFS
{
    public class FileNameComparer : Comparer<string>
    {
        public override int Compare(string x, string y) => Utils.FileNameCompare(x, y);
    }
}
