using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET.Extra;

namespace HKExporter {
    public class MonoScriptResolver {
        private AssetsManager _am;
        private AssetsFileInstance _assetFile;
        private Dictionary<string, ulong> _lookup;

        public readonly uint Id;
        public readonly long GuidLs;
        public readonly long GuidMs;

        public MonoScriptResolver(uint id, string unityProjectPath, string dllDir, string dllName) {
            this._am = new AssetsManager();
            this._am.LoadClassPackage("../../Lib/cldb.dat");
            this._lookup = new Dictionary<string, ulong>();
            
            var dllGuid = File.ReadAllLines(unityProjectPath + "/" + dllDir + "/" + dllName + ".meta")[1].Substring(6);
            var path = unityProjectPath + "/Library/metadata/" + dllGuid.Substring(0, 2) + "/" + dllGuid;
            this._assetFile = this._am.LoadAssetsFile(path, true);

            this.Id = id;
            this.GuidLs = Convert.ToInt64(dllGuid.Substring(0, 16), 16);
            this.GuidMs = Convert.ToInt64(dllGuid.Substring(16, 16), 16);
        }

        public void Init() {
            var table = this._assetFile.table;
            foreach (var info in table.pAssetFileInfo) {
                var type = info.curFileType;
                if (!type.Equals(UnityTypes.MonoScript)) continue;
                var assetBaseField = this._am.GetATI(this._assetFile.file, info, true).GetBaseField();
                var className = assetBaseField.Get("m_ClassName").GetValue().AsString();
                this._lookup.Add(className, info.index);
            }
        }

        public ulong GetPathID(string className) {
            return this._lookup.ContainsKey(className) ? this._lookup[className] : 0;
        }

        public long GetSignedPathID(string className) {
            var pathID = this.GetPathID(className);
            if (pathID > long.MaxValue) return (long) (pathID - long.MaxValue);
            else return (long) pathID;
        }
    }
}