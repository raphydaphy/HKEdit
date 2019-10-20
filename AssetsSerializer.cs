using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using HKExporter.Util;

namespace HKExporter {
    public class AssetsSerializer {
        private readonly AssetsManager _am;
        private readonly ReferenceCrawler _crawler;

        private readonly string _levelName;
        private readonly string _sceneFilePath;
        private readonly string _metaFilePath;
        private readonly string _assetsFilePath;
        private readonly string _unityVersion;
        
        public AssetsSerializer(ReferenceCrawler crawler, string levelName, string sceneFilePath, string metaFilePath, string assetsFilePath, string unityVersion) {
            this._am = crawler._am;
            this._crawler = crawler;

            this._levelName = levelName;
            this._sceneFilePath = sceneFilePath;
            this._metaFilePath = metaFilePath;
            this._assetsFilePath = assetsFilePath;
            this._unityVersion = unityVersion;
        }

        public void Serialize(bool metaFile = true) {
            Debug.Log("Saving scene...");
            
            var assetTypes = this.GenAssetTypeTrees();
            var sceneGuid = UnityHelper.CreateMD5(this._levelName);

            if (metaFile) {
                UnityHelper.CreateMetaFile(sceneGuid, this._metaFilePath);
            }

            var sceneFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(this._unityVersion, this._crawler.Types))));
            var assetFile = new AssetsFile(new AssetsFileReader(new MemoryStream(BundleCreator.CreateBlankAssets(this._unityVersion, assetTypes))));
            byte[] sceneFileData;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                var deps = new List<AssetsFileDependency>();
                deps.Add(UnityHelper.CreateDependency(this._assetsFilePath, ""));
                deps.AddRange(this._crawler.ExternalDeps);
                //deps.Add(UnityHelper.CreateDependency("library/unity default resources", ""));
                sceneFile.dependencies.pDependencies = deps.ToArray();
                sceneFile.dependencies.dependencyCount = (uint)deps.Count;

                sceneFile.preloadTable.items = this._crawler.MonoScripts.ToArray();
                sceneFile.preloadTable.len = (uint) this._crawler.MonoScripts.Count;

                sceneFile.Write(w, 0, this._crawler.SceneReplacers.ToArray(), 0);
                sceneFileData = ms.ToArray();
            }
            byte[] assetFileData;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
                assetFile.Write(w, 0, this._crawler.AssetReplacers.ToArray(), 0);
                assetFileData = ms.ToArray();
            }

            File.WriteAllBytes(this._sceneFilePath, sceneFileData);
            File.WriteAllBytes(this._assetsFilePath, assetFileData);
        }
        
        private List<Type_0D> GenAssetTypeTrees() {
            return new List<Type_0D>()
            {
                C2T5.Cldb2TypeTree(this._am.classFile, (int) UnityTypes.AudioClip),
                C2T5.Cldb2TypeTree(this._am.classFile, (int) UnityTypes.Shader),
                C2T5.Cldb2TypeTree(this._am.classFile, (int) UnityTypes.Texture2D),
                C2T5.Cldb2TypeTree(this._am.classFile, (int) UnityTypes.Material),
                C2T5.Cldb2TypeTree(this._am.classFile, (int) UnityTypes.AnimationClip)
            };
        }
    }
}