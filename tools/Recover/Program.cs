using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;

if (args.Length != 2)
    throw new ArgumentException("Usage: Recover <assembly.dll> <output-directory>");
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
var project = new WholeProjectDecompiler(settings, resolver, null, null, null);
Directory.CreateDirectory(outputPath);
project.DecompileProject(module, outputPath);
Console.WriteLine($"Recovered {module.Name} to {outputPath}");
