using System.IO;
using Mono.Cecil;

namespace HKExporter {
    public class AssemblyFixer {
        public static void RenameAssemblies(string oldName = "Assembly-CSharp", string newName = "HKCode", string inputDir = "../../", string outputDir = "../../") {
            // Cache relative paths
            var input = Path.Combine(inputDir, oldName);
            var output = Path.Combine(outputDir, newName);

            // Read main dll
            var assemblyCSharp = Mono.Cecil.AssemblyDefinition.ReadAssembly(input + ".dll");

            // Rename Assembly-CSharp
            assemblyCSharp.Name.Name = newName;
            //assemblyCSharp.MainModule.Name = newName;
            
            // Write modified main dll
            assemblyCSharp.Write(output + ".dll");

            // No need to continue if there is no firstpass dll
            if (!File.Exists(input + "-firstpass.dll")) return;

            // Rename firstpass dll
            var firstPass = Mono.Cecil.AssemblyDefinition.ReadAssembly(input + "-firstpass.dll");
            firstPass.Name.Name = newName + "-firstpass";
            //firstPass.MainModule.Name = newName + "-firstpass";

            // Loop through references
            foreach (var reference in assemblyCSharp.MainModule.AssemblyReferences) {
                if (reference.Name.Equals(oldName + "-firstpass")) {
                    reference.Name = newName + "-firstpass";
                }
            }
            
            // Write the new firstpass dll
            firstPass.Write(output + "-firstpass.dll");
        }
    }
}