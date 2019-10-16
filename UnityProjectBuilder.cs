using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using HKExporter.Util;

namespace HKExporter {
    public class UnityProjectBuilder {
        private readonly string _dir;
        private readonly string _managedDir;
        private readonly string _version;

        private readonly string _settingsDir;
        
        public UnityProjectBuilder(string dir, string managedDir, string version) {
            this._dir = dir;
            this._managedDir = managedDir;
            this._version = version;
            this._settingsDir = Path.Combine(this._dir, "ProjectSettings");
        }

        public bool Setup(AssetsManager am, AssetsFileInstance globalGameManagers, string dllDir) {
            if (Directory.Exists(_dir)) return false;
            Debug.Log("Setting up Unity project..");
            Directory.CreateDirectory(this._dir);
            var unityManagedDir = Path.Combine(this._dir, this._managedDir);
            Directory.CreateDirectory(unityManagedDir);
            // TODO: automatically get required dlls
            AssemblyFixer.RenameAssemblies("Assembly-CSharp", "HKCode", dllDir, unityManagedDir);
            
            Directory.CreateDirectory(this._settingsDir);

            uint[] settingsPaths = {2, 3, 4, 7, 8, 12, 14, 17, 22};

            foreach (var pathId in settingsPaths) {
                this.CreateSettings(am, globalGameManagers, pathId);
            }
            
            File.WriteAllText(Path.Combine(this._settingsDir, "ProjectVersion.txt"), "m_EditorVersion: " + this._version);
            File.Copy("../../Lib/EditorSettings.asset", Path.Combine(this._settingsDir, "EditorSettings.asset"));
            return true;
        }

        private void CreateSettings(AssetsManager am, AssetsFileInstance globalGameManagers, uint id) {
            var tagSettings = globalGameManagers.table.getAssetInfo(id);
            var baseField = am.GetATI(globalGameManagers.file, tagSettings, false).GetBaseField();
            
            var settingsClass = AssetHelper.FindAssetClassByID(am.classFile, tagSettings.curFileType);
            var settingName = settingsClass.name.GetString(am.classFile);
            
            var type0d = C2T5.Cldb2TypeTree(am.classFile, settingName);
            var types = new List<Type_0D> {type0d};
            
            byte[] baseFieldData;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                baseField.Write(w);
                baseFieldData = ms.ToArray();
            }
            
            var replacer = new AssetsReplacerFromMemory(0, 1, type0d.classId, 0xFFFF, baseFieldData);
            AssetsReplacer[] replacers = {replacer};
            
            var file = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(this._version, types))));
            
            byte[] data;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                file.Write(w, 0, replacers, 0);
                data = ms.ToArray();
            }
            
            File.WriteAllBytes(Path.Combine(this._settingsDir, settingName + ".asset"), data);
        }
    }
}