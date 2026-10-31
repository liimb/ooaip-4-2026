using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using App;

namespace SpaceBattle.Lib;

public static class AutoRegistrar
{
    private const string AdapterTemplateResource = "SpaceBattle.Lib.Templates.AdapterTemplate.txt";
    private const string FactoryTemplateResource = "SpaceBattle.Lib.Templates.FactoryTemplate.txt";
    private const string CommandFactoryTemplateResource = "SpaceBattle.Lib.Templates.CommandFactoryTemplate.txt";

    private static readonly ConcurrentDictionary<Type, Func<object, object>> AdapterFactories = new();

    public static void RegisterAll(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var interfaceTypes = assembly.GetTypes().Where(IsSupportedInterface).ToArray();
        foreach (var interfaceType in interfaceTypes)
        {
            RegisterAdapter(interfaceType);
        }

        RegisterCommandFactory();
    }

    private static bool IsSupportedInterface(Type type)
    {
        if (!type.IsInterface || type.ContainsGenericParameters) return false;
        var properties = GetProperties(type).ToArray();
        if (properties.Length == 0) return false;
        if (properties.Any(p => p.GetIndexParameters().Length > 0 || p.GetMethod == null || p.PropertyType.ContainsGenericParameters))
            return false;
        var accessors = properties.SelectMany(p => p.GetAccessors()).ToHashSet();
        return type.GetMethods().All(accessors.Contains);
    }

    private static IEnumerable<PropertyInfo> GetProperties(Type type)
    {
        return type.GetInterfaces().Append(type)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .DistinctBy(p => p.Name);
    }

    private static void RegisterAdapter(Type interfaceType)
    {
        var factoryFunc = AdapterFactories.GetOrAdd(interfaceType, CreateAdapterFactoryDelegate);

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                $"Adapters.{interfaceType.Name}",
                (object[] args) =>
                {
                    if (args.Length == 0 || args[0] == null)
                        throw new ArgumentException("Необходимо передать словарь.");
                    return factoryFunc(args[0]);
                }
            ).Execute();
        }
        catch (ArgumentException) { }
    }

    private static Func<object, object> CreateAdapterFactoryDelegate(Type interfaceType)
    {
        var adapterSource = GenerateAdapterSource(interfaceType);
        var factorySource = ReadTemplate(FactoryTemplateResource).Replace("{{InterfaceName}}", GetTypeName(interfaceType));

        var factorySourceWithoutUsings = string.Join(
            Environment.NewLine,
            factorySource.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                .Where(line => !line.TrimStart().StartsWith("using ")));

        var generatedAssembly = CompileSource(adapterSource + Environment.NewLine + factorySourceWithoutUsings, interfaceType);
        var factoryType = generatedAssembly.GetTypes().Single(t => t.Name == "GeneratedAdapterFactory");
        var createMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;

        return (entity) => createMethod.Invoke(null, new[] { entity })!;
    }

    private static void RegisterCommandFactory()
    {
        try
        {
            var source = ReadTemplate(CommandFactoryTemplateResource);
            var generatedAssembly = CompileSource(source, typeof(ICommand));
            var factoryType = generatedAssembly.GetTypes().Single(t => t.Name == "GeneratedCommandFactory");
            var createMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Factory.Generated",
                (object[] args) => createMethod.Invoke(null, new object[] { args })!
            ).Execute();
        }
        catch (ArgumentException) { }
    }

    private static string GenerateAdapterSource(Type interfaceType)
    {
        var template = ReadTemplate(AdapterTemplateResource).Replace("{{InterfaceName}}", GetTypeName(interfaceType));
        var generatedProperties = string.Join(Environment.NewLine, GetProperties(interfaceType).Select(GeneratePropertySource));

        return template + $@"
namespace SpaceBattle.Lib.Generated
{{
    public partial class GeneratedAdapter
    {{
        {generatedProperties}
    }}
}}";
    }

    private static string GeneratePropertySource(PropertyInfo property)
    {
        var setter = property.SetMethod == null ? string.Empty : $"set => PropertyAdapterFactory.CreateSetCommand(_values, nameof({property.Name}), value).Execute();";
        var typeName = GetTypeName(property.PropertyType);
        return $@"
        public {typeName} {property.Name}
        {{
            get => PropertyAdapterFactory.Get<{typeName}>(_values, nameof({property.Name}));
            {setter}
        }}";
    }

    private static string ReadTemplate(string resourceName)
    {
        using var stream = typeof(AutoRegistrar).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Не найден встроенный шаблон: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string GetTypeName(Type type)
    {
        if (type.IsArray) return $"{GetTypeName(type.GetElementType()!)}[]";
        if (type.IsGenericType)
        {
            var genericName = type.GetGenericTypeDefinition().FullName!;
            var tickIndex = genericName.IndexOf('`');
            if (tickIndex >= 0) genericName = genericName[..tickIndex];
            return $"global::{genericName.Replace('+', '.')}<{string.Join(", ", type.GetGenericArguments().Select(GetTypeName))}>";
        }
        return "global::" + (type.FullName ?? throw new InvalidOperationException()).Replace('+', '.');
    }

    private static Assembly CompileSource(string source, Type referenceType)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? throw new InvalidOperationException();
        var references = trustedAssemblies.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(PropertyAdapterFactory).Assembly.Location));
        if (referenceType.Assembly.Location != string.Empty)
            references.Add(MetadataReference.CreateFromFile(referenceType.Assembly.Location));

        var compilation = CSharpCompilation.Create(
            $"Generated_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references.DistinctBy(r => r.Display),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        if (!result.Success)
        {
            var errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Ошибка компиляции:{Environment.NewLine}{errors}");
        }

        stream.Position = 0;
        return Assembly.Load(stream.ToArray());
    }
}
