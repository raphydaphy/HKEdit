using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET.Extra;

namespace HKExporter {
    public class MonoScriptResolver {
        private readonly AssetsManager _am;
        private readonly AssetsFileInstance _assetFile;
        private readonly Dictionary<string, long> _lookup;
        private readonly string _unityProjectPath;

        public readonly uint Id;
        public readonly long GuidMostSignificant;
        public readonly long GuidLeastSignificant;

        public MonoScriptResolver(uint id, string unityProjectPath, string dllDir, string dllName) {
            this._am = new AssetsManager();
            this._am.LoadClassPackage("../../Lib/cldb.dat");
            this._lookup = new Dictionary<string, long>();
            this._unityProjectPath = unityProjectPath;

            var metaFilePath = Path.Combine(unityProjectPath, dllDir + "/" + dllName + ".meta");
            var guid = "";
            if (File.Exists(metaFilePath)) {
                guid = File.ReadAllLines(metaFilePath)[1].Substring(6);
            } else {
                guid = this.GetDllGuid(dllName);
            }

            if (string.IsNullOrEmpty(guid)) {
                Debug.Log("Failed to resolve Dll " + dllName);
                return;
            }

            var path = unityProjectPath + "/Library/metadata/" + guid.Substring(0, 2) + "/" + guid;
            this._assetFile = this._am.LoadAssetsFile(path, true);
            
            Debug.Log("Created MonoScript resolver for " + dllName + " with guid " + guid + " with id " + id);
            
            var guidCharArray = guid.ToCharArray();
            Array.Reverse( guidCharArray );
            var guidReverse = new string(guidCharArray);

            this.GuidLeastSignificant = Convert.ToInt64(guidReverse.Substring(0, 16), 16);
            this.GuidMostSignificant = Convert.ToInt64(guidReverse.Substring(16, 16), 16);

            // var rconGui = $"{this.GuidMostSignificant.ToString("x8")}{this.GuidLeastSignificant.ToString("x8")}";

            this.Id = id;
        }

        public void Init() {
            var table = this._assetFile.table;
            foreach (var info in table.pAssetFileInfo) {
                var type = info.curFileType;
                if (!type.Equals(UnityTypes.MonoScript)) continue;
                var assetBaseField = this._am.GetATI(this._assetFile.file, info, true).GetBaseField();
                var className = assetBaseField.Get("m_ClassName").GetValue().AsString();
                this._lookup.Add(className, (long)info.index);
            }
        }

        private string GetDllGuid(string dllName) {
            dllName = dllName.Substring(0, dllName.Length - 4);
            Debug.Log("Finding GUID for '" + dllName + "'");
            var assetDatabase = this._am.LoadAssetsFile(Path.Combine(this._unityProjectPath, "Library/assetDatabase3"), true);
            var baseField = this._am.GetATI(assetDatabase.file, assetDatabase.table.pAssetFileInfo[0], true).GetBaseField();
            var assetArray = baseField.Get("m_Assets").Get("Array");
            var i = 0;
            foreach (var assetPair in assetArray.pChildren) {
                var name = assetPair.Get("second").Get("mainRepresentation").Get("name").GetValue().AsString();
                if (name.Equals(dllName)) {
                    var guid = "";
                    var guidField = assetPair.Get("first");

                    foreach (var guidPart in guidField.pChildren) {
                        var hexString = Convert.ToString(guidPart.GetValue().AsUInt(), 16);
                        while (hexString.Length < 8) hexString += "0";
                        var charArray = hexString.ToArray();
                        Array.Reverse(charArray);
                        hexString = new string(charArray);
                        guid += hexString;
                    }

                    return guid;
                }
                i++;
            }
            return null;
        }

        public long GetPathID(string className) {
            return this._lookup.ContainsKey(className) ? this._lookup[className] : 0;
        }
    }
}