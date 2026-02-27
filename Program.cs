using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace XsltSourceGenSample
{
    /// <summary>
    /// Entry point for the XsltSourceGenSample application.
    ///
    /// <para>
    /// This program demonstrates two modes:
    /// <list type="number">
    ///   <item>
    ///     <term>basic</term>
    ///     <description>
    ///       Runs <see cref="SchemaComparer"/> and prints a summary of detected changes.
    ///       Use this to understand schema comparison without AI or T4 templates.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>ai</term>
    ///     <description>
    ///       Runs <see cref="AiSchemaAnalyzer"/> in addition to schema comparison.
    ///       Requires the <c>OPENAI_API_KEY</c> environment variable to be set.
    ///       Falls back gracefully when the key is absent.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The T4 templates (<c>BasicXsltGenerator.tt</c> and <c>AiEnhancedXsltGenerator.tt</c>)
    /// are not invoked here – they are processed by the T4 engine (Visual Studio or
    /// the <c>dotnet-t4</c> CLI tool) and produce the XSLT files in <c>examples/output/</c>.
    /// </para>
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=======================================================");
            Console.WriteLine(" XSLT Source Generation Sample");
            Console.WriteLine(" C# + T4 Templates + OpenAI");
            Console.WriteLine("=======================================================");
            Console.WriteLine();

            // Resolve paths relative to the project root.
            // When running with `dotnet run` the working directory is the project root.
            string projectRoot    = AppContext.BaseDirectory;
            string sourceXsdPath  = FindFile(projectRoot, "examples/schemas/v1.0.xsd");
            string targetXsdPath  = FindFile(projectRoot, "examples/schemas/v2.0.xsd");
            string domainDictPath = FindFile(projectRoot, "config/domain-dsl-dictionary.json");

            // Validate that the example schema files exist.
            if (!File.Exists(sourceXsdPath) || !File.Exists(targetXsdPath))
            {
                Console.Error.WriteLine($"ERROR: Example schema files not found.");
                Console.Error.WriteLine($"  Expected: {sourceXsdPath}");
                Console.Error.WriteLine($"  Expected: {targetXsdPath}");
                Console.Error.WriteLine("Make sure you are running from the repository root.");
                return 1;
            }

            // Parse the mode argument (default to "basic").
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "basic";

            switch (mode)
            {
                case "basic":
                    RunBasicComparison(sourceXsdPath, targetXsdPath);
                    break;

                case "ai":
                    await RunAiEnhancedAnalysisAsync(sourceXsdPath, targetXsdPath, domainDictPath);
                    break;

                case "help":
                case "--help":
                case "-h":
                    PrintHelp();
                    break;

                default:
                    Console.Error.WriteLine($"Unknown mode: '{mode}'. Use 'basic', 'ai', or 'help'.");
                    return 1;
            }

            return 0;
        }

        /// <summary>
        /// Runs a basic schema comparison and prints a human-readable summary.
        /// No AI API calls are made.
        /// </summary>
        private static void RunBasicComparison(string sourceXsdPath, string targetXsdPath)
        {
            Console.WriteLine("MODE: Basic Schema Comparison (no AI)");
            Console.WriteLine("--------------------------------------");
            Console.WriteLine();

            var comparer = new SchemaComparer();
            var changes  = comparer.Compare(
                sourceXsdPath,
                targetXsdPath,
                sourceVersion: "v1.0",
                targetVersion: "v2.0");

            PrintChangesSummary(changes);

            Console.WriteLine();
            Console.WriteLine("TIP: Run with 'ai' argument to include OpenAI analysis:");
            Console.WriteLine("     dotnet run -- ai");
            Console.WriteLine();
            Console.WriteLine("TIP: To generate XSLT files, run the T4 templates:");
            Console.WriteLine("     dotnet tool install -g dotnet-t4");
            Console.WriteLine("     t4 templates/BasicXsltGenerator.tt -o examples/output/Transform_v1_to_v2_basic.xslt");
        }

        /// <summary>
        /// Runs schema comparison followed by AI-enhanced analysis.
        /// Requires <c>OPENAI_API_KEY</c> environment variable for live AI calls;
        /// falls back to structural analysis if the key is absent.
        /// </summary>
        private static async Task RunAiEnhancedAnalysisAsync(
            string sourceXsdPath,
            string targetXsdPath,
            string domainDictPath)
        {
            Console.WriteLine("MODE: AI-Enhanced Schema Analysis");
            Console.WriteLine("----------------------------------");
            Console.WriteLine();

            // Load optional domain context.
            string? domainContext = null;
            if (File.Exists(domainDictPath))
            {
                domainContext = await File.ReadAllTextAsync(domainDictPath);
                Console.WriteLine($"[Program] Loaded domain context from: {domainDictPath}");
            }

            // Step 1: Structural comparison.
            var comparer = new SchemaComparer();
            var changes  = comparer.Compare(
                sourceXsdPath,
                targetXsdPath,
                sourceVersion: "v1.0",
                targetVersion: "v2.0");

            PrintChangesSummary(changes);

            // Step 2: AI analysis.
            Console.WriteLine();
            Console.WriteLine("Running AI analysis…");
            Console.WriteLine();

            var analyser = new AiSchemaAnalyzer();
            var enhanced = await analyser.AnalyseAsync(changes, domainContext);

            PrintAiAnalysis(enhanced.AiAnalysis);

            Console.WriteLine();
            Console.WriteLine("TIP: To generate the AI-enhanced XSLT file, run:");
            Console.WriteLine("     t4 templates/AiEnhancedXsltGenerator.tt -o examples/output/Transform_v1_to_v2_ai_enhanced.xslt");
        }

        // ====================================================================
        // Console output helpers
        // ====================================================================

        private static void PrintChangesSummary(SchemaChanges changes)
        {
            Console.WriteLine($"Schema Comparison: {changes.SourceVersion} → {changes.TargetVersion}");
            Console.WriteLine($"Root element: {changes.RootElementName}");
            Console.WriteLine();

            PrintSection("ADDED ELEMENTS", changes.AddedElements.Count, () =>
            {
                foreach (var e in changes.AddedElements)
                    Console.WriteLine($"  + {e.Path} [{e.XsdType ?? "complex"}]{(e.IsRequired ? " (required)" : "")}");
            });

            PrintSection("REMOVED ELEMENTS", changes.RemovedElements.Count, () =>
            {
                foreach (var e in changes.RemovedElements)
                    Console.WriteLine($"  - {e.Path} [{e.XsdType ?? "complex"}]");
            });

            PrintSection("RENAMED ELEMENTS", changes.RenamedElements.Count, () =>
            {
                foreach (var kv in changes.RenamedElements)
                    Console.WriteLine($"  ≡ {kv.Key}  →  {kv.Value}");
            });

            PrintSection("MOVED ELEMENTS", changes.MovedElements.Count, () =>
            {
                foreach (var kv in changes.MovedElements)
                    Console.WriteLine($"  ~ {kv.Key}  →  {kv.Value}");
            });

            PrintSection("REORDERED ELEMENTS", changes.ReorderedElements.Count, () =>
            {
                foreach (var e in changes.ReorderedElements)
                    Console.WriteLine($"  ⟳ {e.Path} (was order {e.Order})");
            });

            Console.WriteLine($"Unchanged elements: {changes.UnchangedElements.Count}");
        }

        private static void PrintSection(string title, int count, Action printItems)
        {
            if (count == 0)
            {
                Console.WriteLine($"{title}: (none)");
                return;
            }

            Console.WriteLine($"{title} ({count}):");
            printItems();
            Console.WriteLine();
        }

        private static void PrintAiAnalysis(AiSchemaAnalysis? analysis)
        {
            if (analysis == null)
            {
                Console.WriteLine("AI analysis: not available.");
                return;
            }

            Console.WriteLine("AI ANALYSIS RESULTS");
            Console.WriteLine("-------------------");
            Console.WriteLine($"Summary: {analysis.Summary}");
            Console.WriteLine();

            if (analysis.Patterns.Count > 0)
            {
                Console.WriteLine($"Detected patterns: {string.Join(", ", analysis.Patterns)}");
                Console.WriteLine();
            }

            if (analysis.DefaultValues.Count > 0)
            {
                Console.WriteLine($"Suggested defaults ({analysis.DefaultValues.Count}):");
                foreach (var dv in analysis.DefaultValues)
                    Console.WriteLine($"  {dv.ElementPath} = '{dv.Value}'  [{dv.Reason}]");
                Console.WriteLine();
            }

            if (analysis.Risks.Count > 0)
            {
                Console.WriteLine($"Migration risks ({analysis.Risks.Count}):");
                foreach (var risk in analysis.Risks)
                    Console.WriteLine($"  [{risk.Severity.ToUpper()}] {risk.AffectedElement}: {risk.Description}");
                Console.WriteLine();
            }

            if (analysis.Mappings.Count > 0)
            {
                Console.WriteLine($"Element mappings ({analysis.Mappings.Count}):");
                foreach (var m in analysis.Mappings)
                    Console.WriteLine($"  {m.SourcePath} → {m.TargetPath} [{m.MappingType}]");
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage: dotnet run -- [mode]");
            Console.WriteLine();
            Console.WriteLine("Modes:");
            Console.WriteLine("  basic   Compare schemas and print structural diff (default)");
            Console.WriteLine("  ai      Compare schemas + run AI-enhanced analysis");
            Console.WriteLine("  help    Show this help message");
            Console.WriteLine();
            Console.WriteLine("Environment variables:");
            Console.WriteLine("  OPENAI_API_KEY   OpenAI API key (required for 'ai' mode)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run");
            Console.WriteLine("  dotnet run -- basic");
            Console.WriteLine("  OPENAI_API_KEY=sk-... dotnet run -- ai");
        }

        /// <summary>
        /// Locates a file by trying multiple base paths.
        /// This handles the difference between running from the project root
        /// and running from the bin/Debug/net8.0 output directory.
        /// </summary>
        private static string FindFile(string baseDir, string relativePath)
        {
            // Try from the base directory first.
            string candidate = Path.Combine(baseDir, relativePath);
            if (File.Exists(candidate)) return candidate;

            // Walk up the directory tree looking for the file (handles bin/Debug/net8.0 case).
            string? dir = baseDir;
            for (int i = 0; i < 5 && dir != null; i++)
            {
                candidate = Path.Combine(dir, relativePath);
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }

            // Return the original path even if not found (caller validates existence).
            return Path.Combine(baseDir, relativePath);
        }
    }
}
