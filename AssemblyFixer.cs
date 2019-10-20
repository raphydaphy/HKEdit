using System.IO;
using Mono.Cecil;

namespace HKExporter {
    public class AssemblyFixer {
        public static void RenameAssemblies(string oldName = "Assembly-CSharp", string newName = "HKCode", string inputDir = "../../", string outputDir = "../../") {
            // Cache relative paths
            var input = Path.Combine(inputDir, oldName);
            var output = Path.Combine(outputDir, newName);

            // Read main dll
            var assemblyCSharp = AssemblyDefinition.ReadAssembly(input + ".dll");

            // Rename Assembly-CSharp
            assemblyCSharp.Name.Name = newName;
            //assemblyCSharp.MainModule.Name = newName;
            
            if (File.Exists(input + "-firstpass.dll")) {
                // Rename firstpass dll
                var firstPass = AssemblyDefinition.ReadAssembly(input + "-firstpass.dll");
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

            // Write the new main dll after fixing the firstpass references
            assemblyCSharp.Write(output + ".dll");
        }
    }
}