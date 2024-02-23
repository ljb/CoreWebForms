// MIT License.

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Compiler.Generator;

[Generator]
public class PreApplicationStartGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidPreApplicationStartMethod = new DiagnosticDescriptor(
        id: "WEBFORMS1000",
        title: "Invalid PreApplicationStartMethod",
        messageFormat: "Invalid PreApplicationStartMethod in {0} for {1}.{2}",
        category: "WebForms",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private sealed record PreApplicationStartMethod(string Assembly, string TypeName, string MethodName, bool IsStatic, bool IsValid);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var invocation = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node.TryGetMapMethodName(out var method) && method == "AddPreApplicationStartMethod",
            transform: static (context, token) =>
            {
                var operation = context.SemanticModel.GetOperation(context.Node, token);
                if (operation is IInvocationOperation invocation)
                {
                    return invocation.GetLocation();
                }
                return default;
            })
            .Where(static invocation => invocation is { });

        var startMethods = context.CompilationProvider.Select((compilation, token) =>
        {
            // We must search this way as the type itself is type forwarded from System.Web and there may be multiple symbol entries
            var potentialAttributes = compilation.GlobalNamespace.ConstituentNamespaces
                        .Select(n => n.ContainingAssembly)
                        .Select(a => a.GetTypeByMetadataName("System.Web.PreApplicationStartMethodAttribute"));

            var set = new HashSet<INamedTypeSymbol?>(potentialAttributes, SymbolEqualityComparer.Default);

            var builder = ImmutableArray.CreateBuilder<PreApplicationStartMethod>();

            if (set.Count > 0)
            {
                foreach (var n in compilation.GlobalNamespace.ConstituentNamespaces)
                {
                    foreach (var a in n.ContainingAssembly.GetAttributes())
                    {
                        if (set.Contains(a.AttributeClass))
                        {
                            if (a.ConstructorArguments is [{ Kind: TypedConstantKind.Type, Value: INamedTypeSymbol type }, { Kind: TypedConstantKind.Primitive, Value: string name }])
                            {
                                if (IsValidMethod(type, name) is { } member)
                                {
                                    builder.Add(new(type.ContainingAssembly.Name, type.ToString(), name, member.IsStatic, IsValid: true));
                                }
                                else
                                {
                                    builder.Add(new(type.ContainingAssembly.Name, type.ToString(), name, false, IsValid: false));
                                }

                                static IMethodSymbol? IsValidMethod(INamedTypeSymbol type, string name)
                                {
                                    if (!type.TypeParameters.IsDefaultOrEmpty)
                                    {
                                        return null;
                                    }

                                    if (type.GetMembers(name) is not [IMethodSymbol method])
                                    {
                                        return null;
                                    }

                                    if (method is { IsStatic: false } && !type.Constructors.Any(c => c.Parameters.IsDefaultOrEmpty))
                                    {
                                        return null;
                                    }

                                    if (method is { TypeArguments.IsDefaultOrEmpty: true, Parameters.IsDefaultOrEmpty: true, ReturnsVoid: true } member)
                                    {
                                        return method;
                                    }

                                    return null;
                                }
                            }
                        }

                    }
                }
            }

            return builder.ToImmutable();
        });

        context.RegisterSourceOutput(invocation.Combine(startMethods), (context, source) =>
        {
            if (source.Left is not { } location)
            {
                return;
            }

            var startups = source.Right;

            using var str = new StringWriter();
            using var indented = new IndentedTextWriter(str);
            indented.WriteLine("﻿// <auto-generated />");
            indented.WriteLine();
            indented.WriteLine("using Microsoft.AspNetCore.Builder;");
            indented.WriteLine("using Microsoft.AspNetCore.Hosting;");
            indented.WriteLine("using Microsoft.AspNetCore.SystemWebAdapters;");
            indented.WriteLine("using Microsoft.AspNetCore.SystemWebAdapters.HttpHandlers;");
            indented.WriteLine("using Microsoft.Extensions.FileProviders;");
            indented.WriteLine("using Microsoft.Extensions.Primitives;");
            indented.WriteLine("using Microsoft.Extensions.DependencyInjection;");
            indented.WriteLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            indented.WriteLine("using Microsoft.Extensions.Options;");
            indented.WriteLine("using Microsoft.Extensions.Logging;");
            indented.WriteLine();
            indented.WriteLine("#pragma warning disable");
            indented.WriteLine();
            indented.WriteLine("namespace WebForms.Generated");
            indented.WriteLine("{");
            indented.Indent++;
            indented.WriteLine("internal static class InterceptedPreApplicationStartMethods");
            indented.WriteLine("{");
            indented.Indent++;
            indented.Write("[System.Runtime.CompilerServices.InterceptsLocation(\"");
            indented.Write(location.FilePath.Replace("\\", "\\\\"));
            indented.Write("\", ");
            indented.Write(location.Line);
            indented.Write(", ");
            indented.Write(location.Character);
            indented.WriteLine(")]");
            indented.WriteLine("internal static ISystemWebAdapterBuilder AddPreApplicationStartMethod(this ISystemWebAdapterBuilder builder, bool failOnError = true)");
            indented.WriteLine("{");
            indented.Indent++;
            indented.WriteLine("builder.Services.AddOptions<PreApplicationOptions>()");
            indented.Indent++;
            indented.WriteLine(".Configure(options => options.FailOnError = failOnError);");
            indented.Indent--;
            indented.WriteLine();
            indented.WriteLine("builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, PreApplicationStartMethodStartupFilter>());");
            indented.WriteLine("return builder;");
            indented.Indent--;
            indented.WriteLine("}");
            indented.WriteLine();

            indented.WriteLine("private sealed class PreApplicationOptions");
            indented.WriteLine("{");
            indented.Indent++;
            indented.WriteLine("public bool FailOnError { get; set; }");
            indented.Indent--;
            indented.WriteLine("}");
            indented.WriteLine();

            indented.WriteLine("private sealed class PreApplicationStartMethodStartupFilter(IOptions<PreApplicationOptions> options, ILogger<PreApplicationStartMethodStartupFilter> logger) : IStartupFilter");
            indented.WriteLine("{");
            indented.Indent++;
            indented.WriteLine("public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)");
            indented.Indent++;
            indented.WriteLine("=> builder =>");
            indented.WriteLine("{");
            indented.Indent++;
            indented.WriteLine("try");
            indented.WriteLine("{");
            indented.Indent++;
            indented.WriteLine("RunStartupMethods();");
            indented.Indent--;
            indented.WriteLine("}");
            indented.WriteLine("catch when (!options.Value.FailOnError)");
            indented.WriteLine("{");
            indented.WriteLine("}");
            indented.WriteLine();
            indented.WriteLine("next(builder);");
            indented.Indent--;
            indented.WriteLine("};");
            indented.Indent--;
            indented.WriteLine();
            indented.WriteLine("private void RunStartupMethods()");
            indented.WriteLine("{");

            indented.Indent++;

            foreach (var (assembly, typeName, methodName, isStatic, isValid) in startups)
            {
                if (!isValid)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidPreApplicationStartMethod, location: null, assembly, typeName, methodName));
                    indented.Write("// Invalid pre application start method: ");
                }

                if (isStatic)
                {
                    indented.WriteLine($"{typeName}.{methodName}();");
                }
                else
                {
                    indented.WriteLine($"new {typeName}().{methodName}();");
                }
            }

            indented.Indent--;
            indented.WriteLine("}");
            indented.Indent--;
            indented.WriteLine("}");
            indented.Indent--;
            indented.WriteLine("}");
            indented.Indent--;
            indented.WriteLine("}");
            indented.Indent--;
            indented.WriteLine();
            indented.Write("""
                    namespace System.Runtime.CompilerServices
                    {
                        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
                        file sealed class InterceptsLocationAttribute(string filePath, int line, int character) : Attribute
                        {
                        }
                    }
                    """);

            context.AddSource("PreApplicationStartMethod", str.ToString());
        });
    }
}
