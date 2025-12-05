using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Dynamics.AX.Metadata.Providers;
using Microsoft.Dynamics.AX.Metadata.Storage;
using Microsoft.Dynamics.AX.Metadata.Storage.Runtime;
using Waywo.DbSchema.Providers;

namespace Waywo.DbSchema.ConsoleApp
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            if (!ConsoleOptions.TryParse(args, out var options, out var error))
            {
                PrintUsage(error);
                return 1;
            }

            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            if (options.AddFromModel && string.IsNullOrWhiteSpace(options.Model))
            {
                PrintUsage("--add-from-model requiere especificar --model.");
                return 1;
            }

            if (!options.HasTableSelection())
            {
                PrintUsage("Debe especificar al menos una tabla, --add-related <tabla> o --add-from-model.");
                return 1;
            }

            var metadataDirectory = ResolveMetadataDirectory(options.MetadataDirectory);
            if (string.IsNullOrWhiteSpace(metadataDirectory))
            {
                PrintUsage("Debe indicar la ruta de metadatos con --metadata o la variable D365FO_METADATA_DIRECTORY.");
                return 1;
            }

            try
            {
                var metadataProvider = RuntimeDataModelProviderFactory.CreateMetadataProvider(metadataDirectory);
                var dataProvider = new D365FODataModelProvider(metadataProvider)
                {
                    SimplifyTypes = options.SimplifyTypes,
                    IgnoreStaging = options.IgnoreStaging,
                    IgnoreSelfReferences = options.IgnoreSelfReferences,
                    Model = options.Model
                };

                var dbmlProvider = new DBMLSchemaProvider(dataProvider)
                {
                    StandardFields = options.IncludeNonKeyFields,
                    ExtensionFields = options.IncludeExtensions,
                    MarkMandatory = options.MarkMandatory
                };

                ApplyTables(metadataProvider, dataProvider, options);

                var schema = dbmlProvider.GetSchema();

                if (string.IsNullOrWhiteSpace(options.OutputPath))
                {
                    System.Console.WriteLine(schema);
                }
                else
                {
                    var directory = Path.GetDirectoryName(options.OutputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(options.OutputPath, schema);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error generando DBML: {ex.Message}");
                return 1;
            }
        }

        private static void ApplyTables(IMetadataProvider metadataProvider, IDataModelProvider dataProvider, ConsoleOptions options)
        {
            var candidateTables = GetCandidateTables(metadataProvider, options.Model);
            var requestedTables = ExpandTables(candidateTables, options.Tables);

            foreach (var table in requestedTables)
            {
                dataProvider.AddTable(table);
            }

            var includeModelTables = options.AddFromModel || (!requestedTables.Any() && !string.IsNullOrEmpty(options.Model));
            if (includeModelTables && !string.IsNullOrEmpty(options.Model))
            {
                dataProvider.AddTablesFromModel(options.Model);
            }

            if (!string.IsNullOrWhiteSpace(options.AddInwardFor))
            {
                var inwardTargets = ExpandTables(candidateTables, new[] { options.AddInwardFor });
                foreach (var inward in inwardTargets)
                {
                    dataProvider.AddTable(inward);
                    dataProvider.AddInwardTables(inward);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.AddOutwardFor))
            {
                var outwardTargets = ExpandTables(candidateTables, new[] { options.AddOutwardFor });
                foreach (var outward in outwardTargets)
                {
                    dataProvider.AddTable(outward);
                    dataProvider.AddOutwardTables(outward);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.AddRelatedFor))
            {
                var relatedTargets = ExpandTables(candidateTables, new[] { options.AddRelatedFor });
                foreach (var related in relatedTargets)
                {
                    dataProvider.AddTable(related);
                }
            }

            if (options.AddRelated)
            {
                dataProvider.AddRelatedTables();
            }
        }

        private static IEnumerable<string> GetCandidateTables(IMetadataProvider metadataProvider, string? model)
        {
            IEnumerable<string> tableNames = string.IsNullOrWhiteSpace(model)
                ? metadataProvider.Tables.GetPrimaryKeys()
                : metadataProvider.Tables.ListObjectsForModel(model);

            return tableNames ?? Enumerable.Empty<string>();
        }

        private static IEnumerable<string> ExpandTables(IEnumerable<string> available, IEnumerable<string> requested)
        {
            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var availableList = available.ToList();

            foreach (var pattern in requested)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (pattern.Contains("*"))
                {
                    var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
                    foreach (var candidate in availableList.Where(c => regex.IsMatch(c)))
                    {
                        matches.Add(candidate);
                    }
                }
                else
                {
                    matches.Add(pattern);
                }
            }

            return matches;
        }

        private static string? ResolveMetadataDirectory(string? metadataDirectory)
        {
            if (!string.IsNullOrWhiteSpace(metadataDirectory))
            {
                return metadataDirectory;
            }

            var envMetadata = Environment.GetEnvironmentVariable("D365FO_METADATA_DIRECTORY");
            return string.IsNullOrWhiteSpace(envMetadata) ? null : envMetadata;
        }

        private static void PrintUsage(string? error = null)
        {
            if (!string.IsNullOrEmpty(error))
            {
                System.Console.Error.WriteLine(error);
                System.Console.Error.WriteLine();
            }

            System.Console.WriteLine("Uso: Waywo.DbSchema.Console [opciones]\n");
            System.Console.WriteLine("Parámetros principales:");
            System.Console.WriteLine("  --metadata <ruta>                 Ruta al directorio de metadatos (o variable D365FO_METADATA_DIRECTORY).");
            System.Console.WriteLine("  --model <nombre>                  Modelo contenedor de las tablas.");
            System.Console.WriteLine("  --table <tabla1,tabla2>           Tablas separadas por coma; admite wildcard '*'.");
            System.Console.WriteLine("  --add-from-model                  Agrega todas las tablas del modelo indicado.");
            System.Console.WriteLine("  --add-inward <tabla>              Agrega tablas con relaciones entrantes.");
            System.Console.WriteLine("  --add-outward <tabla>             Agrega tablas con relaciones salientes.");
            System.Console.WriteLine("  --add-related [tabla]             Agrega tablas relacionadas de la selección o de la tabla indicada.");
            System.Console.WriteLine("  --include-non-keyfields           Incluye todos los campos, no solo las claves.");
            System.Console.WriteLine("  --include-extensions              Incluye campos de extensiones.");
            System.Console.WriteLine("  --mark-mandatory                  Marca campos obligatorios como 'not null'.");
            System.Console.WriteLine("  --simplify-types                  Convierte EDT a tipos simples.");
            System.Console.WriteLine("  --ignore-staging                  Ignora tablas de staging.");
            System.Console.WriteLine("  --ignore-self-references          Ignora relaciones de una tabla consigo misma.");
            System.Console.WriteLine("  --output <ruta>                   Archivo de salida; si se omite escribe a stdout.");
            System.Console.WriteLine("  --help                            Muestra esta ayuda.");
        }
    }

    internal sealed class ConsoleOptions
    {
        public string? MetadataDirectory { get; private set; }
        public string? Model { get; private set; }
        public string? OutputPath { get; private set; }
        public bool IncludeNonKeyFields { get; private set; }
        public bool IncludeExtensions { get; private set; }
        public bool MarkMandatory { get; private set; }
        public bool SimplifyTypes { get; private set; }
        public bool IgnoreStaging { get; private set; }
        public bool IgnoreSelfReferences { get; private set; }
        public bool AddFromModel { get; private set; }
        public bool AddRelated { get; private set; }
        public bool ShowHelp { get; private set; }

        public string? AddInwardFor { get; private set; }
        public string? AddOutwardFor { get; private set; }
        public string? AddRelatedFor { get; private set; }

        public List<string> Tables { get; } = new List<string>();

        public bool HasTableSelection()
        {
            return Tables.Any()
                || !string.IsNullOrWhiteSpace(AddInwardFor)
                || !string.IsNullOrWhiteSpace(AddOutwardFor)
                || !string.IsNullOrWhiteSpace(AddRelatedFor)
                || AddFromModel
                || !string.IsNullOrWhiteSpace(Model);
        }

        public static bool TryParse(string[] args, out ConsoleOptions options, out string? error)
        {
            options = new ConsoleOptions();
            error = null;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--help":
                        options.ShowHelp = true;
                        break;
                    case "--metadata":
                    case "--metadata-directory":
                        options.MetadataDirectory = ReadNext(args, ref i, "--metadata requiere una ruta", ref error);
                        break;
                    case "--model":
                        options.Model = ReadNext(args, ref i, "--model requiere un nombre", ref error);
                        break;
                    case "--table":
                        var tables = ReadNext(args, ref i, "--table requiere al menos un nombre", ref error);
                        if (tables != null)
                        {
                            options.Tables.AddRange(tables.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)));
                        }
                        break;
                    case "--add-from-model":
                        options.AddFromModel = true;
                        break;
                    case "--add-inward":
                        options.AddInwardFor = ReadNext(args, ref i, "--add-inward requiere un nombre de tabla", ref error);
                        break;
                    case "--add-outward":
                        options.AddOutwardFor = ReadNext(args, ref i, "--add-outward requiere un nombre de tabla", ref error);
                        break;
                    case "--add-related":
                        options.AddRelated = true;
                        var related = PeekNext(args, i);
                        if (!string.IsNullOrWhiteSpace(related) && !related.StartsWith("--"))
                        {
                            i++;
                            options.AddRelatedFor = related;
                        }
                        break;
                    case "--include-non-keyfields":
                        options.IncludeNonKeyFields = true;
                        break;
                    case "--include-extensions":
                        options.IncludeExtensions = true;
                        break;
                    case "--mark-mandatory":
                        options.MarkMandatory = true;
                        break;
                    case "--simplify-types":
                        options.SimplifyTypes = true;
                        break;
                    case "--ignore-staging":
                        options.IgnoreStaging = true;
                        break;
                    case "--ignore-self-references":
                        options.IgnoreSelfReferences = true;
                        break;
                    case "--output":
                        options.OutputPath = ReadNext(args, ref i, "--output requiere una ruta", ref error);
                        break;
                    default:
                        error = $"Parámetro no reconocido: {arg}";
                        break;
                }

                if (error != null)
                {
                    return false;
                }
            }

            if (options.AddRelatedFor != null)
            {
                options.AddRelated = true;
            }

            return true;
        }

        private static string? ReadNext(string[] args, ref int index, string errorMessage, ref string? error)
        {
            if (index + 1 >= args.Length)
            {
                error = errorMessage;
                return null;
            }

            return args[++index];
        }

        private static string? PeekNext(string[] args, int index)
        {
            return index + 1 < args.Length ? args[index + 1] : null;
        }
    }

    internal static class RuntimeDataModelProviderFactory
    {
        public static IMetadataProvider CreateMetadataProvider(string metadataDirectory)
        {
            if (string.IsNullOrWhiteSpace(metadataDirectory))
            {
                throw new ArgumentException("La ruta de metadatos no puede ser nula", nameof(metadataDirectory));
            }

            var configuration = new MetadataRuntimeProviderConfiguration
            {
                MetadataDirectory = metadataDirectory
            };

            TryAttachManifest(configuration, metadataDirectory);

            var providerFactory = new MetadataProviderFactory();
            var createMethod = providerFactory.GetType().GetMethod("CreateRuntimeProvider", new[] { configuration.GetType() })
                               ?? providerFactory.GetType().GetMethod("CreateProvider", new[] { configuration.GetType() });

            if (createMethod == null)
            {
                throw new InvalidOperationException("No se pudo encontrar el método para crear el proveedor de metadatos.");
            }

            var metadataProvider = createMethod.Invoke(providerFactory, new object[] { configuration }) as IMetadataProvider;
            if (metadataProvider == null)
            {
                throw new InvalidOperationException("No se pudo inicializar el proveedor de metadatos.");
            }

            return metadataProvider;
        }

        private static void TryAttachManifest(MetadataRuntimeProviderConfiguration configuration, string metadataDirectory)
        {
            var manifestPath = Path.Combine(metadataDirectory, "ModelManifest.xml");
            var manifestProperty = configuration.GetType().GetProperty("ModelManifest");
            if (manifestProperty == null || !File.Exists(manifestPath))
            {
                return;
            }

            var manifestType = manifestProperty.PropertyType;
            var manifest = InvokeManifestLoader(manifestType, manifestPath) ?? InvokeManifestLoader(manifestType, metadataDirectory);
            if (manifest != null)
            {
                manifestProperty.SetValue(configuration, manifest);
            }
        }

        private static object? InvokeManifestLoader(Type manifestType, string path)
        {
            var loadMethods = new[] { "LoadFromFile", "LoadFromDirectory", "FromFile", "FromPath" };
            foreach (var methodName in loadMethods)
            {
                var method = manifestType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (method != null)
                {
                    return method.Invoke(null, new object[] { path });
                }
            }

            return null;
        }
    }
}
