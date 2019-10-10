using Mono.Cecil;

namespace HKExporter {
    public class AssemblyFixer {
        public static void RenameAssemblies(string oldName = "Assembly-CSharp", string newName = "HKCode") {
            // Cache relative paths
            var input = "../../" + oldName;
            var output = "../../" + newName;

            // Read both inputs
            var assemblyCSharp = Mono.Cecil.AssemblyDefinition.ReadAssembly(input + ".dll");
            var firstPass = Mono.Cecil.AssemblyDefinition.ReadAssembly(input + "-firstpass.dll");

            // Rename Assembly-CSharp
            assemblyCSharp.Name.Name = newName;
            //assemblyCSharp.MainModule.Name = newName;

            // Rename firstpass dll
            firstPass.Name.Name = newName + "-firstpass";
            //firstPass.MainModule.Name = newName + "-firstpass";

            // Loop through references
            foreach (var reference in assemblyCSharp.MainModule.AssemblyReferences) {
                if (reference.Name.Equals(oldName + "-firstpass")) {
                    reference.Name = newName + "-firstpass";
                }
            }

            // Write both outputs
            assemblyCSharp.Write(output + ".dll");
            firstPass.Write(output + "-firstpass.dll");
        }
    }
}