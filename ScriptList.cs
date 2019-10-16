using System.Collections.Generic;

namespace HKExporter {
    public class ScriptList {
        private readonly bool _noScriptData;
        private readonly List<string> _whitelist;
        private readonly List<string> _blacklist;

        public ScriptList(bool noScriptData, List<string> whitelist = null, List<string> blacklist = null) {
            this._noScriptData = noScriptData;
            this._whitelist = whitelist;
            this._blacklist = blacklist;
        }

        public bool IsWhitelistMode() {
            return this._noScriptData;
        }

        public bool IsIgnored(string className, string assemblyname) {
            return this._noScriptData ? !this.IsWhitelisted(className, assemblyname) : this.IsBlacklisted(className, assemblyname);
        }

        public bool IsBlacklisted(string className, string assemblyName) {
            return this._blacklist != null && this._blacklist.Contains(GetScriptName(className, assemblyName));
        }

        private bool IsWhitelisted(string className, string assemblyName) {
            return this._whitelist != null && this._whitelist.Contains(GetScriptName(className, assemblyName));
        }

        public static string GetScriptName(string className, string assemblyName) {
            return className + "/" + assemblyName;
        }
    }
}