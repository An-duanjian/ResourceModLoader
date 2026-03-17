using System;
using System.IO;
using System.Reflection.Metadata;
using AddressablesTools;
using AddressablesTools.Binary;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using AddressablesTools.JSON;

namespace ModLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            Init();
            if (skip)
            {
                Console.WriteLine("当前状态无法进行该操作，请先启动游戏下载资源");
                Console.WriteLine("按任意键退出程序");
                Console.ReadKey();
                return;
            }
            if(savePath == "" || ccd == null)
            {
                Console.WriteLine("按任意键退出程序");
                Console.ReadKey();
                return;
            }
            ProcessMods();
            ApplyAll();
            Save();
            Console.WriteLine("按任意键退出程序");
            Console.ReadKey();
        }
        static void StartGame()
        {
            if (executable == "") return;

        }
        static void ProcessMods()
        {
            string modsDirectory = Path.Combine(basePath, "mods");

            if (!Directory.Exists(modsDirectory))
            {
                Directory.CreateDirectory(modsDirectory);
            }
            ApplyMod(modsDirectory);
        }
        static ContentCatalogData ccd;
        static string basePath = "";
        static string savePath = "";
        static string executable = "";
        static bool skip = false;
        static BundleScan scan;
        static List<Tuple<string, string, string,string>> collected = new List<Tuple<string,string, string, string>>();
        static Dictionary<String,ResourceLocation> generatedAbDict = new Dictionary<String,ResourceLocation>();
        static void Init()
        {
            string localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow"); 
            string currentPath = Directory.GetCurrentDirectory();
            string appName = "";
            for (int i = 0; i < 2; i++)
            {
                string[] allSubDirs = Directory.GetDirectories(currentPath);
                foreach (string subDir in allSubDirs)
                {
                    if (subDir.EndsWith("_Data"))
                    {
                        appName = subDir.Substring(0, subDir.Length - 5);
                    }
                }
                if(appName != "")
                {
                    break;
                }
                currentPath = Directory.GetParent(currentPath).FullName;
            }
            if(appName == "")
            {
                Console.Error.WriteLine("在游戏运行目录下安装该软件");
                return;
            }
            basePath = currentPath;
            executable = Path.Combine(currentPath, appName + ".exe");
            Console.WriteLine($"使用 {executable} 作为可执行文件");
            string[] appData = File.ReadAllLines(Path.Combine(currentPath, appName+"_Data","app.info"));
            if(appData.Length < 2) {
                Console.Error.WriteLine("Appinfo 不合法");
                return;
            }
            Console.WriteLine($"{appData[0]} / {appData[1]} ");
            string presistDir = Path.Combine(localPath, appData[0], appData[1], "com.unity.addressables");


            string addressableSettings = File.ReadAllText(Path.Combine(currentPath, appName + "_Data", "StreamingAssets", "aa", "settings.json"));
            int offset1 = addressableSettings.IndexOf("/catalog_")+9;
            int offset2 = addressableSettings.IndexOf(".hash",offset1);
            string version = addressableSettings.Substring(offset1, offset2 - offset1);
            Console.WriteLine($"Game Version {version}");

            string currentHash = File.ReadAllText(Path.Combine(presistDir, "catalog_" + version + ".hash"));
            string lastHash = "";
            if(Path.Exists(Path.Combine(presistDir, "catalog_" + version + ".hash_modded")))
            {
                lastHash = File.ReadAllText(Path.Combine(presistDir, "catalog_" + version + ".hash_modded"));
            }
            savePath = Path.Combine(presistDir, "catalog_" + version + ".json");
            if (!Path.Exists(savePath))
            {
                skip = true;
                return;
            }
            string catalogFile = Path.Combine(presistDir, "catalog_" + version + ".json.modded_bak");
            if (lastHash != currentHash || !Path.Exists(catalogFile))
            {
                File.Copy(savePath, catalogFile,true);
            }
            File.Copy(Path.Combine(presistDir, "catalog_" + version + ".hash"), Path.Combine(presistDir, "catalog_" + version + ".hash_modded"), true);
            ccd = AddressablesCatalogFileParser.FromJsonString(File.ReadAllText(catalogFile));
            scan = new BundleScan(ccd, Path.Combine(currentPath, appName + "_Data"), Path.Combine(presistDir, "AssetBundles"));
        }
        static void Save()
        {
            File.WriteAllText(savePath, AddressablesCatalogFileParser.ToJsonString(ccd));
        }
        static void ApplyMod(string modPath)
        {
            bool performCommonReplace = true;
            if (File.Exists(Path.Combine(modPath, "replace.txt")))
            {
                performCommonReplace = false;
                string[] files = File.ReadAllLines(Path.Combine(modPath, "replace.txt"));
                foreach (string file in files)
                {
                    if (file.Trim().StartsWith("#"))
                        continue;
                    string[] def = file.Split(':');
                    if (def.Length < 2)
                        continue;
                    string name = def[0];
                    string bundle = def[1];
                    string req = def.Length < 3 ? def[1] : def[2];
                    string bundleFile = Path.Combine(modPath, bundle);
                    CollectApplyBundleMod(name, bundleFile,"",req);
                }
            }
            foreach(var file in Directory.GetFiles(modPath))
            {
                if (Path.GetExtension(file).ToLower() == ".png" || Path.GetExtension(file).ToLower() == ".jpg")
                {
                    string bundle = AB.createImageAbSingle(file);
                    CollectApplyBundleMod(Path.GetFileNameWithoutExtension(file), bundle,"d");
                }
                if((Path.GetExtension(file).ToLower() == ".bundle"  || Path.GetFileName(file)== "__data") && performCommonReplace)
                {
                    var list = scan.CalculateToReplaceItems(file);
                    foreach(var item in list.Item2)
                    {
                        CollectApplyBundleMod(item, file, "" , list.Item1);
                    }
                }
            }
            foreach(var dir in Directory.GetDirectories(modPath))
            {
                if(dir != "." && dir != "..")
                {
                    ApplyMod(Path.Combine(modPath,dir));
                }
            }
        }
        static void CollectApplyBundleMod(string name, string bundleFile,string containerRedir= "",string depReq = "")
        {
            collected.Add(new Tuple<string, string, string,string>(name, bundleFile, containerRedir,depReq));
        }
        static void ApplyAll()
        {
            Dictionary<string, string> applied = new Dictionary<string, string>();
            foreach(var item in collected)
            {
                Console.WriteLine($"重定向 {item.Item1} -> {item.Item2}");
                if (applied.ContainsKey(item.Item1))
                {
                    Console.WriteLine($"[W] {item.Item1} 正在被多次patch。上次重定向到 {applied[item.Item1]}");
                }
                applied.Add(item.Item1, item.Item2);
                ApplyBundleMod(item.Item1, item.Item2,item.Item3,item.Item4);
            }
        }
        static void ApplyBundleMod(string name,string bundleFile,string containerRedir = "",string depReq = "")
        {
            if (!Path.Exists(bundleFile))
            {
                Console.WriteLine($"[W] {bundleFile} 不存在");
                return;
            }

            if (!ccd.Resources.ContainsKey(name))
            {
                Console.WriteLine($"[W] {name} 不在Addressable系统中");
                return;
            }

            foreach (var location in ccd.Resources[name])
            {
                if (location.ProviderId == "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider")
                {
                    location.InternalId = "file://" + bundleFile;
                    Console.WriteLine($"Bundle {name} --> {location.InternalId}");
                    continue;
                }
                else if (location.ProviderId != "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider")
                {
                    Console.WriteLine($"[W] 处理中 {name}");
                    Console.WriteLine($"[W] 不支持的提供者类型 {location.ProviderId}");
                    continue;
                }
                ResourceLocation? firstDep = null;
                if (location.Dependencies != null)
                {
                    firstDep = location.Dependencies.First();
                }
                else if (location.DependencyKey != null)
                {
                    firstDep = ccd.Resources[location.DependencyKey].First();
                }
                if (firstDep == null)
                {
                    Console.WriteLine($"[W] 处理中 {name}");
                    Console.WriteLine($"[W] 没找到依赖的文件");
                    continue;
                }
                if(depReq != "" && depReq != firstDep.PrimaryKey && "patched." + depReq != firstDep.PrimaryKey)
                {
                    continue;
                }
                var rl = getAbIdFor(bundleFile, firstDep);

                if (rl == null)
                {
                    Console.WriteLine($"[W] 处理中 {name}");
                    Console.WriteLine($"[W] 无法创建虚拟Bundle");
                    continue;
                }


                if (location.Dependencies != null)
                {
                    location.Dependencies.Clear();
                    location.Dependencies.Add(rl);
                }
                else if (location.DependencyKey != null)
                {
                    location.DependencyKey = rl.PrimaryKey;
                }

                if(containerRedir != "")
                {
                    location.InternalId = containerRedir; 
                }
                Console.WriteLine($"Resource {name} --> {rl.PrimaryKey}");
            }
        }
        static ResourceLocation? getAbIdFor(string path, ResourceLocation reference)
        {
            if (generatedAbDict.ContainsKey(path))
                return generatedAbDict[path];

            var rl = new ResourceLocation();
            rl.ProviderId = reference.ProviderId;
            rl.InternalId = "file://" + path;
            rl.PrimaryKey = "patched." + reference.PrimaryKey;
            rl.Type = reference.Type;
            AssetBundleRequestOptions opt = new AssetBundleRequestOptions();
            opt.Hash = "";
            opt.BundleName = rl.PrimaryKey;
            if (reference.Data is WrappedSerializedObject { Object: AssetBundleRequestOptions abro, Type: SerializedType t })
            {
                opt.ComInfo = abro.ComInfo;
                rl.Data = new WrappedSerializedObject(t, opt);
            }
            else
            {
                return null;
            }

            ccd.Resources[rl.PrimaryKey] = new List<ResourceLocation> { rl };
            generatedAbDict[path] = rl;
            return rl;
        }
    }
}