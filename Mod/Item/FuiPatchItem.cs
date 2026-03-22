using AssetsTools.NET.Extra;
using ResourceModLoader.Mod.Patch;
using ResourceModLoader.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Mod.Item
{
    class FuiPatchItem : IModItem
    {
        string ext = "";
        List<string> source = new List<string>();
        List<string> target = new List<string>();
        public FuiPatchItem(int priority,string source) : base(priority)
        {
            this.source = new List<string> { source };
            ext = Path.GetExtension(source);
        }

        private IPatch GetContext()
        {
            if (ext == ".bin") return new BinPatch();
            return null;
        }
        public override void Init(ModContext context, AddressableMgr addressableMgr, BundleScan bundleScan)
        {
            foreach(var (name,bundle) in addressableMgr.GetAllResources())
            {
                if(name.EndsWith("_fui"))
                    target.Add(bundle);
            }
        }
        public override bool MergeToThis(IModItem modItem)
        {
            if(modItem is FuiPatchItem cpi)
            {
                source.AddRange(cpi.source);
            }
            return base.MergeToThis(modItem);
        }
        public override bool RequirePatch(string name)
        {
            return target.Contains(name);
        }
        public override void PostPatch(string bundleName, AssetsManager manager, BundleFileInstance bundle, AssetsFileInstance[] assets, Dictionary<long, string>[] patched, List<List<Tuple<int, long, byte[]>>> patches)
        {
            if (ext == "") return;
            foreach (var asset in assets)
            {
                if(asset == null) continue;
                var container = Utils.AB.GetContainerDic(manager, asset);
                foreach(var file in asset.file.AssetInfos)
                {
                    if (file.GetTypeId(asset.file) == (int)AssetClassID.AssetBundle) continue;
                    var field = manager.GetBaseField(asset, file);
                    if (field == null) continue;
                    var nameField = field["m_Name"];
                    if (nameField == null || nameField.IsDummy) continue;
                    if(nameField.AsString.EndsWith("_fui"))
                    {
                        var patch = GetContext();
                        patch.Init(manager, asset, file);
                        foreach(var src in this.source)
                            if (patch.PerformPatch(src))
                                Report.AddTaintFile(src, bundleName);
                        patch.Finalize(manager, asset, file);
                    }
                }
            }
        }
        public static bool IsValid(string source)
        {
            return Path.GetFileNameWithoutExtension(source).EndsWith(".fui_patch");
        }
    }
}
