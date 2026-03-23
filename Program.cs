using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Xml.Linq;
using AddressablesTools;
using AddressablesTools.Binary;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using AddressablesTools.JSON;
using ResourceModLoader.Mod;
using ResourceModLoader.Mod.Item;
using ResourceModLoader.Module;
using ResourceModLoader.Tool;
using ResourceModLoader.Tool.SpriteAnimTool;
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
            if (args.Length > 0)
            {
                if (args[0] == "tool")
                {
                    Tool(args);
                    return;
                }else if (args[0] == "dev")
                {
                    isDevMode = true;
                }
                else
                {
                    Log.Warn("未识别的参数");
                    return;
                }
            }
            TryCopy();
            scan.Scan();
            do
            {
                ProcessMods();
                ApplyAll();
                addressableMgr.Save();
                Report.Print(Path.Combine(basePath, "mods"));
                if (isDevMode)
                {
                    addressableMgr.Reset();
                    Report.Reset();
                    modContext = new ModContext(addressableMgr, scan);
                    Log.Info("继续运行将重新应用mod");
                }
                Log.Wait();
            } while (isDevMode);
        }
        static void Tool(string[] args)
        {
            if (args.Length < 2)
            {
                PrintToolHelp();
                return;
            }

            string toolName = args[1];
            string[] remain = new string[Math.Max(args.Length - 2, 0)];
            Array.Copy(args, 2, remain, 0, remain.Length);

            if (toolName == "proto-export")
            {
                ProtoExportTool.Invoke(remain, addressableMgr, scan);
            }
            else if (toolName == "sprite-anim")
            {
                HandleSpriteAnimTool(remain);
            }
            else
            {
                Log.Warn($"未知的工具: {toolName}");
                PrintToolHelp();
            }
        }

        static void HandleSpriteAnimTool(string[] args)
        {
            if (args.Length == 0)
            {
                PrintSpriteAnimHelp();
                return;
            }

            try
            {
                var cmd = args[0].ToLowerInvariant();
                
                if (cmd == "export")
                {
                    if (!HasArg(args, "-in") && !HasArg(args, "-out") && !HasArg(args, "-class"))
                    {
                        RunSpriteAnimBatchExport();
                        return;
                    }

                    // 用法: tool sprite-anim export -in <bundle> -out <exportDir>
                    var inPath = GetArg(args, "-in");
                    var outDir = GetArg(args, "-out");

                    if (string.IsNullOrEmpty(inPath) || string.IsNullOrEmpty(outDir))
                    {
                        Log.Error("export 需要 -in -out 参数，或不带参数使用自动批处理模式");
                        PrintSpriteAnimHelp();
                        return;
                    }

                    Directory.CreateDirectory(outDir);
                    AbExporter.Run(inPath, outDir, string.Empty);
                }
                else if (cmd == "import")
                {
                    var atlasStr = GetArg(args, "-atlas");
                    int atlasSize = string.IsNullOrEmpty(atlasStr) ? 4096 : int.Parse(atlasStr);

                    if (!HasArg(args, "-in") && !HasArg(args, "-jsonDir") && !HasArg(args, "-out") && !HasArg(args, "-class"))
                    {
                        RunSpriteAnimBatchImport(atlasSize);
                        return;
                    }

                    // 用法: tool sprite-anim import -in <bundle> -jsonDir <exportDir> -out <newBundle> [-atlas 4096]
                    var inPath = GetArg(args, "-in");
                    var jsonDir = GetArg(args, "-jsonDir");
                    var outPath = GetArg(args, "-out");

                    if (string.IsNullOrEmpty(inPath) || string.IsNullOrEmpty(jsonDir) || string.IsNullOrEmpty(outPath))
                    {
                        Log.Error("import 需要 -in -jsonDir -out 参数，或不带路径参数使用自动批处理模式");
                        PrintSpriteAnimHelp();
                        return;
                    }

                    AbImporter.Run(inPath, jsonDir, outPath, string.Empty, atlasSize);
                }
                else
                {
                    Log.Error($"未知的sprite-anim子命令: {cmd}");
                    PrintSpriteAnimHelp();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"sprite-anim工具执行失败: {ex.Message}");
                Log.Info(ex.StackTrace);
            }
        }

        static void RunSpriteAnimBatchExport()
        {
            var (importDir, exportDir) = EnsureSpriteAnimWorkingDirectories();
            var bundleFiles = EnumerateSpriteAnimBundles(importDir);
            if (bundleFiles.Length == 0)
            {
                Log.Warn($"[sprite-anim] 未在 {importDir} 找到任何 AB 包");
                return;
            }

            Log.Info($"[sprite-anim] 自动导出模式: import={importDir}, export={exportDir}");
            foreach (var bundlePath in bundleFiles)
            {
                string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
                string spriteDir = Path.Combine(exportDir, bundleName, "sprite");
                Directory.CreateDirectory(spriteDir);
                Log.Info($"[sprite-anim] 导出 {Path.GetFileName(bundlePath)} -> {spriteDir}");
                AbExporter.Run(bundlePath, spriteDir, string.Empty);
            }
        }

        static void RunSpriteAnimBatchImport(int atlasSize)
        {
            var (importDir, exportDir) = EnsureSpriteAnimWorkingDirectories();
            var bundleFiles = EnumerateSpriteAnimBundles(importDir);
            if (bundleFiles.Length == 0)
            {
                Log.Warn($"[sprite-anim] 未在 {importDir} 找到任何 AB 包");
                return;
            }

            Log.Info($"[sprite-anim] 自动回填模式: import={importDir}, export={exportDir}");
            foreach (var bundlePath in bundleFiles)
            {
                string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
                string jsonDir = Path.Combine(exportDir, bundleName, "sprite");
                if (!Directory.Exists(jsonDir))
                {
                    Log.Warn($"[sprite-anim] 跳过 {Path.GetFileName(bundlePath)}: 未找到 {jsonDir}");
                    continue;
                }

                if (!HasClipJson(jsonDir))
                {
                    Log.Warn($"[sprite-anim] 跳过 {Path.GetFileName(bundlePath)}: {jsonDir} 内未找到任何 clip.json");
                    continue;
                }

                string outBundle = Path.Combine(
                    importDir,
                    $"{Path.GetFileNameWithoutExtension(bundlePath)}_patched{Path.GetExtension(bundlePath)}");
                Log.Info($"[sprite-anim] 回填 {Path.GetFileName(bundlePath)} <- {jsonDir}");
                AbImporter.Run(bundlePath, jsonDir, outBundle, string.Empty, atlasSize);
            }
        }

        static (string importDir, string exportDir) EnsureSpriteAnimWorkingDirectories()
        {
            string importDir = Path.Combine(basePath, "import");
            string exportDir = Path.Combine(basePath, "export");
            Directory.CreateDirectory(importDir);
            Directory.CreateDirectory(exportDir);
            return (importDir, exportDir);
        }

        static string[] EnumerateSpriteAnimBundles(string inputDir)
        {
            if (!Directory.Exists(inputDir))
                return Array.Empty<string>();

            return Directory
                .GetFiles(inputDir, "*", SearchOption.TopDirectoryOnly)
                .Where(IsSpriteAnimBundleCandidate)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        static bool IsSpriteAnimBundleCandidate(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
                return true;

            return extension == ".ab"
                || extension == ".bundle"
                || extension == ".assetbundle"
                || extension == ".unity3d";
        }

        static bool HasClipJson(string rootDir)
        {
            return Directory.EnumerateFiles(rootDir, "clip.json", SearchOption.AllDirectories).Any();
        }

        static bool HasArg(string[] args, string name)
        {
            return args.Any(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        static string? GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }

        static void PrintToolHelp()
        {
            Log.Info("可用工具:");
            Log.Info("  proto-export  - 导出Proto");
            Log.Info("  sprite-anim   - AssetBundle动画导出/回填工具");
            Log.Info("");
            Log.Info("使用 'tool <toolName> help' 查看详细用法");
        }

        static void PrintSpriteAnimHelp()
        {
            Log.Info("sprite-anim 工具 - AssetBundle动画处理");
            Log.Info("");
            Log.Info("自动批处理模式（基于 mods 同级目录的 import/export）:");
            Log.Info("  tool sprite-anim export");
            Log.Info("    从 <basePath>/import 扫描 AB 包，导出到 <basePath>/export/<bundleName>/sprite/");
            Log.Info("  tool sprite-anim import [-atlas 4096]");
            Log.Info("    从 <basePath>/export/<bundleName>/sprite/ 扫描 clip.json+PNG，从 <basePath>/import 查找原 AB 包，生成 <bundleName>_patched.* 到 <basePath>/import/");
            Log.Info("");
            Log.Info("导出动画:");
            Log.Info("  tool sprite-anim export -in <bundle.ab> -out <exportDir>");
            Log.Info("");
            Log.Info("回填动画:");
            Log.Info("  tool sprite-anim import -in <bundle.ab> -jsonDir <exportDir> -out <newBundle.ab> [-atlas 4096]");
            Log.Info("");
            Log.Info("参数说明:");
            Log.Info("  -in        输入bundle路径");
            Log.Info("  -out       输出目录或文件路径");
            Log.Info("  -jsonDir   导出目录（包含clip.json的根目录）");
            Log.Info("  -atlas     图集最大尺寸（默认4096）");
        }
        static void StartGame()
        {
            if (executable == "") return;
        }
        static void ProcessMods()
        {
            string modsDirectory = Path.Combine(basePath, "mods");
            Log.Info("扫描Mods");
            Log.SetupProgress(-1);
            ApplyMod(modsDirectory, 100);
            Log.FinalizeProgress("搜索结束");
        }
        static string basePath = "";
        static string executable = "";
        static BundleScan scan;
        static AddressableMgr addressableMgr;
        static ModContext modContext;
        static bool isDevMode = false;
        static string DiscoverGameDir(string user)
        {
            string currentPath = Directory.GetCurrentDirectory();

            if (Path.Exists(Path.Combine(currentPath, "possible_names.txt")))
            {
                const string DETECT_STR = "[Subsystems] Discovering subsystems at path ";
                var possibleNames = File.ReadAllText(Path.Combine(currentPath, "possible_names.txt")).Split("\n");
                foreach (var possibleName in possibleNames)
                {
                    var detectPath = Path.Combine(possibleName.Trim().Replace("{User}", user), "Player.log");
                    if (File.Exists(detectPath))
                    {
                        var unityLog = File.ReadAllText(detectPath);
                        var sp = unityLog.IndexOf(DETECT_STR);
                        if (sp >= 0)
                        {
                            sp += DETECT_STR.Length;
                            var ep = unityLog.IndexOf("\n", sp);
                            var path = unityLog.Substring(sp, ep - sp);
                            path = Path.GetDirectoryName(Path.GetDirectoryName(path));
                            return path;
                        }
                    }
                }
            }
            return currentPath;
        }
        static void TryCopy()
        {
            string currentPath = Directory.GetCurrentDirectory();

            if (Path.Exists(Path.Combine(currentPath, "copies.txt")))
            {
                string self = Process.GetCurrentProcess().MainModule.FileName;
                string dep = Path.Combine(Path.GetDirectoryName(self), "PVRTexLib.dll");
                if (!Path.Exists(Path.Combine(basePath, Path.GetFileName(self))))
                {
                    File.Copy(self, Path.Combine(basePath, Path.GetFileName(self)));
                    if(Path.Exists(dep))
                        File.Copy(dep, Path.Combine(basePath, Path.GetFileName(dep)),true);

                    Log.Info("已将本程序拷贝到 " + Path.Combine(basePath, Path.GetFileName(self)));
                    Log.Info("将来如果要撤销该程序的影响，请到该目录下删除mods文件夹后再次运行目录下的该程序");
                    Log.Info("即将复制文件并修补游戏文件，如果你已经了解，请按下回车键来继续操作");
                    Log.Wait();
                }
                string modsDirectory = Path.Combine(basePath, "mods");
                var copyItems = File.ReadAllText(Path.Combine(currentPath, "copies.txt")).Split("\n");
                foreach (var copyItem in copyItems)
                {
                    var p = Path.Combine(currentPath, copyItem.Trim());
                    var target = Path.Combine(modsDirectory, copyItem.Trim());
                    var dir = Path.GetDirectoryName(target);
                    if(!Path.Exists(dir))
                        Directory.CreateDirectory(dir);
                    if (File.Exists(p)) 
                    {
                        File.Copy(p, target,true);
                        Log.SuccessAll($"已复制文件 {target}");
                    }
                    else if (Directory.Exists(p))
                    {
                        if(Directory.Exists(target))
                            Directory.Delete(target,true);
                        Util.CopyDirectory(p, target,true);
                        Log.SuccessAll($"已复制目录 {target}");
                    }
                    else
                    {
                        Log.Warn($"要拷贝的文件{p}不存在");
                    }
                }
            }
        }
        static void Init()
        {
            string localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");
            string currentPath = DiscoverGameDir(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
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

            string modsDirectory = Path.Combine(basePath, "mods");

            if (!Directory.Exists(modsDirectory))
            {
                Directory.CreateDirectory(modsDirectory);
            }
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
            else if (File.Exists(Path.Combine(modPath, "mod.json")))
            {
                Log.StepProgress("Mod扫描 : " + Path.Combine(modPath, "mod.json"));
                modContext.Add(new ModJsonItem(priority, Path.Combine(modPath, "mod.json")));
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
                        modContext.Add(new BundleItem(priority, file, list.Item1, list.Item2));
                    }
                    if (Path.GetExtension(file).ToLower() == ".zip")
                    {
                        string tp = Zip.ExtractAndGetPath(file);
                        if (tp != "")
                            ApplyMod(tp, priority);
                    }
                    if (WrappableFileItem.IsValid(file, addressableMgr))
                        modContext.Add(new WrappableFileItem(priority, file));
                    if (CommonPatchItem.IsValid(file))
                        modContext.Add(new CommonPatchItem(priority, file));
                    if (FuiPatchItem.IsValid(file))
                        modContext.Add(new FuiPatchItem(priority, file));
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
                if (toPatch.Any() || modContext.IsRequiredPatch(bundleName)) {
                    var (result,conflicts) = AB.MergeBundles(scan.GetBundleLocalPath(bundleName), toPatch, Path.Combine(basePath, "_generated", bundleName), (m,b, a, p,r) => modContext.PostPatch(bundleName,m,b,a,p,r));

                    if (result)
                    {
                        modContext.Redirect(bundleName, Path.Combine(basePath, "_generated", bundleName), "", "", true);
                        foreach (var (name, i, c) in conflicts)
                        {
                            Report.Warning(i, $"在修补 {name} 时和 {c} 冲突");
                        }
                    }
                    else
                    {
                        foreach(var tp in toPatch)
                        {
                            foreach(var (name,_,_) in conflicts)
                                Report.Warning(tp, $" {bundleName} 的修补中存在 {name} 和当前的包不能兼容，无法完成修补");
                        }
                        if(toPatch.Count() == 1)
                        {
                            Report.Warning(toPatch[0], $" {bundleName} 被直接替换为当前文件，因为他是唯一符合要求的文件");
                            modContext.Redirect(bundleName, toPatch[0], "", "", true);
                        }
                    }
                }
            }
        }
        static void ApplyAll()
        {
            modContext.InitMod();
            modContext.Sort();
            MergeAndPatchBundles();
            modContext.ApplyAll();
        }
    }
}