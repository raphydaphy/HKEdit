using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET.Extra;
using HKExporter.Util;

namespace HKExporter {
    internal class HKExporter {
        private static string _unityProjectDir = "D:/Documents/HKModding/HollowKnight";
        private const string _unityManagedDir = "Assets/Managed";
        private static string _scenesDir = "Assets/Scenes";
        private static string _dataDir = "Data";
        private static AssetsManager _am;
        private static string _gameDir;
        private static string _managedDir;
        private static string _unityVersion;
        
        private static bool _noScriptData;
        private static bool _setupUnityProject;

        public static void Main(string[] args) {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _gameDir = SteamHelper.GetHollowKnightDataPath();

            ArgsHelper argsHelper = new ArgsHelper(args);
            
            _noScriptData = argsHelper.IsPresent("noScriptData");
            _setupUnityProject = argsHelper.IsPresent("setupUnityProject");
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

            var globalGameManagers = _am.LoadAssetsFile(Path.Combine(_gameDir, "globalgamemanagers"), false);
            var buildSettings = globalGameManagers.table.getAssetInfo(11);

            var baseField = _am.GetATI(globalGameManagers.file, buildSettings, false).GetBaseField();

            var scenesArray = baseField.Get("scenes").Get("Array");

            Console.Write("Enter level number: ");
            if (!uint.TryParse(Console.ReadLine(), out var level)) {
                Debug.LogError("Invalid level number");
                return;
            }

            var levelName = scenesArray[level].GetValue().AsString().Substring(14);
            levelName = levelName.Substring(0, levelName.Length - 6);

            _unityVersion = baseField.Get("m_Version").GetValue().AsString();

            if (!Directory.Exists(_scenesDir)) {
                Directory.CreateDirectory(_scenesDir);
            }

            if (!Directory.Exists(_dataDir)) {
                Directory.CreateDirectory(_dataDir);
            }
            
            var sceneFilePath = Path.Combine(_scenesDir, "level" + level + ".unity");
            var metaFilePath = Path.Combine(_scenesDir, "level" + level + ".unity.meta");
            var assetsFilePath = Path.Combine(_dataDir, "level" + level + ".assets");
            
            if (File.Exists(sceneFilePath)) {
                Console.Write("You have already exported this scene. Do you want to overwrite it (Y/n) ? ");
                var input = Console.ReadLine();
                if (input != null && input.ToLower().Equals("y")) {
                    File.Delete(sceneFilePath);
                } else {
                    return;
                }
            }

            if (File.Exists(metaFilePath)) File.Delete(metaFilePath);
            if (File.Exists(assetsFilePath)) File.Delete(assetsFilePath);

            if (_setupUnityProject) {
                var projectBuilder = new UnityProjectBuilder(_unityProjectDir, _unityManagedDir, _unityVersion);
                if (projectBuilder.Setup(_am, globalGameManagers, _managedDir)) {
                    Debug.Log("Unity project generated at '" + _unityProjectDir + "', please open it in Unity to generate metadata files before continuing...");
                    Console.Write("Press Enter once you have opened the Unity project...");
                    Console.ReadLine();
                } else {
                    Debug.Log("Unity project setup is enabled but the directory already exists... skipping");
                }
            }

            var scenePath = Path.Combine(_gameDir, "level" + level );
            var scene = _am.LoadAssetsFile(scenePath, true);

            Debug.Log("Generating QLTs...");

            _am.UpdateDependencies();

            foreach (var t in _am.files) {
                t.table.GenerateQuickLookupTree();
            }

            var blacklist = new List<string> {
                // Crash
                ScriptList.GetScriptName("tk2dSpriteCollectionData", "HKCode.dll"),
                ScriptList.GetScriptName("PlayMakerFSM", "PlayMaker.dll"),
                ScriptList.GetScriptName("HeroController", "HKCode.dll"),
                ScriptList.GetScriptName("HeroAudioController", "HKCode.dll")        
            };

            var whitelist = new List<string> {
                // Works
                //ScriptList.GetScriptName("tk2dSpriteAnimator", "HKCode.dll"),
                //ScriptList.GetScriptName("tk2dSpriteAnimation", "HKCode.dll"),
                //ScriptList.GetScriptName("tk2dSprite", "HKCode.dll"),
                //ScriptList.GetScriptName("PlayAudioAndRecycle", "HKCode.dll"),
                ScriptList.GetScriptName("SpatterOrange", "HKCode.dll")
            };

            var crawler = new ReferenceCrawler(_am, scene, _unityProjectDir, _unityManagedDir, _managedDir, new ScriptList(_noScriptData, whitelist, blacklist));
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