using AddressablesTools.Catalog;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader
{
    class BundleScan
    {
        private AddressableMgr ccd;
        string local;
        string cache;
        Dictionary<string, AssetsFileInstance> bundleAssetCache = new Dictionary<string, AssetsFileInstance>();
        Dictionary<string, string> bundlePathLocal = new Dictionary<string, string>();
        Dictionary<string, List<Tuple<string, string>>> bundlePathMap = new Dictionary<string, List<Tuple<string, string>>>();
        public BundleScan(AddressableMgr ccd, string local, string cache)
        {
            this.ccd = ccd;
            this.local = local;
            this.cache = cache;

            Log.Info("正在为缓存目录构建临时索引");
            foreach (var p in Directory.GetDirectories(cache))
            {
                foreach (var sp in Directory.GetDirectories(p))
                {
                    bundlePathLocal[Path.GetFileName(sp) + ".bundle"] = Path.Combine(sp, "__data");
                    RegisterBundlePath(Path.Combine(sp, "__data"), Path.GetFileName(sp) + ".bundle");
                }
            }
            Log.Info("正在为游戏资产目录构建临时索引");
            foreach (var p in Directory.GetDirectories(Path.Combine(local, "StreamingAssets", "aa")))
            {
                foreach(var sp in Directory.GetFiles(p))
                {
                    if (!sp.EndsWith(".bundle")) continue;
                    bundlePathLocal[Path.GetFileName(sp)] = sp;
                    RegisterBundlePath(sp, Path.GetFileName(sp));
                }
            }
        }
        private void RegisterBundlePath(string path,string bundleFileName)
        {
            AssetsManager manager = new AssetsManager();
            var incomingBundle = manager.LoadBundleFile(path);
            var asset = manager.LoadAssetsFileFromBundle(incomingBundle, 0);
            var assetFile = asset.file;
            var abdef = assetFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var fab = manager.GetBaseField(asset, abdef);
            var bundleName = fab["m_Name"].AsString;
            string firstNonMetaName = FindFirstNonMetaName(asset, manager);
            Tuple<string,string> info = new Tuple<string,string>(bundleFileName,firstNonMetaName);
            if (bundlePathMap.ContainsKey(bundleName))
            {
                foreach(var pair in bundlePathMap[bundleName])
                {
                    if (pair.Item2 == firstNonMetaName)
                        Log.Warn($"完全重复项目{pair.Item1}@{pair.Item2} 在 {bundleName}(incoming={bundleFileName})");
                }
                bundlePathMap[bundleName].Append(info);
            }
            else
                bundlePathMap[bundleName] = new List<Tuple<string, string>> { info };
        }
        public Tuple<string, List<string>> CalculateToReplaceItems(string bundlePath)
        {
            AssetsManager manager = new AssetsManager();
            List<string> results = new List<string>();

            var incomingBundle = manager.LoadBundleFile(bundlePath);
            var asset = manager.LoadAssetsFileFromBundle(incomingBundle, 0);
            var assetFile = asset.file;
            var hasUnAddressable = false;

            var abdef = assetFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var fab = manager.GetBaseField(asset, abdef);
            string bundleName = FindBundleName(fab["m_Name"].AsString, asset, manager);

            var targetAsset = GetBundle(bundleName);
            if (targetAsset != null)
            {
                var targetAssetFile = targetAsset.file;

                foreach (var assetInfo in assetFile.AssetInfos)
                {
                    if (assetInfo.GetTypeId(assetFile) == (int)AssetClassID.AssetBundle)
                        continue;
                    var bf = manager.GetBaseField(asset, assetInfo);
                    var nameObj = bf["m_Name"];
                    if (nameObj.IsDummy)
                        continue;
                    var addressableKey = nameObj.AsString;
                    if (addressableKey == null)
                        continue;

                    foreach (var targetAssetInfo in targetAssetFile.AssetInfos)
                    {
                        var bf1 = manager.GetBaseField(targetAsset, targetAssetInfo);
                        var nameObj1 = bf1["m_Name"];
                        if (nameObj1.IsDummy)
                            continue;
                        var addressableKey1 = nameObj1.AsString;
                        if (addressableKey1 == null || addressableKey != addressableKey1)
                            continue;

                        long v1Start = assetInfo.GetAbsoluteByteOffset(assetFile);
                        long v1Size = assetInfo.ByteSize;
                        assetFile.Reader.Position = (int)v1Start;
                        var buf1 = assetFile.Reader.ReadBytes((int)v1Size);

                        long v2Start = targetAssetInfo.GetAbsoluteByteOffset(targetAssetFile);
                        long v2Size = targetAssetInfo.ByteSize;
                        targetAssetFile.Reader.Position = (int)v2Start;
                        var buf2 = targetAssetFile.Reader.ReadBytes((int)v2Size);


                        if (!buf1.Equals(buf2))
                        {
                            if (!ccd.IsAddressableName(addressableKey))
                            {
                                hasUnAddressable = true;
                                break;
                            }
                            results.Add(nameObj.AsString);
                        }
                        break;
                    }
                    if (hasUnAddressable) break;
                }
            }
            if (hasUnAddressable || results.Count == 0)
            {
                results.Clear();
                results.Add(bundleName);
            }

            return new Tuple<string, List<string>>(bundleName, results);
        }
        
        public string FindFirstNonMetaName(AssetsFileInstance asset, AssetsManager manager)
        {
            var assetFile = asset.file;
            foreach (var asf in assetFile.AssetInfos)
            {
                if (asf.GetTypeId(assetFile) == (int)AssetClassID.AssetBundle) continue;
                var tField = manager.GetBaseField(asset, asf);
                if (tField["m_Name"].IsDummy || tField["m_Name"].TypeName != "string") continue;
                return tField["m_Name"].AsString;
            }
            return "";
        }
        public string FindBundleName(string bundleMetaName, AssetsFileInstance af, AssetsManager manager)
        {
            if (!bundlePathMap.ContainsKey(bundleMetaName))
                return bundleMetaName;
            var list = bundlePathMap[bundleMetaName];
            if(list.Count == 1)
                return list[0].Item1;
            string firstNonMetaName = FindFirstNonMetaName(af, manager);
            foreach(var d in list)
            {
                if(d.Item2 == firstNonMetaName)
                    return d.Item1;
            }
            Log.Warn($"{bundleMetaName}有多个对应，但是无法找到第一个文件名为'{firstNonMetaName}'的对应");
            return list[0].Item1;
        }
        public AssetsFileInstance? GetBundle(string bundleName)
        {
            if (bundleAssetCache.ContainsKey(bundleName))
                return bundleAssetCache[bundleName];
            if (!ccd.IsAddressableName(bundleName))
                return null;

            var bundlePath = GetBundleLocalPath(bundleName);
            if(bundlePath == "") return null;
            AssetsManager managerOut = new AssetsManager();
            var bundle = managerOut.LoadBundleFile(bundlePath);
            bundleAssetCache[bundleName] = managerOut.LoadAssetsFileFromBundle(bundle, 0);
            return bundleAssetCache[bundleName];
        }

        public string GetBundleLocalPath(string bundleName)
        {
            var rl = ccd.GetFirstAvailableResourceLocationList(bundleName);
            if (rl == null || rl.Count == 0) return "";
            var bundlePath = rl[0].InternalId;
            if (bundlePath.StartsWith("{App.WebServerConfig.Path}"))
            {
                if (!this.bundlePathLocal.ContainsKey(bundlePath.Replace("{App.WebServerConfig.Path}\\", "")))
                {
                    return "";
                }
                bundlePath = this.bundlePathLocal[bundlePath.Replace("{App.WebServerConfig.Path}\\", "")];
            }
            else if (bundlePath.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}"))
            {
                bundlePath = bundlePath.Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", Path.Combine(local, "StreamingAssets", "aa"));
            }
            return bundlePath;
        }
    }
}
