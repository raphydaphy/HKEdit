using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Util;
using Debug = UnityEngine.Debug;

public class HKEdit {
    private const string SCENE_LEVEL_NAME = "editorscene";
    private const string ASSET_LEVEL_NAME = "editorasset";
    private const string scenesDir = "Assets/Scenes";
    private const string dataDir = "Data";

    private static AssetsManager am;
    private static string hkDir;
    private static Dictionary<AssetID, AssetID> pointers;
    private static List<AssetsReplacer> sceneReplacers;
    private static List<AssetsReplacer> assetReplacers;
    private static List<AssetsReplacer> monoReplacers;
    private static string progressBarTitle;
    private static string unityVersion;
    private static int curSceneId;
    private static int curAssetId;
    
    [MenuItem("HKEdit/Load Scene")]
    private static void Test() {
        am = new AssetsManager();
        am.LoadClassPackage("cldb.dat");
        am.useTemplateFieldCache = true;
        am.updateAfterLoad = false;

        hkDir = Util.SteamHelper.GetHollowKnightDataPath();
        
        var globalgamemanagers = am.LoadAssetsFile(Path.Combine(hkDir, "globalgamemanagers"), false);
        var buildSettings = globalgamemanagers.table.getAssetInfo(11);

        var baseField = am.GetATI(globalgamemanagers.file, buildSettings).GetBaseField();
        
        var scenesArray = baseField.Get("scenes").Get("Array");

        // TODO: Allow level to be selected
        const int level = 0;

        var levelName = scenesArray[level].GetValue().AsString().Substring(14);
        levelName = levelName.Substring(0, levelName.Length - 6);

        progressBarTitle = "Loading Scene #" + level + ": " + levelName;

        if (ShouldCancel("Checking workspace", 0.2f)) return;

        if (!baseField.Get("m_Version").GetValue().AsString().Equals(Application.unityVersion)) {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Incorrect Unity version!", " You are using " +
                                        Application.unityVersion + " but the assets are compiled for version " + 
                                        baseField.Get("m_Version").GetValue().AsString(), "Ok");
            return;
        }

        unityVersion = Application.unityVersion;

        if (!Directory.Exists(scenesDir)) {
            Directory.CreateDirectory(scenesDir);
        }

        if (!Directory.Exists(dataDir)) {
            Directory.CreateDirectory(dataDir);
        }

        var metaFilePath = Path.Combine(scenesDir, "level" + level + ".unity.meta");
        var sceneFilePath = Path.Combine(scenesDir, "level" + level + ".unity");
        var assetsFilePath = Path.Combine(dataDir, "level" + level + ".assets");

        if (File.Exists(sceneFilePath)) {
            if (EditorUtility.DisplayDialog("Overwrite scene?", "" +
                                                                " You have already exported this scene. Would you like to overwrite it or open the existing scene? ",
                "Open Existing", "Overwrite")) {
                if (ShouldCancel("Opening Scene", 0.5f)) return;
                EditorSceneManager.OpenScene(sceneFilePath);
                EditorUtility.ClearProgressBar();
                return;
            } else {
                File.Delete(sceneFilePath);
            }
        }

        if (File.Exists(metaFilePath)) File.Delete(metaFilePath);
        if (File.Exists(assetsFilePath)) File.Delete(assetsFilePath);

        curSceneId = 1;
        curAssetId = 1;
        
        pointers = new Dictionary<AssetID, AssetID>();
        sceneReplacers = new List<AssetsReplacer>();
        assetReplacers = new List<AssetsReplacer>();
        monoReplacers = new List<AssetsReplacer>();

        if (ShouldCancel("Reading level file", 0.5f)) return;
        var scenePath = Path.Combine(Util.SteamHelper.GetHollowKnightDataPath(), "level" + level);
        var scene = am.LoadAssetsFile(scenePath, true);

        if (ShouldCancel("Updating Dependencies", 0.8f)) return;
        am.UpdateDependencies();

        for (var i = 0; i < am.files.Count; i++) {
            if (i % 100 == 0 && ShouldCancel("Generating QLTs", (float) i / am.files.Count)) return;
            am.files[i].table.GenerateQuickLookupTree();
        }

        var table = scene.table;
        var gameObjects = table.GetAssetsOfType(0x01);
        
        for (var i = 0; i < gameObjects.Count; i++) {
            if (i % 100 == 0 && ShouldCancel("Recursing GameObject dependencies", (float) i / gameObjects.Count)) return;
            
            var gameObjectInfo = gameObjects[i];
            var gameObjectBaseField = am.GetATI(scene.file, gameObjectInfo).GetBaseField();
            
            AddPointer(new AssetID(scene.path, (long) gameObjectInfo.index), false);
            FindNestedPointers(scene, gameObjectBaseField, gameObjectInfo, false);
        }
        var types = new List<Type_0D>();
        var typeNames = new List<string>();
        
        var fileToInst = am.files.ToDictionary(d => d.path);
        var j = 0;

        foreach (var pair in pointers) {
            if (j % 100 == 0 && ShouldCancel("Rewiring asset pointers", (float) j / pointers.Count)) return;

            var file = fileToInst[pair.Key.fileName];
            var info = file.table.getAssetInfo((ulong) pair.Key.pathId);

            var assetClass = AssetHelper.FindAssetClassByID(am.classFile, info.curFileType);
            var assetName = assetClass.name.GetString(am.classFile);

            if (!typeNames.Contains(assetName)) {
                var type0d = C2T5.Cldb2TypeTree(am.classFile, assetName);
                type0d.classId = (int)info.curFileType;
                types.Add(type0d);
                typeNames.Add(assetName);
            }

            ReplacePointers(file, info, pair.Value.pathId);
            j++;
        }

        if (ShouldCancel("Saving scene", 1)) return;

        List<Type_0D> assetTypes = new List<Type_0D>()
        {
            C2T5.Cldb2TypeTree(am.classFile, 0x1c), // audioclip
            C2T5.Cldb2TypeTree(am.classFile, 0x30), // shader
            C2T5.Cldb2TypeTree(am.classFile, 0x53) // texture2d
        };

        var sceneGuid = Util.UnityHelper.CreateMD5(levelName);
        
        UnityHelper.CreateMetaFile(sceneGuid, metaFilePath);

        var sceneFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(unityVersion, types))));
        var assetFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(unityVersion, assetTypes))));
        byte[] sceneFileData;
        using (MemoryStream ms = new MemoryStream())
        using (AssetsFileWriter w = new AssetsFileWriter(ms))
        {
            w.bigEndian = false;
            sceneFile.dependencies.pDependencies = new AssetsFileDependency[]
            {
                UnityHelper.CreateDependency(assetsFilePath),
            };
            sceneFile.dependencies.dependencyCount = 1;
                
            sceneFile.preloadTable.items = new AssetPPtr[]
            {
                //new AssetPPtr(2, 11500000)
            };
            sceneFile.preloadTable.len = 0;
            sceneFile.Write(w, 0, sceneReplacers.ToArray(), 0);
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

        if (ShouldCancel("Refreshing Assets", 0.95f)) return;
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

    private static void ReplacePointers(AssetsFileInstance file, AssetFileInfoEx info, long pathId) {
        var baseField = am.GetATI(file.file, info).GetBaseField();
        FindNestedPointers(file, baseField, info, true);
        FinalizeAsset(file, baseField, info);
        
        byte[] baseFieldData;
        using (MemoryStream ms = new MemoryStream())
        using (AssetsFileWriter w = new AssetsFileWriter(ms))
        {
            w.bigEndian = false;
            
            // Copy all data from base field into byte array
            baseField.Write(w);
            baseFieldData = ms.ToArray();
        }
        AssetsReplacer replacer = new AssetsReplacerFromMemory(0, (ulong)pathId, (int)info.curFileType, 0xFFFF, baseFieldData);
        
        // TODO: mono replacer?
        
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
        }
    }

    private static void FindNestedPointers(AssetsFileInstance file, AssetTypeValueField field, AssetFileInfoEx info, bool replace) {
        foreach (var child in field.pChildren) {
            if (child == null) {
                Debug.LogError("Found null child in under a " + field.GetFieldType() + " named " + field.GetName());
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
                    

                    bool exists = pointers.ContainsKey(id);

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
                        if (exists || asset.info.curFileType == 0x01) continue;

                        AddPointer(id, IsAsset(asset.info));
                        var baseField = asset.instance.GetBaseField();

                        if (asset.info.curFileType == 0x72 && child.GetName().Equals("component")) {
                            baseField = am.GetMonoBaseFieldCached(file, asset.info, Path.Combine(hkDir, "Managed"));
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
        return inf.curFileType == 0x1c || inf.curFileType == 0x30 || inf.curFileType == 0x53;
    }

    private static bool ShouldCancel(string status, float percent) {
        if (!EditorUtility.DisplayCancelableProgressBar(progressBarTitle, status, percent)) return false;
        EditorUtility.ClearProgressBar();
        return true;
    }
}
