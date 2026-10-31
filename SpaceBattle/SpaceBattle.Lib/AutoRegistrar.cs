using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using App;

namespace SpaceBattle.Lib;

public static class AutoRegistrar
{
    private const string AdapterTemplateResource =
        "SpaceBattle.Lib.Templates.AdapterTemplate.txt";

    private const string FactoryTemplateResource =
        "SpaceBattle.Lib.Templates.FactoryTemplate.txt";

    private static readonly ConcurrentDictionary<Type, ConstructorInfo>
        AdapterConstructors = new();

    private static readonly ConcurrentDictionary<Type, Func<object, object>>
        FactoryMethods = new();

    public static void RegisterAll(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var interfaceTypes = assembly
            .GetTypes()
            .Where(IsSupportedInterface)
            .ToArray();

        foreach (var interfaceType in interfaceTypes)
        {
            RegisterAdapter(interfaceType);
        }
    }

    public static void RegisterAllFactories(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var interfaceTypes = assembly
            .GetTypes()
            .Where(IsSupportedInterface)
            .ToArray();

        foreach (var interfaceType in interfaceTypes)
        {
            RegisterFactory(interfaceType);
        }
    }

    private static bool IsSupportedInterface(Type type)
    {
        if (!type.IsInterface || type.ContainsGenericParameters)
        {
            return false;
        }

        var properties = GetProperties(type).ToArray();
        if (properties.Length == 0)
        {
            return false;
        }

        if (properties.Any(property =>
                property.GetIndexParameters().Length > 0 ||
                property.GetMethod == null ||
                property.PropertyType.ContainsGenericParameters))
        {
            return false;
        }

        var accessors = properties
            .SelectMany(property => property.GetAccessors())
            .ToHashSet();

        return type.GetMethods().All(accessors.Contains);
    }

    private static IEnumerable<PropertyInfo> GetProperties(Type type)
    {
        return type
            .GetInterfaces()
            .Append(type)
            .SelectMany(interfaceType =>
                interfaceType.GetProperties(
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly))
            .DistinctBy(property => property.Name);
    }

    private static void RegisterAdapter(Type interfaceType)
    {
        var constructor = AdapterConstructors.GetOrAdd(
            interfaceType,
            CreateAdapterConstructor);

        var factory = new AdapterFactory(
            entity => constructor.Invoke(new[] { entity }));

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                $"Adapters.{interfaceType.Name}",
                (object[] args) =>
                {
                    if (args.Length == 0 || args[0] == null)
                    {
                        throw new ArgumentException(
                            "Для создания адаптера необходимо передать словарь (entity).");
                    }

                    return factory.Create(args[0]);
                }
            ).Execute();
        }
        catch (ArgumentException)
        {
        }
    }

    private static void RegisterFactory(Type interfaceType)
    {
        var factoryFunc = FactoryMethods.GetOrAdd(
            interfaceType,
            CreateFactoryDelegate);

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                $"Factories.{interfaceType.Name}",
                (object[] args) =>
                {
                    if (args.Length == 0 || args[0] == null)
                    {
                        throw new ArgumentException(
                            "Для создания фабрики необходимо передать словарь (entity).");
                    }

                    return factoryFunc(args[0]);
                }
            ).Execute();
        }
        catch (ArgumentException)
        {
        }
    }

    private static ConstructorInfo CreateAdapterConstructor(Type interfaceType)
    {
        var source = GenerateAdapterSource(interfaceType);
        var adapterType = CompileAdapter(source, interfaceType);
        return adapterType.GetConstructor(new[] { typeof(object) })
            ?? throw new InvalidOperationException(
                $"У сгенерированного адаптера {adapterType.FullName} " +
                "нет конструктора с параметром object.");
    }

    private static Func<object, object> CreateFactoryDelegate(Type interfaceType)
    {
        var source = GenerateFactorySource(interfaceType);
        var factoryType = CompileFactory(source, interfaceType);

        var createMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Не найден метод Create.");

        return (entity) => createMethod.Invoke(null, new[] { entity })!;
    }

    private static string GenerateAdapterSource(Type interfaceType)
    {
        var template = ReadTemplate(AdapterTemplateResource);

        template = template.Replace("{{InterfaceName}}", GetTypeName(interfaceType));

        var generatedProperties = string.Join(
            Environment.NewLine,
            GetProperties(interfaceType)
                .Select(GeneratePropertySource));

        return template + $@"

namespace SpaceBattle.Lib.Generated
{{
    public partial class GeneratedAdapter
    {{
        {generatedProperties}
    }}
}}";
    }

    private static string GenerateFactorySource(Type interfaceType)
    {
        var generatedProperties = string.Join(
            Environment.NewLine,
            GetProperties(interfaceType)
                .Select(GeneratePropertySource));

        return ReadTemplate(FactoryTemplateResource)
            .Replace("{{InterfaceName}}", GetTypeName(interfaceType))
            .Replace("{{GeneratedProperties}}", generatedProperties);
    }

    private static string GeneratePropertySource(PropertyInfo property)
    {
        var setter = property.SetMethod == null
            ? string.Empty
            : $"set => PropertyAdapterFactory.CreateSetCommand(_values, nameof({property.Name}), value).Execute();";

        var typeName = GetTypeName(property.PropertyType);
        var name = property.Name;

        return $@"
        public {typeName} {name}
        {{
            get => PropertyAdapterFactory.Get<{typeName}>(
                _values,
                nameof({name}));
            {setter}
        }}";
    }

    private static string ReadTemplate(string resourceName)
    {
        using var stream = typeof(AutoRegistrar)
            .Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Не найден встроенный шаблон: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string GetTypeName(Type type)
    {
        if (type.IsArray)
        {
            return $"{GetTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var genericName = type.GetGenericTypeDefinition().FullName!;
            var tickIndex = genericName.IndexOf('`');
            if (tickIndex >= 0)
            {
                genericName = genericName[..tickIndex];
            }
            return $"global::{genericName.Replace('+', '.')}<" +
                   string.Join(
                       ", ",
                       type.GetGenericArguments().Select(GetTypeName)) +
                   ">";
        }

        return "global::" +
               (type.FullName
                   ?? throw new InvalidOperationException(
                       $"Не удалось определить имя типа {type}."))
               .Replace('+', '.');
    }

    private static Type CompileAdapter(string source, Type interfaceType)
    {
        return Compile(source, interfaceType, "GeneratedAdapter", "адаптера");
    }

    private static Type CompileFactory(string source, Type interfaceType)
    {
        return Compile(source, interfaceType, "GeneratedAdapterFactory", "фабрики");
    }

    private static Type Compile(string source, Type interfaceType, string typeName, string description)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var trustedAssemblies = (string?)AppContext.GetData(
            "TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException(
                "Не удалось получить список системных сборок .NET.");

        var references = trustedAssemblies
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(
            MetadataReference.CreateFromFile(
                typeof(PropertyAdapterFactory).Assembly.Location));
        references.Add(
            MetadataReference.CreateFromFile(
                interfaceType.Assembly.Location));

        var compilation = CSharpCompilation.Create(
            $"Generated_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references.DistinctBy(reference => reference.Display),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        if (!result.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                result.Diagnostics
                    .Where(diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException(
                $"Ошибка компиляции {description}:{Environment.NewLine}{errors}");
        }

        stream.Position = 0;
        var generatedAssembly = Assembly.Load(stream.ToArray());

        return generatedAssembly.GetTypes()
            .Single(type => type.Name == typeName);
    }
}
