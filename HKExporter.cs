using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Util;

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
        private static Dictionary<AssetID, AssetID> pointers;
        private static Dictionary<ScriptID, ushort> sidToMid;
        private static List<AssetsReplacer> sceneReplacers;
        private static List<AssetsReplacer> assetReplacers;
        private static List<AssetsReplacer> monoReplacers;
        private static string unityVersion;
        private static int curSceneId;
        private static int curAssetId;
        private static ushort curMonoId;
        private static List<Type_0D> types;
        private static List<string> typeNames;
        private static List<string> monoClassNames;
        
        private static bool useCached;

        public static void Main(string[] args) {
            Debug.Log("Preparing workspace..");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            if (args.Contains("-useCached")) {
                Debug.Log("useCached is enabled!");
                useCached = true;
                scenesDir = "Test/Assets/Scenes";
                dataDir = "Test/Data";
            }

            am = new AssetsManager();
            am.LoadClassPackage("../../Lib/cldb.dat");
            am.useTemplateFieldCache = true;
            am.updateAfterLoad = false;

            hkDir = Util.SteamHelper.GetHollowKnightDataPath();
            managedDir = /*useCached ? "D:/Documents/HKModding/HKExporter-Test/Assets/Managed" :*/ Path.Combine(hkDir, "Managed");
            
            Debug.Log("Using managed dir: " + managedDir + "\n");

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
                if (!useCached) {
                    Console.Write("You have already exported this scene. Do you want to overwrite it (Y/n) ? ");
                    var input = Console.ReadLine();
                    if (input != null && input.ToLower().Equals("y")) {
                        File.Delete(Path.Combine(DEFAULT_SCENES_DIR, sceneFilePath));
                    } else {
                        return;
                    }
                }
            } else if (useCached) {
                Debug.LogError("You must export the scene in normal mode before re-opening it from the cache");
                return;
            }
            
            sceneFilePath = Path.Combine(scenesDir, sceneFilePath);
            var metaFilePath = Path.Combine(scenesDir, "level" + level + ".unity.meta");
            var assetsFilePath = Path.Combine(dataDir, "level" + level + ".assets");

            if (!useCached) {
                if (File.Exists(metaFilePath)) File.Delete(metaFilePath);
                if (File.Exists(assetsFilePath)) File.Delete(assetsFilePath);
            }
            
            curSceneId = 1;
            curAssetId = 1;
            curMonoId = 0;
            
            pointers = new Dictionary<AssetID, AssetID>();
            sidToMid = new Dictionary<ScriptID, ushort>();
            
            sceneReplacers = new List<AssetsReplacer>();
            assetReplacers = new List<AssetsReplacer>();
            monoReplacers = new List<AssetsReplacer>();
            
            types = new List<Type_0D>();
            typeNames = new List<string>();
            monoClassNames = new List<string>();

            var scenePath = Path.Combine(useCached ? DEFAULT_SCENES_DIR : SteamHelper.GetHollowKnightDataPath(), "level" + level + (useCached ? ".unity" : ""));
            var scene = am.LoadAssetsFile(scenePath, true);

            Debug.Log("Generating QLTs...");

            am.UpdateDependencies();

            foreach (var t in am.files) {
                t.table.GenerateQuickLookupTree();
            }

            var table = scene.table;
            var gameObjects = table.GetAssetsOfType(0x01);
            var gameObjectBaseFields = new Dictionary<AssetFileInfoEx, AssetTypeValueField>();
            
            Debug.Log("Finding GameObjects...");

            var c = 0;
            for (c = 0; c < gameObjects.Count; c++) {
                var gameObjectInfo = gameObjects[c];
                var gameObjectBaseField = am.GetATI(scene.file, gameObjectInfo).GetBaseField();
            
                AddPointer(new AssetID(scene.path, (long) gameObjectInfo.index), false);
                gameObjectBaseFields.Add(gameObjectInfo, gameObjectBaseField);
            }
            c = 0;
            
            Debug.Log("Finding nested assets...");

            foreach (var pair in gameObjectBaseFields) {
                FindNestedPointers(scene, pair.Value, pair.Key, false);
                c++;
            }

            var fileToInst = am.files.ToDictionary(d => d.path);
            var j = 0;
            
            Debug.Log("Rewiring asset pointers...");
            
            foreach (var pair in pointers) {
                var file = fileToInst[pair.Key.fileName];
                var info = file.table.getAssetInfo((ulong) pair.Key.pathId);

                ReplacePointers(file, info, pair.Value.pathId);
                j++;
            }

            var assetTypes = new List<Type_0D>()
            {
                C2T5.Cldb2TypeTree(am.classFile, 0x1c), // audioclip
                C2T5.Cldb2TypeTree(am.classFile, 0x30), // shader
                C2T5.Cldb2TypeTree(am.classFile, 0x53), // texture2d
                C2T5.Cldb2TypeTree(am.classFile, 0x15), // material
                C2T5.Cldb2TypeTree(am.classFile, 0x4A), // animationclip
                C2T5.Cldb2TypeTree(am.classFile, 0x2B) // mesh
            };
            
            
            Debug.Log("Saving scene...");

            var sceneGuid = Util.UnityHelper.CreateMD5(levelName);
        
            UnityHelper.CreateMetaFile(sceneGuid, metaFilePath);

            var sceneFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(unityVersion, types))));
            var assetFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(unityVersion, assetTypes))));
            byte[] sceneFileData;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                sceneFile.dependencies.pDependencies = new AssetsFileDependency[]
                {
                    UnityHelper.CreateDependency(assetsFilePath, ""),
                };
                sceneFile.dependencies.dependencyCount = 1;
                
                sceneFile.preloadTable.items = new AssetPPtr[] {};
                sceneFile.preloadTable.len = 0;
            
                sceneFile.Write(w, 0, sceneReplacers.Concat(monoReplacers).ToArray(), 0);
                sceneFileData = ms.ToArray();
            }
            byte[] assetFileData;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                assetFile.Write(w, 0, assetReplacers.ToArray(), 0);
                assetFileData = ms.ToArray();
            }

            File.WriteAllBytes(sceneFilePath, sceneFileData);
            File.WriteAllBytes(assetsFilePath, assetFileData);
            
            stopwatch.Stop();
            Debug.Log("Exported scene #" + level + " ( " + levelName + " ) in " + stopwatch.ElapsedMilliseconds + " ms");
        }

        private static void ReplacePointers(AssetsFileInstance file, AssetFileInfoEx info, long pathId) {
            var baseField = am.GetATI(file.file, info).GetBaseField();
            
            var assetClass = AssetHelper.FindAssetClassByID(am.classFile, info.curFileType);
            var assetName = assetClass.name.GetString(am.classFile);

            ushort monoId = 0xFFFF;
            
            if (info.curFileType != 0x72) {
                if (!typeNames.Contains(assetName)) {
                    var type0d = C2T5.Cldb2TypeTree(am.classFile, assetName);
                    type0d.classId = (int)info.curFileType;
                    types.Add(type0d);
                    typeNames.Add(assetName);
                }
            } else {
                //baseField = am.GetMonoBaseFieldCached(file, info, managedDir);
                var m_Script = baseField.Get("m_Script");
                var scriptBaseField = am.GetExtAsset(file, m_Script).instance.GetBaseField();
                var m_ClassName = scriptBaseField.Get("m_ClassName").GetValue().AsString();
                var m_Namespace = scriptBaseField.Get("m_Namespace").GetValue().AsString();
                var m_AssemblyName = scriptBaseField.Get("m_AssemblyName").GetValue().AsString();
                var sid = new ScriptID(m_ClassName, m_Namespace, m_AssemblyName);

                if (m_ClassName.Equals("PlayMakerFSM")) {
                    if (!typeNames.Contains(assetName)) {
                        var type0d = C2T5.Cldb2TypeTree(am.classFile, assetName);
                        type0d.classId = (int)info.curFileType;
                        types.Add(type0d);
                        typeNames.Add(assetName);
                    }
                } else {
                    if (!sidToMid.ContainsKey(sid)) {
                        var mc = new MonoClass();
                        mc.Read(m_ClassName, Path.Combine(managedDir, m_AssemblyName), file.file.header.format);

                        var type0d = C2T5.Cldb2TypeTree(am.classFile, assetName);
                        var typeConverter = new TemplateFieldToType0D();

                        var monoFields = typeConverter.TemplateToTypeField(mc.children, type0d);

                        type0d.pStringTable = typeConverter.stringTable;
                        type0d.stringTableLen = (uint) type0d.pStringTable.Length;
                        type0d.scriptIndex = curMonoId;
                        type0d.pTypeFieldsEx = type0d.pTypeFieldsEx.Concat(monoFields).ToArray();
                        type0d.typeFieldsExCount = (uint) type0d.pTypeFieldsEx.Length;

                        types.Add(type0d);
                        sidToMid.Add(sid, curMonoId);
                        curMonoId++;
                    }
                    monoId = sidToMid[sid];
                }
            }
            
            FindNestedPointers(file, baseField, info, true);
            FinalizeAsset(file, baseField, info);

            byte[] baseFieldData;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
            
                // Copy all data from base field into byte array
                baseField.Write(w);
                baseFieldData = ms.ToArray();
            }
            AssetsReplacer replacer = new AssetsReplacerFromMemory(0, (ulong)pathId, (int)info.curFileType, monoId, baseFieldData);

            if (IsAsset(info)) assetReplacers.Add(replacer);
            //else if (info.curFileType == 0x73) monoReplacers.Add(replacer);
            else sceneReplacers.Add(replacer);
        }

        private static void FinalizeAsset(AssetsFileInstance file, AssetTypeValueField field, AssetFileInfoEx info) {
            if (info.curFileType == 0x01) // GameObject
            {
                var ComponentArray = field.Get("m_Component").Get("Array");

                //remove all null pointers
                List<AssetTypeValueField> newFields = ComponentArray.pChildren.Where(f =>
                    f.pChildren[0].pChildren[1].GetValue().AsInt64() != 0
                ).ToList();

                var newSize = (uint)newFields.Count;
                ComponentArray.SetChildrenList(newFields.ToArray(), newSize);
                ComponentArray.GetValue().Set(new AssetTypeArray() { size = newSize });
            } else if (info.curFileType == 0x73) { // MonoScript
                var m_AssemblyName = field.Get("m_AssemblyName").GetValue();
                if (m_AssemblyName.AsString().Equals("Assembly-CSharp.dll")) {
                    m_AssemblyName.Set("HKCode.dll");
                } else if (m_AssemblyName.AsString().Equals("Assembly-CSharp-firstpass.dll")) {
                    m_AssemblyName.Set("HKCode-firstpass.dll");
                }
            } else if (info.curFileType == 0x1c) { // Texture2D
                var path = field.Get("m_StreamData").Get("path");
                var pathString = path.GetValue().AsString();
                var directory = Path.GetDirectoryName(file.path);
                if (directory == null) {
                    Debug.LogWarning("Texture2D has null stream data path!");
                    return;
                }
                var fixedPath = Path.Combine(directory, pathString);
                path.GetValue().Set(fixedPath);
            } else if (info.curFileType == 0x53) { // AudioClip
                var path = field.Get("m_Resource").Get("m_Source");
                var pathString = path.GetValue().AsString();
                var directory = Path.GetDirectoryName(file.path);
                if (directory == null) {
                    Debug.LogWarning("AudioClip has null resource source path!");
                    return;
                }
                var fixedPath = Path.Combine(directory, pathString);
                path.GetValue().Set(fixedPath);
            } else if (info.curFileTypeOrIndex == 0x72) {
                
            }
        }

        private static void FindNestedPointers(AssetsFileInstance file, AssetTypeValueField field, AssetFileInfoEx info, bool replace) {
            foreach (var child in field.pChildren) {
                if (child == null) {
                    Debug.LogWarning("Found null child in under a " + field.GetFieldType() + " named " + field.GetName());
                    return;
                }

                if (!child.templateField.hasValue) {
                    //not array of values either
                    if (child.templateField.isArray && child.templateField.children[1].valueType != EnumValueTypes.ValueType_None) return;
                    var type = child.templateField.type;
                    if (type.StartsWith("PPtr<") && type.EndsWith(">") && child.childrenCount == 2) {
                        var fileId = child.Get("m_FileID").GetValue().AsInt();
                        var pathId = child.Get("m_PathID").GetValue().AsInt64();

                        if (pathId == 0) {
                            //Debug.LogWarning("A " + field.GetFieldType() + " called " + field.GetName() + " has a " + type + " child with a null path: " + pathId);
                            continue;
                        }

                        var id = AssetID.FromPPtr(file, fileId, pathId);
                        var asset = am.GetExtAsset(file, (uint) fileId, (ulong) pathId);
                        var exists = pointers.ContainsKey(id);

                        if (replace) {
                            if (exists) {
                                var newId = pointers[id];

                                var isSelfAsset = IsAsset(info);
                                var isDepAsset = newId.fileName.Equals(ASSET_LEVEL_NAME);

                                var newFileId = isDepAsset ^ isSelfAsset ? 1 : 0;

                                child.Get("m_FileID").GetValue().Set(newFileId);
                                child.Get("m_PathID").GetValue().Set(newId.pathId);
                            } else {
                                child.Get("m_FileID").GetValue().Set(0);
                                child.Get("m_PathID").GetValue().Set(0);
                            }
                        } else {
                            if (exists) continue;

                            AddPointer(id, IsAsset(asset.info));
                            var baseField = asset.instance.GetBaseField();

                            if (asset.info.curFileType == 0x72) {
                                var m_Script = baseField.Get("m_Script");
                                if (m_Script != null && m_Script.childrenCount == 2 && m_Script.GetFieldType().Equals("PPtr<MonoScript>")) {
                                    //baseField = am.GetMonoBaseFieldCached(asset.file, asset.info, managedDir);
                                }
                            }

                            FindNestedPointers(asset.file, baseField, info, false);
                        }
                    } else FindNestedPointers(file, child, info, replace);
                }
            }
        }

        private static void AddPointer(AssetID id, bool isAsset) {
            var name = isAsset ? ASSET_LEVEL_NAME : SCENE_LEVEL_NAME;
            var newId = new AssetID(name, isAsset ? curAssetId : curSceneId);
            pointers.Add(id, newId);
            if (isAsset) curAssetId++;
            else curSceneId++;
        }

        // TODO: why does this only target three file types
        private static bool IsAsset(AssetFileInfoEx inf)
        {
            return inf.curFileType == 0x1c || inf.curFileType == 0x30 || inf.curFileType == 0x53 || inf.curFileType == 0x15 || inf.curFileType == 0x4A || inf.curFileType == 0x2B;
        }
    }
}