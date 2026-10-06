using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using System.Reflection.Metadata;

if (args.Length is < 2 or > 3)
    throw new ArgumentException("Usage: Recover <assembly.dll> <output-directory> [namespace]");
string assemblyPath = Path.GetFullPath(args[0]);
string outputPath = Path.GetFullPath(args[1]);
if (Directory.Exists(outputPath))
    throw new IOException($"Refusing to overwrite recovered source: {outputPath}");
using var module = new PEFile(assemblyPath);
var resolver = new UniversalAssemblyResolver(assemblyPath, false, module.DetectTargetFrameworkId());
resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath)!);
var settings = new DecompilerSettings(LanguageVersion.CSharp11_0)
{
    UseSdkStyleProjectFormat = true,
    LocalFunctions = false,
    StaticLocalFunctions = false,
    AnonymousMethods = false,
    AnonymousTypes = false,
    UsePrimaryConstructorSyntax = false,
    UsePrimaryConstructorSyntaxForNonRecordTypes = false
};
WholeProjectDecompiler project = args.Length == 3
    ? new NamespaceProjectDecompiler(settings, resolver, args[2])
    : new WholeProjectDecompiler(settings, resolver, null, null, null);
Directory.CreateDirectory(outputPath);
project.DecompileProject(module, outputPath);
Console.WriteLine($"Recovered {module.Name} to {outputPath}");

sealed class NamespaceProjectDecompiler(DecompilerSettings settings, IAssemblyResolver resolver, string namespaceName)
    : WholeProjectDecompiler(settings, resolver, null, null, null)
{
    protected override bool IncludeTypeWhenDecompilingProject(MetadataFile module, TypeDefinitionHandle type)
    {
        var definition = module.Metadata.GetTypeDefinition(type);
        return module.Metadata.GetString(definition.Namespace) == namespaceName
            && base.IncludeTypeWhenDecompilingProject(module, type);
    }
}
