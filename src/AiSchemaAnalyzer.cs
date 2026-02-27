using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XsltSourceGenSample
{
    /// <summary>
    /// Uses the OpenAI Chat Completions API to perform a semantic analysis of the
    /// structural differences between two XSD schema versions.
    ///
    /// <para>
    /// The analyser sends a carefully-crafted prompt containing the schema diff
    /// (produced by <see cref="SchemaComparer"/>) to a GPT model and asks it to return
    /// a structured JSON object describing:
    /// <list type="bullet">
    ///   <item>A plain-English summary of the migration</item>
    ///   <item>Recommended default values for added elements</item>
    ///   <item>Potential migration risks</item>
    ///   <item>Explicit element-level mappings (renames, computations, conditionals)</item>
    ///   <item>High-level transformation patterns detected</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// When the OpenAI API is unavailable (missing key, network error, etc.) the analyser
    /// falls back gracefully and returns a minimal <see cref="AiSchemaAnalysis"/> with a
    /// note explaining that AI features were skipped.
    /// </para>
    /// </summary>
    public class AiSchemaAnalyzer
    {
        // The OpenAI Chat Completions endpoint.
        private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";

        // Default model – gpt-4o-mini offers the best cost/quality trade-off for
        // structured JSON extraction tasks as of 2024.
        private const string DefaultModel = "gpt-4o-mini";

        // Maximum tokens to allow in the model's response.
        // 1 000 tokens is generally enough for structured JSON with a few dozen mappings.
        private const int MaxResponseTokens = 1000;

        private readonly string? _apiKey;
        private readonly string  _model;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initialises the analyser.
        /// </summary>
        /// <param name="apiKey">
        /// OpenAI API key.  If null or empty, AI features are disabled and all calls
        /// return a fallback <see cref="AiSchemaAnalysis"/> without hitting the API.
        /// Pass <c>null</c> to allow the constructor to read
        /// <c>OPENAI_API_KEY</c> from environment variables automatically.
        /// </param>
        /// <param name="model">GPT model name to use (defaults to gpt-4o-mini).</param>
        public AiSchemaAnalyzer(string? apiKey = null, string model = DefaultModel)
        {
            // Allow the key to come from an environment variable when not supplied directly.
            _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            _model  = model;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Analyses a <see cref="SchemaChanges"/> object and returns an enriched
        /// <see cref="EnhancedSchemaChanges"/> that includes AI-generated insights.
        /// </summary>
        /// <param name="changes">The structural diff produced by <see cref="SchemaComparer"/>.</param>
        /// <param name="domainContext">
        /// Optional domain-specific context loaded from <c>domain-dsl-dictionary.json</c>.
        /// When provided this is appended to the prompt so the model can apply
        /// domain knowledge (e.g., knowing that "EffDt" is an abbreviation for "EffectiveDate").
        /// </param>
        /// <returns>An <see cref="EnhancedSchemaChanges"/> with an optional <see cref="AiSchemaAnalysis"/>.</returns>
        public async Task<EnhancedSchemaChanges> AnalyseAsync(
            SchemaChanges changes,
            string? domainContext = null)
        {
            // Copy the structural changes into the enhanced container.
            var enhanced = new EnhancedSchemaChanges
            {
                SourceVersion    = changes.SourceVersion,
                TargetVersion    = changes.TargetVersion,
                RootElementName  = changes.RootElementName,
                AddedElements    = changes.AddedElements,
                RemovedElements  = changes.RemovedElements,
                MovedElements    = changes.MovedElements,
                ReorderedElements = changes.ReorderedElements,
                RenamedElements  = changes.RenamedElements,
                UnchangedElements = changes.UnchangedElements
            };

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Console.WriteLine("[AiSchemaAnalyzer] No API key found – AI analysis skipped.");
                Console.WriteLine("[AiSchemaAnalyzer] Set the OPENAI_API_KEY environment variable to enable AI features.");
                enhanced.AiAnalysis = CreateFallbackAnalysis(changes);
                return enhanced;
            }

            Console.WriteLine($"[AiSchemaAnalyzer] Sending schema diff to OpenAI ({_model})…");

            try
            {
                enhanced.AiAnalysis = await CallOpenAiAsync(changes, domainContext);
                Console.WriteLine("[AiSchemaAnalyzer] AI analysis complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiSchemaAnalyzer] AI call failed: {ex.Message}");
                Console.WriteLine("[AiSchemaAnalyzer] Falling back to minimal analysis.");
                enhanced.AiAnalysis = CreateFallbackAnalysis(changes);
            }

            return enhanced;
        }

        // ====================================================================
        // Private methods
        // ====================================================================

        /// <summary>
        /// Builds the prompt, calls the OpenAI API, and parses the JSON response
        /// into an <see cref="AiSchemaAnalysis"/> object.
        /// </summary>
        private async Task<AiSchemaAnalysis> CallOpenAiAsync(
            SchemaChanges changes,
            string? domainContext)
        {
            // ------------------------------------------------------------------
            // Prompt engineering notes
            // ------------------------------------------------------------------
            // We use a two-message structure:
            //   1. SYSTEM message: establishes the role and response format.
            //   2. USER message:   contains the schema diff data.
            //
            // The system message instructs the model to:
            //   - Act as an XML schema migration expert
            //   - Return ONLY valid JSON (no markdown, no explanation outside the JSON)
            //   - Follow the exact JSON structure we defined in the schema
            //
            // The user message is generated by BuildUserPrompt(), which serialises
            // the SchemaChanges into a concise human-readable format so the model
            // can understand what changed between the two schema versions.
            // ------------------------------------------------------------------

            string systemPrompt = BuildSystemPrompt();
            string userPrompt   = BuildUserPrompt(changes, domainContext);

            // Construct the request payload.
            var requestPayload = new
            {
                model      = _model,
                max_tokens = MaxResponseTokens,
                messages   = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userPrompt   }
                },
                // Ask the model to return valid JSON (supported by GPT-4 and later).
                response_format = new { type = "json_object" }
            };

            string requestJson = JsonSerializer.Serialize(requestPayload);
            using var content  = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Attach the API key as a Bearer token.
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            HttpResponseMessage response = await _httpClient.PostAsync(OpenAiApiUrl, content);
            string responseBody          = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"OpenAI API returned {(int)response.StatusCode}: {responseBody}");
            }

            // Parse the outer ChatCompletion envelope to extract the inner JSON string.
            using var doc           = JsonDocument.Parse(responseBody);
            string innerJsonString  = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";

            // Deserialise the inner JSON into our AiSchemaAnalysis model.
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<AiSchemaAnalysis>(innerJsonString, options)
                   ?? new AiSchemaAnalysis { Summary = "Deserialisation returned null." };
        }

        /// <summary>
        /// Returns the system prompt that instructs the model on its role
        /// and the required JSON output format.
        /// </summary>
        private static string BuildSystemPrompt()
        {
            return """
                You are an expert XML schema migration analyst. Your job is to analyse the
                differences between two versions of an XML schema and produce a structured
                JSON analysis to guide XSLT transformation generation.

                You MUST respond with ONLY valid JSON that matches this exact structure:
                {
                  "summary": "string – plain-English summary of what changed",
                  "defaultValues": [
                    {
                      "elementPath": "string – XPath to the new element",
                      "value": "string – recommended default (literal or XPath expression)",
                      "reason": "string – why this default makes sense"
                    }
                  ],
                  "risks": [
                    {
                      "severity": "low|medium|high",
                      "description": "string – what the risk is",
                      "affectedElement": "string – element path(s) affected",
                      "mitigation": "string – how to mitigate"
                    }
                  ],
                  "mappings": [
                    {
                      "sourcePath": "string",
                      "targetPath": "string",
                      "mappingType": "direct|rename|computed|conditional|split|merge",
                      "transformExpression": "string or null – XPath expression when needed",
                      "notes": "string – explanation"
                    }
                  ],
                  "patterns": ["string", "..."]
                }

                Do NOT include any text outside the JSON. Do NOT use markdown code fences.
                """;
        }

        /// <summary>
        /// Builds the user prompt by serialising the <see cref="SchemaChanges"/> into
        /// a concise text representation that is easy for the model to process.
        /// Also appends domain context when provided.
        /// </summary>
        private static string BuildUserPrompt(SchemaChanges changes, string? domainContext)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Schema migration analysis request:");
            sb.AppendLine($"  Source version : {changes.SourceVersion}");
            sb.AppendLine($"  Target version : {changes.TargetVersion}");
            sb.AppendLine($"  Root element   : {changes.RootElementName}");
            sb.AppendLine();

            if (changes.AddedElements.Count > 0)
            {
                sb.AppendLine("ADDED ELEMENTS (exist in target, not in source):");
                foreach (var e in changes.AddedElements)
                    sb.AppendLine($"  + {e.Path} [{e.XsdType ?? "complex"}] required={e.IsRequired}");
                sb.AppendLine();
            }

            if (changes.RemovedElements.Count > 0)
            {
                sb.AppendLine("REMOVED ELEMENTS (exist in source, not in target):");
                foreach (var e in changes.RemovedElements)
                    sb.AppendLine($"  - {e.Path} [{e.XsdType ?? "complex"}]");
                sb.AppendLine();
            }

            if (changes.MovedElements.Count > 0)
            {
                sb.AppendLine("MOVED ELEMENTS (same name, different path):");
                foreach (var kv in changes.MovedElements)
                    sb.AppendLine($"  ~ {kv.Key}  →  {kv.Value}");
                sb.AppendLine();
            }

            if (changes.RenamedElements.Count > 0)
            {
                sb.AppendLine("RENAMED ELEMENTS (different name, same structure):");
                foreach (var kv in changes.RenamedElements)
                    sb.AppendLine($"  ≡ {kv.Key}  →  {kv.Value}");
                sb.AppendLine();
            }

            if (changes.ReorderedElements.Count > 0)
            {
                sb.AppendLine("REORDERED ELEMENTS (order changed within parent):");
                foreach (var e in changes.ReorderedElements)
                    sb.AppendLine($"  ⟳ {e.Path} (was order {e.Order})");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(domainContext))
            {
                sb.AppendLine("DOMAIN CONTEXT (use this to apply domain knowledge):");
                sb.AppendLine(domainContext);
                sb.AppendLine();
            }

            sb.AppendLine("Please analyse these changes and return the JSON analysis as specified.");

            return sb.ToString();
        }

        /// <summary>
        /// Creates a minimal <see cref="AiSchemaAnalysis"/> used when AI is unavailable.
        /// The fallback still generates direct-copy mappings for unchanged elements,
        /// providing a usable baseline even without AI.
        /// </summary>
        private static AiSchemaAnalysis CreateFallbackAnalysis(SchemaChanges changes)
        {
            var analysis = new AiSchemaAnalysis
            {
                Summary  = $"AI analysis unavailable. Structural diff: " +
                           $"{changes.AddedElements.Count} added, " +
                           $"{changes.RemovedElements.Count} removed, " +
                           $"{changes.MovedElements.Count} moved, " +
                           $"{changes.RenamedElements.Count} renamed.",
                Patterns = new List<string> { "structural-migration" }
            };

            // Generate simple "direct" mappings for unchanged elements.
            foreach (var element in changes.UnchangedElements)
            {
                analysis.Mappings.Add(new ElementMapping
                {
                    SourcePath  = element.Path,
                    TargetPath  = element.Path,
                    MappingType = "direct",
                    Notes       = "Element unchanged – direct copy."
                });
            }

            // Generate "rename" mappings for detected renames.
            foreach (var kv in changes.RenamedElements)
            {
                analysis.Mappings.Add(new ElementMapping
                {
                    SourcePath  = kv.Key,
                    TargetPath  = kv.Value,
                    MappingType = "rename",
                    Notes       = "Element renamed – value copied from old path."
                });
            }

            // Flag added required elements as medium risk (they need defaults).
            foreach (var element in changes.AddedElements)
            {
                if (element.IsRequired)
                {
                    analysis.Risks.Add(new MigrationRisk
                    {
                        Severity        = "medium",
                        Description     = $"New required element '{element.Path}' has no source value.",
                        AffectedElement = element.Path,
                        Mitigation      = "Provide a default value or derive it from existing source data."
                    });

                    analysis.DefaultValues.Add(new DefaultValue
                    {
                        ElementPath = element.Path,
                        Value       = "''",
                        Reason      = "Placeholder – replace with an appropriate default."
                    });
                }
            }

            return analysis;
        }
    }
}
