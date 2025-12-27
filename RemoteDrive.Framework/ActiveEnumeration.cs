using Microsoft.Windows.ProjFS;
using RemoteDrive.Framework.ProjFS;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RemoteDrive.Framework
{
    internal class ActiveEnumeration
    {
        private ReadOnlyCollection<PlaceholderInfo> PlaceholderFiles { get; }
        private IEnumerator<PlaceholderInfo> FileEnumerator { get; set; }
        private string FilterString { get; set; } = null;

        public ActiveEnumeration(ReadOnlyCollection<PlaceholderInfo> placeholderFiles)
        {
            PlaceholderFiles = placeholderFiles;
            ResetEnumerator();
            MoveNext();
        }

        public bool IsCurrentValid { get; private set; }

        public PlaceholderInfo Current => FileEnumerator.Current;

        public void RestartEnumeration(string filter)
        {
            ResetEnumerator();
            IsCurrentValid = FileEnumerator.MoveNext();
            SaveFilter(filter);
        }

        public bool MoveNext()
        {
            IsCurrentValid = FileEnumerator.MoveNext();

            while (IsCurrentValid && IsCurrentHidden())
            {
                IsCurrentValid = FileEnumerator.MoveNext();
            }

            return IsCurrentValid;
        }

        public bool TrySaveFilterString(string filter)
        {
            if (FilterString == null)
            {
                SaveFilter(filter);
                return true;
            }

            return false;
        }

        public string GetFilterString()
        {
            return FilterString;
        }

        private static bool FileNameMatchesFilter(string name, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            if (filter == "*")
            {
                return true;
            }

            return Utils.IsFileNameMatch(name, filter);
        }

        private void SaveFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                FilterString = string.Empty;
            }
            else
            {
                FilterString = filter;
                if (IsCurrentValid && IsCurrentHidden())
                {
                    MoveNext();
                }
            }
        }

        private bool IsCurrentHidden()
        {
            return !FileNameMatchesFilter(Current.Name, GetFilterString());
        }

        private void ResetEnumerator()
        {
            FileEnumerator = PlaceholderFiles.GetEnumerator();
        }
    }
}
