using AssetsTools.NET.Extra;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod
{
    class ModContext
    {
        private AddressableMgr  addressableMgr;
        private BundleScan scan;
        public List<IModItem> modItems = new List<IModItem>();
        Dictionary<string,string> lastRedirect = new Dictionary<string,string>();
        public ModContext(AddressableMgr mgr, BundleScan scan) {
            this.addressableMgr = mgr;
            this.scan = scan;
        }
        public void Redirect(string name,string bundleFile,string container,string originalBundle,bool noReport = false)
        {
            if (lastRedirect.ContainsKey(name))
            {
                return;
            }
            if (!noReport)
            {
                Report.AddModFile(bundleFile);
                Report.AddTaintFile(bundleFile, name);
            }
            addressableMgr.ApplyBundleMod(name, bundleFile, container, originalBundle);
        }
        public void Add(IModItem modItem)
        {
            modItems.Add(modItem);
        }
        public List<string> CollectToPatch(string name)
        {
            List<string> result = new List<string>();
            foreach(IModItem modItem in modItems)
            {
                var collected = modItem.GetToPatchBundles(name);
                result.AddRange(collected);
            }
            foreach(string r in result)
            {
                Report.AddModFile(r);
                Report.AddTaintFile(r, name);
            }
            return result;
        }
        public void PostPatch(AssetsManager m,AssetsFileInstance[] a, Dictionary<long, string>[] patched, List<List<Tuple<int, long, byte[]>>> patches)
        {
            foreach(var modItem in modItems)
            {
                modItem.PostPatch(m, a, patched,patches);
            }
        }
        public void ApplyAll()
        {
            foreach (var modItem in modItems)
                modItem.Apply(this);
        }
        public void Sort()
        {
            modItems.Sort((a, b) => a.priority - b.priority);
        }
    }
}
