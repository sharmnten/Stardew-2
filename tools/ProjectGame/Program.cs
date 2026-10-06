using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var runtimeReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path)).ToList();
if (args is ["--self-test"])
{
    const string fixture = "namespace Microsoft.Xna.Framework.Graphics { class Texture2D { public int Width => 32; public int Height => 24; public int Bounds => 0; } class RenderTarget2D : Texture2D {} } class Rectangle { public int Width => 42; } class Fixture { int Run(Microsoft.Xna.Framework.Graphics.Texture2D texture, Microsoft.Xna.Framework.Graphics.RenderTarget2D target, Rectangle rectangle) => texture.Width + target.Height + texture.Bounds + (texture?.Width ?? 0) + rectangle.Width; }";
    var tree = CSharpSyntaxTree.ParseText(fixture);
    var compilation = CSharpCompilation.Create("fixture", [tree], runtimeReferences);
    var rewriter = new TextureDimensions(compilation.GetSemanticModel(tree));
    string output = rewriter.Visit(tree.GetRoot())!.ToFullString();
    if (rewriter.Count != 4 || !output.Contains("texture.BrowserWidth()") || !output.Contains("target.BrowserHeight()") ||
        !output.Contains("texture.BrowserBounds()") || !output.Contains("texture?.BrowserWidth()") || !output.Contains("rectangle.Width"))
        throw new Exception("Texture projection regression: " + output);
    Console.WriteLine("4 texture accesses; unrelated dimensions unchanged");
    return;
}
if (args.Length != 2) throw new ArgumentException("ProjectGame <projected-game-directory> <original-binaries-directory>");
string root = Path.GetFullPath(args[0]);
var references = runtimeReferences.Concat(Directory.GetFiles(args[1], "*.dll")
    .Where(path => !Path.GetFileName(path).StartsWith("System.") && Path.GetFileName(path) != "Stardew Valley.dll")
    .Select(path => MetadataReference.CreateFromFile(Path.GetFullPath(path))));
var trees = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
    .Order(StringComparer.Ordinal).Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
        new CSharpParseOptions(LanguageVersion.CSharp11), path)).ToArray();
var game = CSharpCompilation.Create("Stardew Valley", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
int total = 0;
foreach (var tree in trees)
{
    var rewriter = new TextureDimensions(game.GetSemanticModel(tree));
    var projected = rewriter.Visit(tree.GetRoot())!;
    if (rewriter.Count == 0) continue;
    File.WriteAllText(tree.FilePath, projected.ToFullString());
    total += rewriter.Count;
}
Console.WriteLine($"Projected {total} original texture dimension accesses using resolved framework property symbols.");

sealed class TextureDimensions(SemanticModel model) : CSharpSyntaxRewriter
{
    public int Count { get; private set; }
    private bool IsTextureProperty(SyntaxNode node) => model.GetSymbolInfo(node).Symbol is IPropertySymbol property &&
        property.Name is "Width" or "Height" or "Bounds" && property.ContainingType.ToDisplayString() == "Microsoft.Xna.Framework.Graphics.Texture2D";
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        bool adapt = IsTextureProperty(node);
        var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
        if (!adapt) return visited;
        Count++;
        return SyntaxFactory.InvocationExpression(visited.WithName(SyntaxFactory.IdentifierName("Browser" + node.Name.Identifier.Text)))
            .WithTriviaFrom(node);
    }
    public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (!IsTextureProperty(node)) return base.VisitMemberBindingExpression(node);
        Count++;
        return SyntaxFactory.InvocationExpression(node.WithName(SyntaxFactory.IdentifierName("Browser" + node.Name.Identifier.Text)))
            .WithTriviaFrom(node);
    }
}
