using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET.Extra;

namespace HKExporter {
    public class MonoScriptResolver {
        private AssetsManager _am;
        private AssetsFileInstance _assetFile;
        private Dictionary<string, ulong> _lookup;

        public readonly uint Id;
        public readonly long GuidMostSignificant;
        public readonly long GuidLeastSignificant;

        public MonoScriptResolver(uint id, string unityProjectPath, string dllDir, string dllName) {
            this._am = new AssetsManager();
            this._am.LoadClassPackage("../../Lib/cldb.dat");
            this._lookup = new Dictionary<string, ulong>();
            
            var guid = File.ReadAllLines(unityProjectPath + "/" + dllDir + "/" + dllName + ".meta")[1].Substring(6);
            var path = unityProjectPath + "/Library/metadata/" + guid.Substring(0, 2) + "/" + guid;
            this._assetFile = this._am.LoadAssetsFile(path, true);
            
            var guidCharArray = guid.ToCharArray();
            Array.Reverse( guidCharArray );
            guid = new string(guidCharArray);

            this.GuidLeastSignificant = Convert.ToInt64(guid.Substring(0, 16), 16);
            this.GuidMostSignificant = Convert.ToInt64(guid.Substring(16, 16), 16);

            // Debug: reconstruct the GUId from the two longs
            //var rconGui = $"{this.GuidMostSignificant.ToString("x8")}{this.GuidLeastSignificant.ToString("x8")}";
            
            this.Id = id;
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
            return (long) pathID;
        }
    }
}