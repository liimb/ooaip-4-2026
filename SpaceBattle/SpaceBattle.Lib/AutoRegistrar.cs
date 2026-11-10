using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    private const string CommandFactoryTemplateResource =
        "SpaceBattle.Lib.Templates.CommandFactoryTemplate.txt";

    private const string CommandTemplateResource =
        "SpaceBattle.Lib.Templates.CommandTemplate.txt";

    private static readonly ConcurrentDictionary<Type, Lazy<Func<object, object>>>
        AdapterFactories = new();

    private static readonly ConcurrentDictionary<Type, Lazy<Func<object[], object>>> CommandFactories = new();
    private static readonly Lazy<Func<object[], object>> MacroFactory = new(CreateCommandFactory);

    private static readonly ConditionalWeakTable<object, HashSet<string>> Registrations = new();

    private static void Register(string name, Func<object[], object> factory)
    {
        var scope = Ioc.Resolve<object>("IoC.Scope.Current");
        var names = Registrations.GetOrCreateValue(scope);
        lock (names)
        {
            if (names.Contains(name))
            {
                return;
            }

            Ioc.Resolve<ICommand>("IoC.Register", name, factory).Execute();
            names.Add(name);
        }
    }

    public static void RegisterAll(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var interfaceTypes = assembly.GetTypes()
            .Where(IsSupportedInterface)
            .ToArray();

        foreach (var interfaceType in interfaceTypes)
        {
            RegisterAdapter(interfaceType);
        }

        RegisterCommandFactory();
        RegisterCommands(assembly);
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
        return type.GetInterfaces()
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
        var factory = AdapterFactories.GetOrAdd(
            interfaceType,
            type => new Lazy<Func<object, object>>(() => CreateAdapterFactoryDelegate(type))).Value;

        Func<object[], object> adapterFactory = args =>
        {
            if (args == null || args.Length == 0 || args[0] == null)
            {
                throw new ArgumentException(
                    "Необходимо передать исходный объект.");
            }

            if (args[0] is not IDictionary<string, object>)
            {
                throw new ArgumentException(
                    "Исходный объект должен реализовывать " +
                    "IDictionary<string, object>.");
            }

            return factory(args[0]);
        };

        Register(
            $"Adapters.{interfaceType.Name}",
            adapterFactory);

        Register(
            GetAdapterDependencyName(interfaceType),
            adapterFactory);
    }

    private static Func<object, object> CreateAdapterFactoryDelegate(
        Type interfaceType)
    {
        var adapterSource = GenerateAdapterSource(interfaceType);

        var factorySource = ReadTemplate(FactoryTemplateResource)
            .Replace(
                "{{InterfaceName}}",
                GetTypeName(interfaceType));
        var factorySourceWithoutUsings = string.Join(
            Environment.NewLine,
            factorySource
                .Split(
                    new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.None)
                .Where(line =>
                    !line.TrimStart().StartsWith(
                        "using ",
                        StringComparison.Ordinal)));

        var generatedAssembly = CompileSource(
            adapterSource + Environment.NewLine +
            factorySourceWithoutUsings,
            interfaceType);

        var factoryType = generatedAssembly.GetTypes()
            .Single(type =>
                type.Name == "GeneratedAdapterFactory");

        var createMethod = GetCreateMethod(factoryType);

        return createMethod.CreateDelegate<Func<object, object>>();
    }

    private static void RegisterCommands(Assembly assembly)
    {
        var commandTypes = assembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                typeof(ICommand).IsAssignableFrom(type) &&
                type.Name.EndsWith(
                    "Command",
                    StringComparison.Ordinal))
            .Select(type => new
            {
                CommandType = type,
                Constructor = type.GetConstructors(
                        BindingFlags.Public |
                        BindingFlags.Instance)
                    .SingleOrDefault(constructor =>
                    {
                        var parameters = constructor.GetParameters();

                        return parameters.Length == 1 &&
                               parameters[0].ParameterType.IsInterface;
                    })
            })
            .Where(item => item.Constructor != null)
            .ToArray();

        for (var index = 0; index < commandTypes.Length; index++)
        {
            var commandType = commandTypes[index].CommandType;

            var interfaceType = commandTypes[index]
                .Constructor!
                .GetParameters()[0]
                .ParameterType;

            RegisterGeneratedCommand(
                commandType,
                interfaceType,
                index);
        }
    }

    private static void RegisterGeneratedCommand(
        Type commandType,
        Type interfaceType,
        int index)
    {
        var commandName = GetDependencyTypeName(commandType);
        var factory = CommandFactories.GetOrAdd(commandType,
            _ => new Lazy<Func<object[], object>>(() => CreateGeneratedCommandFactory(commandType, interfaceType, index))).Value;

        Register($"Commands.{commandName}", factory);
        Register($"Commands.{commandType.Name[..^"Command".Length]}", factory);
    }

    private static Func<object[], object> CreateGeneratedCommandFactory(Type commandType, Type interfaceType, int index)
    {
        var adapterName = GetAdapterDependencyName(interfaceType);
        var factoryTypeName = $"GeneratedCommandFactory{index}";

        var source = ReadTemplate(CommandTemplateResource)
            .Replace("{{FactoryTypeName}}", factoryTypeName)
            .Replace("{{CommandType}}", GetTypeName(commandType))
            .Replace("{{InterfaceType}}", GetTypeName(interfaceType))
            .Replace("{{AdapterDependencyName}}", adapterName);

        var generatedAssembly = CompileSource(
            source,
            commandType,
            interfaceType);

        var factoryType = generatedAssembly.GetTypes()
            .Single(type => type.Name == factoryTypeName);

        var createMethod = GetCreateMethod(factoryType);

        return createMethod.CreateDelegate<Func<object[], object>>();
    }

    private static string GetDependencyTypeName(Type type)
    {
        return (type.FullName ?? type.Name)
            .Replace('.', '_')
            .Replace('+', '_');
    }

    private static string GetAdapterDependencyName(Type interfaceType)
    {
        return "Adapter." +
               GetDependencyTypeName(interfaceType) +
               "Adapter";
    }

    private static void RegisterCommandFactory()
    {
        Register("Commands.Factory.Generated", MacroFactory.Value);
    }

    private static Func<object[], object> CreateCommandFactory()
    {
        var source = ReadTemplate(CommandFactoryTemplateResource);

        var generatedAssembly = CompileSource(
            source,
            typeof(ICommand));

        var factoryType = generatedAssembly.GetTypes()
            .Single(type =>
                type.Name == "GeneratedCommandFactory");

        var createMethod = GetCreateMethod(factoryType);

        return createMethod.CreateDelegate<Func<object[], object>>();
    }

    private static string GenerateAdapterSource(Type interfaceType)
    {
        var template = ReadTemplate(AdapterTemplateResource)
            .Replace(
                "{{InterfaceName}}",
                GetTypeName(interfaceType));

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

    private static MethodInfo GetCreateMethod(Type factoryType)
    {
        return factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Factory {factoryType.FullName} must declare a public static Create method.");
    }

    private static List<MetadataReference> GetPlatformReferences(string? trustedAssemblies)
    {
        if (trustedAssemblies == null)
        {
            throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
        }

        return trustedAssemblies.Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }

    private static string GeneratePropertySource(PropertyInfo property)
    {
        var typeName = GetTypeName(property.PropertyType);

        var setter = property.SetMethod == null
            ? string.Empty
            : $"set => _values[nameof({property.Name})] = value;";

        return $@"
        public {typeName} {property.Name}
        {{
            get => ({typeName})_values[nameof({property.Name})]!;
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
            var genericName =
                type.GetGenericTypeDefinition().FullName!;

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
               (type.FullName ??
                throw new InvalidOperationException(
                    $"Не удалось определить имя типа {type}."))
               .Replace('+', '.');
    }

    private static Assembly CompileSource(
        string source,
        params Type[] referenceTypes)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = GetPlatformReferences(
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));

        AddAssemblyReference(references, typeof(AutoRegistrar).Assembly);
        AddAssemblyReference(references, typeof(ICommand).Assembly);

        foreach (var referenceType in referenceTypes)
        {
            AddAssemblyReference(references, referenceType.Assembly);
        }

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
                result.Diagnostics.Where(
                    diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Error));

            throw new InvalidOperationException(
                "Ошибка компиляции сгенерированного кода:" +
                Environment.NewLine +
                errors);
        }

        stream.Position = 0;

        return Assembly.Load(stream.ToArray());
    }

    private static void AddAssemblyReference(
        ICollection<MetadataReference> references,
        Assembly assembly)
    {
        var assemblyLocation = assembly.Location;

        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            references.Add(
                MetadataReference.CreateFromFile(assemblyLocation));
        }
    }
}
