using System;
using System.IO;
using System.Reflection.Metadata;
using AddressablesTools;
using AddressablesTools.Binary;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using AddressablesTools.JSON;
using ResourceModLoader.Mod;
using ResourceModLoader.Mod.Item;
using ResourceModLoader.Module;
using ResourceModLoader.Utils;

namespace ResourceModLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            Init();
            if (addressableMgr == null)
            {
                Log.Wait();
                return;
            }
            if (addressableMgr.Loaded() == 0)
            {
                Log.Warn("当前状态无法进行该操作，请先启动游戏下载资源");
                Log.Wait();
                return;
            }
            ProcessMods();
            ApplyAll();
            addressableMgr.Save();
            Report.Print(Path.Combine(basePath, "mods"));
            Log.Wait();
        }
        static void StartGame()
        {
            if (executable == "") return;

        }
        static void ProcessMods()
        {
            Log.Info("扫描Mods");
            string modsDirectory = Path.Combine(basePath, "mods");

            if (!Directory.Exists(modsDirectory))
            {
                Directory.CreateDirectory(modsDirectory);
            }
            Log.SetupProgress(-1);
            ApplyMod(modsDirectory, 100);
            Log.FinalizeProgress("搜索结束");
        }
        static string basePath = "";
        static string executable = "";
        static BundleScan scan;
        static AddressableMgr addressableMgr;
        static ModContext modContext;
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
                if (appName != "")
                {
                    break;
                }
                currentPath = Directory.GetParent(currentPath).FullName;
            }
            if (appName == "")
            {
                Log.Error("在游戏运行目录下安装该软件");
                return;
            }
            basePath = currentPath;
            executable = Path.Combine(currentPath, appName + ".exe");
            Log.Debug($"使用 {executable} 作为可执行文件");
            string[] appData = File.ReadAllLines(Path.Combine(currentPath, appName + "_Data", "app.info"));
            if (appData.Length < 2)
            {
                Log.Error("Appinfo 不合法");
                return;
            }
            Log.Debug($"{appData[0]} / {appData[1]} ");
            string presistDir = Path.Combine(localPath, appData[0], appData[1], "com.unity.addressables");


            string addressableSettings = File.ReadAllText(Path.Combine(currentPath, appName + "_Data", "StreamingAssets", "aa", "settings.json"));
            int offset1 = addressableSettings.IndexOf("/catalog_") + 9;
            int offset2 = addressableSettings.IndexOf(".hash", offset1);
            string version = addressableSettings.Substring(offset1, offset2 - offset1);
            Log.Debug($"Game Version {version}");

            addressableMgr = new AddressableMgr();
            addressableMgr.Add(Path.Combine(presistDir, "catalog_" + version + ".json"));
            addressableMgr.Add(Path.Combine(currentPath, appName + "_Data", "StreamingAssets", "aa", "catalog.bundle"));
            scan = new BundleScan(addressableMgr, Path.Combine(currentPath, appName + "_Data"), Path.Combine(presistDir, "AssetBundles"));
            modContext = new ModContext(addressableMgr, scan);
        }
        static void ApplyMod(string modPath, int priority)
        {
            if (File.Exists(Path.Combine(modPath, "priority.txt")))
                try { priority = int.Parse(File.ReadAllText(Path.Combine(modPath, "priority.txt"))); } catch (Exception _) { }
            if (File.Exists(Path.Combine(modPath, "优先级.txt")))
                try { priority = int.Parse(File.ReadAllText(Path.Combine(modPath, "优先级.txt"))); } catch (Exception _) { }

            if (File.Exists(Path.Combine(modPath, "replace.txt")))
            {
                Log.StepProgress("Mod扫描 : " + Path.Combine(modPath, "replace.txt"));
                modContext.Add(new ReplaceTxtItem(priority, Path.Combine(modPath, "replace.txt")));
            }
            else
            {
                var filesAll = Directory.GetFiles(modPath);
                Array.Sort(filesAll);
                foreach (var file in filesAll)
                {
                    Log.StepProgress("Mod扫描 : " + file);
                    Report.AddModFile(file);
                    if ((Path.GetExtension(file).ToLower() == ".bundle" || Path.GetFileName(file) == "__data"))
                    {
                        var list = scan.CalculateToReplaceItems(file);
                        modContext.Add(new BundleItem(priority,file,list.Item1,list.Item2));
                    }
                    if (Path.GetExtension(file).ToLower() == ".zip")
                    {
                        string tp = Zip.ExtractAndGetPath(file);
                        if (tp != "")
                            ApplyMod(tp, priority);
                    }
                    if(WrappableFileItem.IsValid(file) && addressableMgr.IsAddressableName((Path.GetFileNameWithoutExtension(file) + "@").Split("@")[0]))
                        modContext.Add(new WrappableFileItem(priority, file));
                }
            }
            var dirs = Directory.GetDirectories(modPath);
            Array.Sort(dirs);
            foreach (var dir in dirs)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName != "_generated")
                {
                    ApplyMod(Path.Combine(modPath, dir), priority);
                }
            }
        }
        static void MergeAndPatchBundles()
        {
            if(!Path.Exists(Path.Combine(basePath,"_generated")))
                Directory.CreateDirectory(Path.Combine(basePath, "_generated"));
            foreach(var bundleName in scan.GetAllBundleName())
            {
                var toPatch = modContext.CollectToPatch(bundleName);
                if (toPatch.Any()) {
                    var conflicts = AB.MergeBundles(scan.GetBundleLocalPath(bundleName), toPatch, Path.Combine(basePath, "_generated", bundleName), (m, a, p,r) => modContext.PostPatch(m,a,p,r));
                    modContext.Redirect(bundleName, Path.Combine(basePath, "_generated", bundleName),"","",true);
                    foreach(var (name,i,c) in conflicts)
                    {
                        Report.Warning(i, $"在修补 {name} 时和 {c} 冲突");
                    }
                }
            }
        }
        static void ApplyAll()
        {
            modContext.Sort();
            MergeAndPatchBundles();
            modContext.ApplyAll();
        }
    }
}