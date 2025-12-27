using RemoteDrive.Framework.ProjFS;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RemoteDrive.Framework
{
    public class VirtualFolder
    {
        public ReadOnlyCollection<PlaceholderInfo> Items { get; }

        public VirtualFolder(List<PlaceholderInfo> items)
        {
            IOrderedEnumerable<PlaceholderInfo> orderedItems = items.OrderBy(item => item.Name, new FileNameComparer());
            Items = orderedItems.ToList().AsReadOnly();
        }

        public bool TryGetPlaceholder(string name, out PlaceholderInfo placeholderInfo)
        {
            foreach (PlaceholderInfo item in Items)
            {
                if (item.Name == name)
                {
                    placeholderInfo = item;
                    return true;
                }
            }
            placeholderInfo = null;
            return false;
        }
    }
}
