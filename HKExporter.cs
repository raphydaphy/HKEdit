using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using HKExporter.Util;

namespace HKExporter {
    internal static class HkExporter {
        private const string UnityManagedDir = "Assets/Managed";
        private const string DataDir = "Data";
        
        private static AssetsManager _am;
        private static string _unityProjectDir = "D:/Documents/HKModding/HollowKnight";
        private static string _gameDir;
        private static string _managedDir;
        private static string _unityVersion;
        
        private static bool _noScriptData;
        private static bool _setupUnityProject;
        private static bool _exportAllScenes;

        public static void Main(string[] args) {

            _gameDir = SteamHelper.GetHollowKnightDataPath();

            var argsHelper = new ArgsHelper(args);
            
            _noScriptData = argsHelper.IsPresent("noScriptData");
            _setupUnityProject = argsHelper.IsPresent("setupUnityProject");
            _exportAllScenes = argsHelper.IsPresent("exportAllScenes");
            _unityProjectDir = argsHelper.GetValue("unityProjectDir", _unityProjectDir);
            _gameDir = argsHelper.GetValue("gameDir", _gameDir);
            
            Debug.Log("Script data is " + ArgsHelper.GetBoolString(!_noScriptData));
            Debug.Log("Unity project setup is " + ArgsHelper.GetBoolString(_setupUnityProject));
            Debug.Log("Using unity project dir: " + _unityProjectDir);
            Debug.Log("Using game dir: " + _gameDir);

            Debug.Log("Preparing workspace..");
            
            _managedDir = Path.Combine(_gameDir, "Managed");
            
            _am = new AssetsManager();
            _am.LoadClassPackage("../../Lib/cldb.dat");
            _am.useTemplateFieldCache = true;
            _am.updateAfterLoad = false;

            var globalGameManagers = _am.LoadAssetsFile(Path.Combine(_gameDir, "globalgamemanagers"), true);
            var buildSettings = globalGameManagers.table.getAssetInfo(11);

            var baseField = _am.GetATI(globalGameManagers.file, buildSettings).GetBaseField();

            var scenesArray = baseField.Get("scenes").Get("Array");

            _unityVersion = baseField.Get("m_Version").GetValue().AsString();
            
            if (_setupUnityProject) {
                var projectBuilder = new UnityProjectBuilder(_unityProjectDir, UnityManagedDir, DataDir, _unityVersion);
                if (projectBuilder.Setup(_am, globalGameManagers, _managedDir)) {
                    Debug.Log("Unity project generated at '" + _unityProjectDir + "', please open it in Unity to generate metadata files before continuing...");
                    Console.Write("Press Enter once you have opened the Unity project...");
                    Console.ReadLine();
                    projectBuilder.ExportProjectSettings(_am, globalGameManagers, _managedDir);
                    projectBuilder.ExportScriptableObjects(_am, globalGameManagers, _managedDir);
                    Debug.Log("Finishing Unity project setup");
                } else {
                    Debug.Log("Unity project setup is enabled but the directory already exists... skipping");
                }
            }

            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);

            if (_exportAllScenes) {
                for (uint i = 0; i < scenesArray.childrenCount; i++) {
                    ExportLevel(scenesArray, i);
                }
                return;
            }
            
            Console.Write("Enter level number: ");
            if (!uint.TryParse(Console.ReadLine(), out var level)) {
                Debug.LogError("Invalid level number");
                return;
            }

            ExportLevel(scenesArray, level);
        }

        private static void ExportLevel(AssetTypeValueField scenesArray, uint level) {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var path = scenesArray[level].GetValue().AsString();
            
            var levelName = Path.GetFileName(path);
            var sceneDir = Path.GetDirectoryName(path) ?? "Assets/Scenes";

            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);

            var sceneFilePath = Path.Combine(sceneDir, levelName + ".unity");
            var metaFilePath = Path.Combine(sceneDir, levelName + ".unity.meta");
            var assetsFilePath = Path.Combine(DataDir, levelName + ".assets");
            
            if (File.Exists(sceneFilePath)) {
                Console.Write("You have already exported this scene (#" + level + "). Do you want to overwrite it (Y/n) ? ");
                var input = Console.ReadLine();
                if (input != null && input.ToLower().Equals("y")) {
                    File.Delete(sceneFilePath);
                } else {
                    return;
                }
            }

            if (File.Exists(metaFilePath)) File.Delete(metaFilePath);
            if (File.Exists(assetsFilePath)) File.Delete(assetsFilePath);

            var scenePath = Path.Combine(_gameDir, "level" + level );
            var scene = _am.LoadAssetsFile(scenePath, true);

            Debug.Log("Generating QLTs...");

            _am.UpdateDependencies();

            foreach (var t in _am.files) {
                t.table.GenerateQuickLookupTree();
            }

            var crawler = new ReferenceCrawler(_am, scene, _unityProjectDir, UnityManagedDir, _managedDir, new ScriptList(_noScriptData));
            crawler.Crawl();

            var serializer = new AssetsSerializer(crawler, levelName, sceneFilePath, metaFilePath, assetsFilePath, _unityVersion);
            serializer.Serialize();
            
            stopwatch.Stop();
            Debug.Log("Exported scene #" + level + " ( " + levelName + " ) in " + stopwatch.ElapsedMilliseconds + " ms");
        }

        public static string RemapAssemblyName(string assemblyName) {
            if (assemblyName.Equals("Assembly-CSharp.dll")) return "HKCode.dll";
            if (assemblyName.Equals("Assembly-CSharp-firstpass.dll")) return "HKCode-firstpass.dll";
            return assemblyName;
        }
    }
}