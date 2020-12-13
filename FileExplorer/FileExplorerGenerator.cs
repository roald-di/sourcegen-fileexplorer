namespace FileExplorer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;

    [Generator]
    public class FileExplorerGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var filesByType = context.AdditionalFiles
                .Select(file =>
                {
                    var options = context.AnalyzerConfigOptions.GetOptions(file);

                    options.TryGetValue("build_metadata.AdditionalFiles.TypeName", out var typeName);
                    options.TryGetValue("build_metadata.AdditionalFiles.RelativeTo", out var relativeTo);
                    options.TryGetValue("build_metadata.AdditionalFiles.BrowseFrom", out var browseFrom);

                    return new { typeName, file.Path, relativeTo, browseFrom };
                })
                .Where(file => !string.IsNullOrEmpty(file.typeName) && !string.IsNullOrEmpty(file.relativeTo) && !string.IsNullOrEmpty(file.browseFrom))
                .GroupBy(file => file.typeName, file => File.Create(file.Path, file.relativeTo!, file.browseFrom!));

            foreach (var files in filesByType)
            {
                var (namespaceName, typeName) = SplitLast(files.Key!, '.');

                var root = Folder.Create(typeName, files.Where(file => ValidateFile(file, context)).ToArray());

                var result = @$"
                    namespace {namespaceName ?? "FileExplorer"}
                    {{
                        {Generate(root)}
                    }}";

                var formatted = SyntaxFactory.ParseCompilationUnit(result).NormalizeWhitespace().ToFullString();
                context.AddSource($"FileExplorer_{typeName}", SourceText.From(formatted, Encoding.UTF8));
            }            
        }

        static string Generate(Folder folder)
            => @$"               
                public static partial class {FormatIdentifier(folder.Name)}
                {{
                    {string.Concat(folder.Folders.Select(Generate))}
                    {string.Concat(folder.Files.Select(Generate))}
                }}";

        static string Generate(File file)
        {
            static string Escape(string segment) => $"@\"{segment.Replace("\"", "\"\"")}\"";

            var path = file.RuntimePath
                .Append(file.RuntimeName)
                .Select(Escape);

            return @$"public static readonly string @{FormatIdentifier(file.DesigntimeName)} = System.IO.Path.Combine({string.Join(", ", path)});";
        }

        static readonly DiagnosticDescriptor invalidFileSegment = new("FE0001", "Invalid path segment", "The path '{0}' contains some segments that are not valid as identifiers: {1}", "Naming", DiagnosticSeverity.Warning, true);

        static bool ValidateFile(File file, GeneratorExecutionContext context)
        {
            static bool IsInvalidIdentifier(string text)
                => char.IsDigit(text[0]) || text.Any(character => !char.IsDigit(character) && !char.IsLetter(character) && character != '_');

            var invalid = file.DesigntimePath
                .Append(file.DesigntimeName)
                .Where(IsInvalidIdentifier)
                .ToArray();

            if (invalid.Any())
            {
                var fullPath = Path.Combine(file.RuntimePath.Append(file.RuntimeName).ToArray());
                context.ReportDiagnostic(Diagnostic.Create(invalidFileSegment, Location.None, fullPath, string.Join(", ", invalid.Select(segment => $"'{segment}'"))));
            }

            return !invalid.Any();
        }
        
        static string FormatIdentifier(string text)
        {
            var result = text.ToCharArray();

            result[0] = char.ToUpper(result[0]);

            return new string(result);
        }

        static (string?, string) SplitLast(string text, char delimiter)
        {
            var index = text.LastIndexOf(delimiter);

            return index == -1
                ? (null, text)
                : (text.Substring(0, index), text.Substring(index + 1));
        }

        record File(IReadOnlyList<string> DesigntimePath, IReadOnlyList<string> RuntimePath, string DesigntimeName, string RuntimeName)
        {
            public IReadOnlyList<string> DesigntimePath { get; } = DesigntimePath;
            public IReadOnlyList<string> RuntimePath { get; } = RuntimePath;
            public string DesigntimeName { get; } = DesigntimeName;

            public string RuntimeName { get; } = RuntimeName;

            public static File Create(string absolutePath, string runtimeRoot, string designtimeRoot)
            {
                static string[] MakeRelative(string absolute, string to) =>
                    Path.GetDirectoryName(absolute.Replace('/', Path.DirectorySeparatorChar))!
                        .Split(new[] { to.Replace('/', Path.DirectorySeparatorChar) }, StringSplitOptions.None)
                        .Last()
                        .Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                var designtimePath = MakeRelative(absolutePath, designtimeRoot);
                var runtimePath = MakeRelative(absolutePath, runtimeRoot);

                return new File
                (
                    designtimePath,
                    runtimePath,
                    Path.GetFileNameWithoutExtension(absolutePath) + Path.GetExtension(absolutePath).Replace('.', '_'),
                    Path.GetFileName(absolutePath)
                );
            }
        }

        record Folder(string Name, IReadOnlyList<Folder> Folders, IReadOnlyList<File> Files)
        {
            public string Name { get; } = Name;
            public IReadOnlyList<Folder> Folders { get; } = Folders;
            public IReadOnlyList<File> Files { get; } = Files;

            public static Folder Create(string name, IReadOnlyList<File> files)
                => Create(name, files, 0);

            static Folder Create(string name, IReadOnlyList<File> files, int level)
            {
                var folders = files
                    .Where(file => file.DesigntimePath.Count > level)
                    .GroupBy(file => file.DesigntimePath[level])
                    .Select(next => Create(next.Key, next.ToArray(), level + 1))
                    .ToArray();

                return new Folder(name, folders, files.Where(file => file.DesigntimePath.Count == level).ToArray());
            }
        }
    }
}