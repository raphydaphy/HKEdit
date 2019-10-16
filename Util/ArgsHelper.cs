using System.Linq;

namespace HKExporter.Util {
    public class ArgsHelper {
        private readonly string[] _args;
        public ArgsHelper(string[] args) {
            this._args = args;
        }
        
        public bool IsPresent(string name) {
            return this._args.Contains("-" + name);
        }
        
        public string GetValue(string name, string fallback) {
            for (var i = 0; i < this._args.Length; i++) {
                var arg = this._args[i];
                if (arg.Equals("-" + name) && i < this._args.Length - 1) {
                    return this._args[i + 1];
                }
            }
            return fallback;
        }

        public static string GetBoolString(bool value) {
            return value ? "enabled" : "disabled";
        }
    }
}