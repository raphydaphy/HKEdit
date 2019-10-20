using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using HKExporter.Util;

namespace HKExporter {
    public class UnityProjectBuilder {
        private readonly string _dir;
        private readonly string _version;

        private readonly string _managedDir;
        private readonly string _dataDir;
        private readonly string _settingsDir;
        
        public UnityProjectBuilder(string dir, string managedDir, string dataDir, string version) {
            this._dir = dir;
            this._version = version;
            
            this._managedDir = Path.Combine(this._dir, managedDir);
            this._dataDir = Path.Combine(this._dir, dataDir);
            this._settingsDir = Path.Combine(this._dir, "ProjectSettings");
        }

        public bool Setup(AssetsManager am, AssetsFileInstance globalGameManagers, string dllDir) {
            if (Directory.Exists(_dir)) return false;
            Debug.Log("Setting up Unity project..");
            Directory.CreateDirectory(this._dir);
            Directory.CreateDirectory(this._managedDir);
            // TODO: automatically get required assemblies
            AssemblyFixer.RenameAssemblies("Assembly-CSharp", "HKCode", dllDir, this._managedDir);
            
            Directory.CreateDirectory(this._settingsDir);
            
            File.WriteAllText(Path.Combine(this._settingsDir, "ProjectVersion.txt"), "m_EditorVersion: " + this._version);
            File.Copy("../../Lib/EditorSettings.asset", Path.Combine(this._settingsDir, "EditorSettings.asset"));

            // TODO automatically get required assemblies
            var playMakerDll = Path.Combine(dllDir, "PlayMaker.dll");
            if (File.Exists(playMakerDll)) File.Copy(playMakerDll, Path.Combine(this._managedDir, "PlayMaker.dll"));

            if (!Directory.Exists(this._dataDir)) Directory.CreateDirectory(this._dataDir);
            
            return true;
        }

        public void ExportScriptableObjects(AssetsManager am, AssetsFileInstance globalGameManagers, string dllDir) {
            var resourceManager = this.GetGlobalBaseField(am, globalGameManagers, 13, out var ignored);
            var containers = resourceManager.Get("m_Container").Get("Array");
            var tmpResourcesDir = Path.Combine(this._dir, "Assets/TextMesh Pro/Resources");
            var playmakerResourcesDir = Path.Combine(this._dir, "Assets/PlayMaker/Resources");

            am.UpdateDependencies();

            foreach (var t in am.files) {
                t.table.GenerateQuickLookupTree();
            }

            var i = 0;
            foreach (var child in containers.pChildren) {
                var container = child.Get("first").GetValue().AsString();
                var pptr = child.Get("second");
                var pptrType = pptr.templateField.type;
                if (!(pptrType.StartsWith("PPtr<") && pptrType.EndsWith(">") && child.childrenCount == 2)) continue; 
                var fileId = (uint)pptr.Get("m_FileID").GetValue().AsInt();
                var pathId = (ulong)pptr.Get("m_PathID").GetValue().AsInt64();
                if (pathId == 0) continue;
                var asset = am.GetExtAsset(globalGameManagers, fileId, pathId);
                var baseField = asset.instance.GetBaseField();
                var nameField = baseField.Get("m_Name").GetValue();
                if (nameField == null) continue;
                var name = nameField.AsString();

                if (container.StartsWith("fonts")) {
                    Debug.Log(container + "/" + name);
                }
                
                if (asset.info.curFileType == UnityTypes.MonoBehaviour) {
                    var mScript = baseField.Get("m_Script");
                    if (mScript != null && mScript.childrenCount == 2 && mScript.GetFieldType().Equals("PPtr<MonoScript>")) {
                        baseField = am.GetMonoBaseFieldCached(asset.file, asset.info, dllDir);
                    }
                    
                    if (name.Equals("TMP Settings")) {
                        Debug.Log("TMP settings has " + baseField.childrenCount + " children");
                        this.CreateScriptableObject(am, asset.file, asset.info, baseField, dllDir, name, tmpResourcesDir);
                    } else if (name.Equals("TMP Default Style Sheet")) {
                        this.CreateScriptableObject(am, asset.file, asset.info, baseField, dllDir, name, Path.Combine(tmpResourcesDir, "Style Sheets"));
                    } else if (name.Equals("PlayMakerGlobals")) {
                        this.CreateScriptableObject(am, asset.file, asset.info, baseField, dllDir, name, playmakerResourcesDir);
                    } else if (container.StartsWith("fonts & materials")) {
                        this.CreateScriptableObject(am, asset.file, asset.info, baseField, dllDir, name, Path.Combine(this._dir, "Fonts & Materials/Arial SDF"));
                    }
                }

                i++;
            }
        }

        public void ExportProjectSettings(AssetsManager am, AssetsFileInstance globalGameManagers, string dllDir) {

            // 1, 2, 3, 4, 7, 8, 12, 17, 22
            uint[] settingsPaths = {1, 2, 3, 4, 7, 8, 12, 17, 22}; // 14 = networkmanager but it took ages to generate

            foreach (var pathId in settingsPaths) {
                this.CreateSettingsFile(am, globalGameManagers, pathId, dllDir);
            }
        }

        private AssetTypeValueField GetGlobalBaseField(AssetsManager am, AssetsFileInstance globalGameManagers, uint fieldId, out AssetFileInfoEx info) {
            info = globalGameManagers.table.getAssetInfo(fieldId);
            return info != null ? am.GetATI(globalGameManagers.file, info).GetBaseField() : null;
        }

        private void CreateSettingsFile(AssetsManager am, AssetsFileInstance globalGameManagers, uint id, string dllDir) {
            var baseField = this.GetGlobalBaseField(am, globalGameManagers, id, out var info);
            if (baseField == null) {
                Debug.Log("Skipping settings file #" + id + " as it was not found");
                return;
            }
            
            var assetClass = AssetHelper.FindAssetClassByID(am.classFile, info.curFileType);
            var assetName = assetClass.name.GetString(am.classFile);

            // For some reason Unity stores the player settings in ProjectSettings.asset
            if (assetName.Equals("PlayerSettings")) assetName = "ProjectSettings";
            
            am.UpdateDependencies();
            
            this.CreateScriptableObject(am, globalGameManagers, info, baseField, dllDir, assetName, this._settingsDir);
        }

        private void CreateScriptableObject(AssetsManager am, AssetsFileInstance file, AssetFileInfoEx info, AssetTypeValueField baseField, string dllDir, string name, string outputDir) {
            Debug.Log("Creating Scriptable Object " + name);
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            
            var crawler = new ReferenceCrawler(am, file, info, baseField, this._dir, this._managedDir, dllDir, new ScriptList(false));
            crawler.Crawl();

            var serializer = new AssetsSerializer(crawler, name, Path.Combine(outputDir, name + ".asset"), Path.Combine(outputDir, name + ".asset.meta"), Path.Combine(this._dataDir, name + ".assets"), this._version);
            serializer.Serialize(false);
        }
    }
}