using System;
using System.IO;
using System.Linq;
using AssetsTools.NET.Extra;
using HKExporter.Util;

namespace HKExporter {
    internal class HKExporter {
        private const string SCENE_LEVEL_NAME = "editorscene";
        private const string ASSET_LEVEL_NAME = "editorasset";
        private const string DEFAULT_SCENES_DIR = "Assets/Scenes";
        private const string DEFAULT_DATA_DIR = "Data";

        private static string scenesDir = DEFAULT_SCENES_DIR;
        private static string dataDir = DEFAULT_DATA_DIR;
        private static AssetsManager am;
        private static string hkDir;
        private static string managedDir;
        private static string unityVersion;
        
        private static bool noScriptData;

        public static void Main(string[] args) {
            Debug.Log("Preparing workspace..");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (args.Contains("-noScriptData")) {
                Debug.Log("noScriptData is enabled!");
                noScriptData = true;
            }

            am = new AssetsManager();
            am.LoadClassPackage("../../Lib/cldb.dat");
            am.useTemplateFieldCache = true;
            am.updateAfterLoad = false;

            hkDir = SteamHelper.GetHollowKnightDataPath();
            managedDir = Path.Combine(hkDir, "Managed");

            var globalgamemanagers = am.LoadAssetsFile(Path.Combine(hkDir, "globalgamemanagers"), false);
            var buildSettings = globalgamemanagers.table.getAssetInfo(11);

            var baseField = am.GetATI(globalgamemanagers.file, buildSettings).GetBaseField();

            var scenesArray = baseField.Get("scenes").Get("Array");

            Console.Write("Enter level number: ");
            if (!uint.TryParse(Console.ReadLine(), out var level)) {
                Debug.LogError("Invalid level number");
                return;
            }

            var levelName = scenesArray[level].GetValue().AsString().Substring(14);
            levelName = levelName.Substring(0, levelName.Length - 6);

            unityVersion = baseField.Get("m_Version").GetValue().AsString();

            if (!Directory.Exists(scenesDir)) {
                Directory.CreateDirectory(scenesDir);
            }

            if (!Directory.Exists(dataDir)) {
                Directory.CreateDirectory(dataDir);
            }

            var sceneFilePath = "level" + level + ".unity";

            if (File.Exists(Path.Combine(DEFAULT_SCENES_DIR, sceneFilePath))) {
                Console.Write("You have already exported this scene. Do you want to overwrite it (Y/n) ? ");
                var input = Console.ReadLine();
                if (input != null && input.ToLower().Equals("y")) {
                    File.Delete(Path.Combine(DEFAULT_SCENES_DIR, sceneFilePath));
                } else {
                    return;
                }
            }
            
            sceneFilePath = Path.Combine(scenesDir, sceneFilePath);
            var metaFilePath = Path.Combine(scenesDir, "level" + level + ".unity.meta");
            var assetsFilePath = Path.Combine(dataDir, "level" + level + ".assets");

            if (File.Exists(metaFilePath)) File.Delete(metaFilePath);
            if (File.Exists(assetsFilePath)) File.Delete(assetsFilePath);

            var scenePath = Path.Combine(SteamHelper.GetHollowKnightDataPath(), "level" + level );
            var scene = am.LoadAssetsFile(scenePath, true);

            Debug.Log("Generating QLTs...");

            am.UpdateDependencies();

            foreach (var t in am.files) {
                t.table.GenerateQuickLookupTree();
            }
            
            var crawler = new ReferenceCrawler(am, scene, managedDir, noScriptData);
            crawler.Crawl();

            var serializer = new AssetsSerializer(crawler, levelName, sceneFilePath, metaFilePath, assetsFilePath, unityVersion);
            serializer.Serialize();
            
            stopwatch.Stop();
            Debug.Log("Exported scene #" + level + " ( " + levelName + " ) in " + stopwatch.ElapsedMilliseconds + " ms");
        }
    }
}