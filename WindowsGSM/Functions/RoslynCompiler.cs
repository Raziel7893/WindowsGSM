using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System;
using ICSharpCode.SharpZipLib.Zip;
using System.Text;
using WindowsGSM.Functions;
using Microsoft.Extensions.DependencyModel;

public class RoslynCompiler
{
    readonly CSharpCompilation _compilation;
    Assembly _generatedAssembly;
    Type? _proxyType;
    string _assemblyName;
    string _typeName;
    PluginMetadata _pluginMetadata;

    public RoslynCompiler(string typeName, string code, Type[] typesToReference, PluginMetadata pluginMetadata)
    {
        _pluginMetadata = pluginMetadata;
        _typeName = typeName;

        var refs = DependencyContext.Default.CompileLibraries // filter out some libs?
            .SelectMany(cl => cl.ResolveReferencePaths())
            .Select(asm => MetadataReference.CreateFromFile(asm))
            .ToList();

        refs.Add(MetadataReference.CreateFromFile(typeof(RoslynCompiler).Assembly.Location)); 
        refs.Add(MetadataReference.CreateFromFile(typeof(Newtonsoft.Json.JsonConvert).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(ZipFile).Assembly.Location));

        //generate syntax tree from code and config compilation options
        var syntax = CSharpSyntaxTree.ParseText(code);
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: true,
            optimizationLevel: OptimizationLevel.Release);

        _compilation = CSharpCompilation.Create(_assemblyName = Guid.NewGuid().ToString(), new List<SyntaxTree> { syntax }, refs, options);
    }

    public Type Compile()
    {

        if (_proxyType != null) return _proxyType;

        using (var ms = new MemoryStream())
        {
            var result = _compilation.Emit(ms);
            if (!result.Success)
            {
                var compilationErrors = result.Diagnostics.Where(diagnostic =>
                        diagnostic.IsWarningAsError ||
                        diagnostic.Severity == DiagnosticSeverity.Error)
                    .ToList();
                if (compilationErrors.Any())
                {
                    var firstError = compilationErrors.First();
                    var errorNumber = firstError.Id;
                    var errorDescription = firstError.GetMessage();
                    var firstErrorMessage = $"{errorNumber}: {errorDescription};";
                    var exception = new Exception($"Compilation failed, first error is: {firstErrorMessage}");
                    compilationErrors.ForEach(e => { if (!exception.Data.Contains(e.Id)) exception.Data.Add(e.Id, e.GetMessage()); });

                    var sb = new StringBuilder();
                    foreach (var data in compilationErrors)
                    {
                        sb.Append($"{data.Id}\nLine: {data.Location} - Properties: {string.Join(";", data.Properties.Values)}\n\n");
                    }


                    _pluginMetadata.Error = sb.ToString();
                        Console.WriteLine(_pluginMetadata.Error);
                    
                    throw exception;
                }
            }
            ms.Seek(0, SeekOrigin.Begin);

            _generatedAssembly = AssemblyLoadContext.Default.LoadFromStream(ms);

            _proxyType = _generatedAssembly.GetType(_typeName);
            return _proxyType;
        }
    }
}