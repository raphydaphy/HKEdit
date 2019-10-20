using System;
using System.IO;

namespace HKExporter {
    public class AudioClipResolver {
        private const string dir = "Assets/AudioClip";
        private readonly string _unityProjectPath;
        
        public readonly uint FileId;
        public readonly long PathId;
        public readonly long GuidMostSignificant;
        public readonly long GuidLeastSignificant;

        // TODO: for all assets not just audioclip
        public AudioClipResolver(string unityProjectPath, uint fileId,  string name) {
            this.FileId = fileId;
            var metaFilePath = Path.Combine(unityProjectPath, dir + "/" + name + ".wav.meta");
            if (!File.Exists(metaFilePath)) {
                throw new FileNotFoundException("Could not find wav meta file at " + metaFilePath + "! Please export assets with UnityRipper or enable asset bundle mode.");
            }

            var metaFileLines = File.ReadAllLines(metaFilePath);
            this.PathId = long.Parse(metaFileLines[6].Substring(19));
            var guid = metaFileLines[1].Substring(6);
            
            Debug.Log("Created AudioClip resolver for " + name + " with meta file at " + metaFilePath + ". Guid=" + guid + ", PathID=" + this.PathId);
            
            // TODO: remove duplicated code with MonoScriptResolver
            var guidCharArray = guid.ToCharArray();
            Array.Reverse( guidCharArray );
            var guidReverse = new string(guidCharArray);

            this.GuidLeastSignificant = Convert.ToInt64(guidReverse.Substring(0, 16), 16);
            this.GuidMostSignificant = Convert.ToInt64(guidReverse.Substring(16, 16), 16);
        }
    }
}