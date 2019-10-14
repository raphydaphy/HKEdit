using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using HKExporter.Util;

namespace HKExporter {
    public class ReferenceCrawler {
        private const string UnityProjectDir = "D:/Documents/HKModding/HollowKnight";
        private const string UnityManagedDir = "Assets/Managed";
        private const string SceneLevelName = "editorscene";
        private const string AssetLevelName = "editorasset";
        
        public readonly Dictionary<string, MonoScriptResolver> Assemblies = new Dictionary<string, MonoScriptResolver>();
        public readonly List<AssetsReplacer> SceneReplacers = new List<AssetsReplacer>();
        public readonly List<AssetsReplacer> AssetReplacers = new List<AssetsReplacer>();
        public readonly List<AssetsReplacer> MonoReplacers = new List<AssetsReplacer>();
        public readonly List<Type_0D> Types = new List<Type_0D>();
        public readonly List<AssetPPtr> MonoScripts = new List<AssetPPtr>();
        
        private readonly Dictionary<AssetID, AssetID> _pointers = new Dictionary<AssetID, AssetID>();
        private readonly Dictionary<ScriptID, ushort> _sidToMid = new Dictionary<ScriptID, ushort>();
        private readonly Dictionary<AssetFileInfoEx, AssetTypeValueField> _baseFields = new Dictionary<AssetFileInfoEx, AssetTypeValueField>();
        private readonly List<string> _typeNames = new List<string>();

        private int _curSceneId = 1;
        private int _curAssetId = 1;
        private static uint _curMonoResolver = 2;
        private ushort _curMonoId;
        
        public readonly AssetsManager _am;
        private readonly AssetsFileInstance _file;
        private readonly string _managedDir;
        private readonly bool _noScriptData;

        public ReferenceCrawler(AssetsManager am, AssetsFileInstance file, string managedDir, bool noScriptData) {
            this._am = am;
            this._file = file;
            this._managedDir = managedDir;
            this._noScriptData = noScriptData;
        }

        public void Crawl() {
            Debug.Log("Finding GameObjects...");
            this.FindGameObjects();
            
            Debug.Log("Finding nested assets...");
            foreach (var pair in this._baseFields) {
                this.FindNestedPointers(this._file, pair.Value, pair.Key, false);
            }
            
            Debug.Log("Caching MonoScript pointers");
            foreach (var dll in this.Assemblies) {
                dll.Value.Init();
            }
            
            var fileToInst = this._am.files.ToDictionary(d => d.path);
            
            Debug.Log("Rewiring asset pointers...");
            
            foreach (var pair in this._pointers) {
                var pointerFile = fileToInst[pair.Key.fileName];
                var info = pointerFile.table.getAssetInfo((ulong) pair.Key.pathId);

                this.ReplacePointers(pointerFile, info, pair.Value);
            }
        }

        private void FindGameObjects() {
            foreach (var info in this._file.table.pAssetFileInfo) {
                var type = info.curFileType;
                if (type != UnityTypes.GameObject) continue;
                
                var assetBaseField = this._am.GetATI(this._file.file, info, false).GetBaseField();
                var name = assetBaseField.Get("m_Name").GetValue().AsString();

                if (!name.Equals("_SceneManager")) continue;

                this.AddPointer(new AssetID(this._file.path, (long) info.index), false);
                this._baseFields.Add(info, assetBaseField);
            }
            
            Debug.Log("Found " + this._baseFields.Count + " GameObjects!");
        }
        
        private void FindNestedPointers(AssetsFileInstance file, AssetTypeValueField field, AssetFileInfoEx info, bool replace) {
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
                        // this is because we replace the pointer to the mono-script in ReplacePointers
                        if (replace && child.GetFieldType().Equals("PPtr<MonoScript>")) continue;
                        
                        var fileId = child.Get("m_FileID").GetValue().AsInt();
                        var pathId = child.Get("m_PathID").GetValue().AsInt64();

                        if (pathId == 0) {
                            //Debug.LogWarning("A " + field.GetFieldType() + " called " + field.GetName() + " has a " + type + " child with a null path: " + pathId);
                            continue;
                        }

                        var id = AssetID.FromPPtr(file, fileId, pathId);
                        var asset = this._am.GetExtAsset(file, (uint) fileId, (ulong) pathId);
                        var exists = this._pointers.ContainsKey(id);

                        if (replace) {
                            if (exists) {
                                var newId = this._pointers[id];

                                var isSelfAsset = UnityTypes.IsAsset(info);
                                var isDepAsset = newId.fileName.Equals(AssetLevelName);

                                var newFileId = isDepAsset ^ isSelfAsset ? 1 : 0;

                                child.Get("m_FileID").GetValue().Set(newFileId);
                                child.Get("m_PathID").GetValue().Set(newId.pathId);
                            } else {
                                child.Get("m_FileID").GetValue().Set(0);
                                child.Get("m_PathID").GetValue().Set(0);
                            }
                        } else {
                            if (exists) continue;

                            this.AddPointer(id, UnityTypes.IsAsset(asset.info));
                            var baseField = asset.instance.GetBaseField();

                            if (asset.info.curFileType == UnityTypes.MonoScript) {
                                var assemblyName = RemapAssemblyName(baseField.Get("m_AssemblyName").GetValue().AsString());
                                if (!this.Assemblies.ContainsKey(assemblyName)) {
                                    Debug.Log("Adding mono resolver for " + assemblyName);
                                    this.Assemblies.Add(assemblyName, new MonoScriptResolver(_curMonoResolver, UnityProjectDir, UnityManagedDir, assemblyName));
                                    _curMonoResolver++;
                                }
                            } else if (!this._noScriptData && asset.info.curFileType == UnityTypes.MonoBehaviour) {
                                var mScript = baseField.Get("m_Script");
                                if (mScript != null && mScript.childrenCount == 2 && mScript.GetFieldType().Equals("PPtr<MonoScript>")) {
                                    baseField = this._am.GetMonoBaseFieldCached(asset.file, asset.info, this._managedDir);
                                }
                            }

                            this.FindNestedPointers(asset.file, baseField, info, false);
                        }
                    } else this.FindNestedPointers(file, child, info, replace);
                }
            }
        }private void ReplacePointers(AssetsFileInstance file, AssetFileInfoEx info, AssetID aid) {
            var baseField = this._am.GetATI(file.file, info, false).GetBaseField();
            
            var assetClass = AssetHelper.FindAssetClassByID(this._am.classFile, info.curFileType);
            var assetName = assetClass.name.GetString(this._am.classFile);

            ushort monoId = 0xFFFF;

            if (info.curFileType == UnityTypes.MonoScript) {
                var className = baseField.Get("m_ClassName").GetValue().AsString();
                var assemblyName = RemapAssemblyName(baseField.Get("m_AssemblyName").GetValue().AsString());
                var dll = this.Assemblies[assemblyName];
                var scriptPath = dll.GetPathID(className);
                Debug.Log("New Preload: " + "1/" + scriptPath + " " + assemblyName + "/" + className);
                this.MonoScripts.Add(new AssetPPtr(dll.Id, (ulong) scriptPath));
            }
            
            if (this._noScriptData || info.curFileType != UnityTypes.MonoBehaviour) { 
                if (!this._typeNames.Contains(assetName)) {
                    var type0d = C2T5.Cldb2TypeTree(this._am.classFile, assetName);
                    type0d.classId = (int)info.curFileType;
                    this.Types.Add(type0d);
                    this._typeNames.Add(assetName);
                }
            } else {
                baseField = this._am.GetMonoBaseFieldCached(file, info, this._managedDir);
                
                var mScript = baseField.Get("m_Script");
                var scriptBaseField = this._am.GetExtAsset(file, mScript).instance.GetBaseField();
                var mClassName = scriptBaseField.Get("m_ClassName").GetValue().AsString();
                var mNamespace = scriptBaseField.Get("m_Namespace").GetValue().AsString();
                var mAssemblyName = scriptBaseField.Get("m_AssemblyName").GetValue().AsString();

                var assembly = this.Assemblies[RemapAssemblyName(mAssemblyName)];
                mScript.Get("m_FileID").GetValue().Set(assembly.Id);
                mScript.Get("m_PathID").GetValue().Set(assembly.GetPathID(mClassName));
                var sid = new ScriptID(mClassName, mNamespace, mAssemblyName);

                if (!this._sidToMid.ContainsKey(sid)) {
                    var mc = new MonoClass();
                    mc.Read(mClassName, Path.Combine(this._managedDir, mAssemblyName), file.file.header.format);
                    
                    var type0d = C2T5.Cldb2TypeTree(this._am.classFile, assetName);
                    var typeConverter = new TemplateFieldToType0D();
                    var monoFields = typeConverter.TemplateToTypeField(mc.children, type0d);
                    
                    type0d.pStringTable = typeConverter.stringTable;
                    type0d.stringTableLen = (uint) type0d.pStringTable.Length;
                    type0d.pTypeFieldsEx = type0d.pTypeFieldsEx.Concat(monoFields).ToArray();
                    type0d.typeFieldsExCount = (uint) type0d.pTypeFieldsEx.Length;
                    type0d.classId = (int)info.curFileType;
                    type0d.scriptIndex = this._curMonoId;
                    
                    this.Types.Add(type0d);
                    this._sidToMid.Add(sid, this._curMonoId);
                    this._curMonoId++;
                }

                monoId = this._sidToMid[sid];
            }
            
            this.FindNestedPointers(file, baseField, info, true);
            this.FinalizeAsset(file, baseField, info);

            byte[] baseFieldData;
            using (var ms = new MemoryStream())
            using (var w = new AssetsFileWriter(ms))
            {
                w.bigEndian = false;
            
                // Copy all data from base field into byte array
                baseField.Write(w);
                baseFieldData = ms.ToArray();
            }
            AssetsReplacer replacer = new AssetsReplacerFromMemory(0, (ulong)aid.pathId, (int)info.curFileType, monoId, baseFieldData);

            if (UnityTypes.IsAsset(info)) this.AssetReplacers.Add(replacer);
            else if (info.curFileType == UnityTypes.MonoScript) this.MonoReplacers.Add(replacer);
            else this.SceneReplacers.Add(replacer);
        }

        private void FinalizeAsset(AssetsFileInstance file, AssetTypeValueField field, AssetFileInfoEx info) {
            switch (info.curFileType) {
                case UnityTypes.GameObject: {
                    var componentArray = field.Get("m_Component").Get("Array");
                    //remove all null pointers
                    List<AssetTypeValueField> newFields = componentArray.pChildren.Where(f =>
                        f.pChildren[0].pChildren[1].GetValue().AsInt64() != 0
                    ).ToList();

                    var newSize = (uint)newFields.Count;
                    componentArray.SetChildrenList(newFields.ToArray(), newSize);
                    componentArray.GetValue().Set(new AssetTypeArray() { size = newSize });
                    break;
                }
                case UnityTypes.Texture2D: {
                    var path = field.Get("m_StreamData").Get("path");
                    var pathString = path.GetValue().AsString();
                    var directory = Path.GetDirectoryName(file.path);
                    if (directory == null) {
                        Debug.LogWarning("Texture2D has null stream data path!");
                        return;
                    }
                    var fixedPath = Path.Combine(directory, pathString);
                    path.GetValue().Set(fixedPath);
                    break;
                }
                case UnityTypes.AudioClip: {
                    var path = field.Get("m_Resource").Get("m_Source");
                    var pathString = path.GetValue().AsString();
                    var directory = Path.GetDirectoryName(file.path);
                    if (directory == null) {
                        Debug.LogWarning("AudioClip has null resource source path!");
                        return;
                    }
                    var fixedPath = Path.Combine(directory, pathString);
                    path.GetValue().Set(fixedPath);
                    break;
                }
            }
        }
        
        private void AddPointer(AssetID id, bool isAsset) {
            var name = isAsset ? AssetLevelName : SceneLevelName;
            var newId = new AssetID(name, isAsset ? this._curAssetId : this._curSceneId);
            _pointers.Add(id, newId);
            if (isAsset) this._curAssetId++;
            else this._curSceneId++;
        }

        private static string RemapAssemblyName(string assemblyName) {
            if (assemblyName.Equals("Assembly-CSharp.dll")) return "HKCode.dll";
            if (assemblyName.Equals("Assembly-CSharp-firstpass.dll")) return "HKCode-firstpass.dll";
            return assemblyName;
        }
    }
}