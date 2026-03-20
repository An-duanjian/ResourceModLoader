using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod
{
    internal abstract class IModItem
    {
        public int priority;
        protected IModItem(int priority)
        {
            this.priority = priority;
        }
        virtual public void Apply(ModContext context) {  }
        virtual public void PostPatch(AssetsManager manager, AssetsFileInstance[] assets, Dictionary<long, string>[] patched, List<List<Tuple<int, long, byte[]>>> patches) {  }
        virtual public List<string> GetToPatchBundles(string targetBundleName) { return []; }
    }
}
