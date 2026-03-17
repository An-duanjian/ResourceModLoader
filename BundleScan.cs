using AddressablesTools.Catalog;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModLoader
{
    class BundleScan
    {
        private ContentCatalogData ccd;
        string local;
        string cache;
        Dictionary<string, BundleFileInstance> bundleCache = new Dictionary<string, BundleFileInstance>();
        Dictionary<string, string> cachePath = new Dictionary<string, string>();
        Dictionary<string, string> bundlePathMap = new Dictionary<string, string>();
        AssetsManager manager = new AssetsManager();
        AssetsManager managerOut = new AssetsManager();
        public BundleScan(ContentCatalogData ccd, string local, string cache)
        {
            this.ccd = ccd;
            this.local = local;
            this.cache = cache;

            Console.WriteLine("正在为缓存目录构建临时索引");
            foreach (var p in Directory.GetDirectories(cache))
            {
                foreach (var sp in Directory.GetDirectories(p))
                {
                    cachePath[Path.GetFileName(sp) + ".bundle"] = Path.Combine(sp, "__data");
                    bundlePathMap[Path.GetFileName(p) + ".bundle"] = Path.GetFileName(sp) + ".bundle";
                }
            }
            Console.WriteLine("正在为游戏资产目录构建临时索引");
            foreach (var p in Directory.GetDirectories(Path.Combine(local, "StreamingAssets", "aa")))
            {
                foreach(var sp in Directory.GetFiles(p))
                {
                    if (!sp.EndsWith(".bundle")) continue;
                    var incomingBundle = manager.LoadBundleFile(sp);
                    var asset = manager.LoadAssetsFileFromBundle(incomingBundle, 0);
                    var assetFile = asset.file;
                    var abdef = assetFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
                    var fab = manager.GetBaseField(asset, abdef);
                    var bundleName = fab["m_Name"].AsString;
                    bundlePathMap[bundleName] = Path.GetFileName(sp);
                }
            }
        }
        public Tuple<string, List<string>> CalculateToReplaceItems(string bundlePath)
        {
            List<string> results = new List<string>();

            var incomingBundle = manager.LoadBundleFile(bundlePath);
            var asset = manager.LoadAssetsFileFromBundle(incomingBundle, 0);
            var assetFile = asset.file;
            var hasUnAddressable = false;

            var abdef = assetFile.GetAssetsOfType(AssetClassID.AssetBundle).First();
            var fab = manager.GetBaseField(asset, abdef);
            var bundleName = fab["m_Name"].AsString;
            if (bundlePathMap.ContainsKey(bundleName))
                bundleName = bundlePathMap[bundleName];

            var targetBundle = GetBundle(bundleName);
            if (targetBundle != null)
            {
                var targetAsset = managerOut.LoadAssetsFileFromBundle(targetBundle, 0);
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
                            if (!ccd.Resources.ContainsKey(addressableKey))
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
        public BundleFileInstance? GetBundle(string bundleName)
        {
            if (bundleCache.ContainsKey(bundleName))
                return bundleCache[bundleName];
            if (!ccd.Resources.ContainsKey(bundleName))
                return null;
            var rl = ccd.Resources[bundleName];
            if (rl == null || rl.Count == 0) return null;
            var bundlePath = rl[0].InternalId;
            if (bundlePath.StartsWith("{App.WebServerConfig.Path}"))
            {
                if (!cachePath.ContainsKey(bundlePath.Replace("{App.WebServerConfig.Path}\\", "")))
                {
                    return null;
                }
                bundlePath = cachePath[bundlePath.Replace("{App.WebServerConfig.Path}\\", "")];
            }
            else if (bundlePath.StartsWith("{UnityEngine.AddressableAssets.Addressables.RuntimePath}"))
            {
                bundlePath = bundlePath.Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}", Path.Combine(local, "StreamingAssets", "aa"));
            }

            bundleCache[bundleName] = managerOut.LoadBundleFile(bundlePath);
            return bundleCache[bundleName];
        }
    }
}
