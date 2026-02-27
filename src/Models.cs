using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XsltSourceGenSample
{
    /// <summary>
    /// Represents information about a single XML element extracted from an XSD schema.
    /// This is the core data structure used when comparing two versions of a schema.
    /// </summary>
    public class ElementInfo
    {
        /// <summary>
        /// The local name of the element (e.g., "PolicyNumber", "EffectiveDate").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The full XPath-like path from the root element to this element
        /// (e.g., "Policy/Insured/FirstName"). Used to detect moved elements.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The XSD type of this element (e.g., "xs:string", "xs:date", "xs:decimal").
        /// Null when the element has a complex type (i.e., it is a container element).
        /// </summary>
        public string? XsdType { get; set; }

        /// <summary>
        /// Whether this element contains child elements (is a complex type).
        /// </summary>
        public bool IsComplex { get; set; }

        /// <summary>
        /// The zero-based position of this element within its parent's sequence.
        /// Used to detect reordering of sibling elements.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Whether this element is required (minOccurs >= 1) in the schema.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Maximum number of times this element may appear (0 = unbounded).
        /// </summary>
        public int MaxOccurs { get; set; } = 1;

        /// <summary>
        /// Any documentation annotation attached to this element in the XSD.
        /// </summary>
        public string? Documentation { get; set; }
    }

    /// <summary>
    /// Represents the structural differences between two versions of an XML schema.
    /// Produced by <see cref="SchemaComparer"/> and consumed by the T4 templates.
    /// </summary>
    public class SchemaChanges
    {
        /// <summary>
        /// Source schema version label (e.g., "v1.0").
        /// </summary>
        public string SourceVersion { get; set; } = string.Empty;

        /// <summary>
        /// Target schema version label (e.g., "v2.0").
        /// </summary>
        public string TargetVersion { get; set; } = string.Empty;

        /// <summary>
        /// The name of the root element shared by both schemas.
        /// </summary>
        public string RootElementName { get; set; } = string.Empty;

        /// <summary>
        /// Elements that exist in the target schema but not in the source schema.
        /// These will need default values or mapping logic in the generated XSLT.
        /// </summary>
        public List<ElementInfo> AddedElements { get; set; } = new();

        /// <summary>
        /// Elements that exist in the source schema but not in the target schema.
        /// These elements should be omitted from the XSLT output.
        /// </summary>
        public List<ElementInfo> RemovedElements { get; set; } = new();

        /// <summary>
        /// Elements that exist in both schemas with the same name but at different paths.
        /// Key = path in source schema, Value = path in target schema.
        /// </summary>
        public Dictionary<string, string> MovedElements { get; set; } = new();

        /// <summary>
        /// Elements that exist in both schemas at the same path but in a different order.
        /// These require explicit ordering logic in the XSLT.
        /// </summary>
        public List<ElementInfo> ReorderedElements { get; set; } = new();

        /// <summary>
        /// Elements whose names have changed between versions (requires rename mapping).
        /// Key = old name/path in source, Value = new name/path in target.
        /// </summary>
        public Dictionary<string, string> RenamedElements { get; set; } = new();

        /// <summary>
        /// Elements present in both schemas that require a direct copy (no transformation needed).
        /// </summary>
        public List<ElementInfo> UnchangedElements { get; set; } = new();
    }

    /// <summary>
    /// Extends <see cref="SchemaChanges"/> with AI-generated semantic analysis.
    /// Produced by <see cref="AiSchemaAnalyzer"/> when an OpenAI key is available.
    /// </summary>
    public class EnhancedSchemaChanges : SchemaChanges
    {
        /// <summary>
        /// The full AI analysis result, containing default values, risk flags, and comments.
        /// May be null when AI analysis is unavailable or disabled.
        /// </summary>
        public AiSchemaAnalysis? AiAnalysis { get; set; }
    }

    // -------------------------------------------------------------------------
    // AI Analysis models (populated from OpenAI JSON response)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Top-level container for the AI-generated schema analysis.
    /// Deserialised from the JSON response returned by the OpenAI Chat API.
    /// </summary>
    public class AiSchemaAnalysis
    {
        /// <summary>
        /// Overall narrative summary of what changed between the two schema versions.
        /// Written in plain English and suitable for a code comment in the generated XSLT.
        /// </summary>
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Suggested default values for newly-added elements that have no obvious source value.
        /// </summary>
        [JsonPropertyName("defaultValues")]
        public List<DefaultValue> DefaultValues { get; set; } = new();

        /// <summary>
        /// Potential risks or data-loss scenarios the AI identified in the migration.
        /// </summary>
        [JsonPropertyName("risks")]
        public List<MigrationRisk> Risks { get; set; } = new();

        /// <summary>
        /// Explicit element-level mappings suggested by the AI (e.g., renames, computed fields).
        /// </summary>
        [JsonPropertyName("mappings")]
        public List<ElementMapping> Mappings { get; set; } = new();

        /// <summary>
        /// Broader transformation patterns the AI detected across the schema changes.
        /// Examples: "field consolidation", "namespace restructure", "type widening".
        /// </summary>
        [JsonPropertyName("patterns")]
        public List<string> Patterns { get; set; } = new();
    }

    /// <summary>
    /// Represents a recommended default value for a newly-added element.
    /// </summary>
    public class DefaultValue
    {
        /// <summary>
        /// XPath path to the element in the target schema.
        /// </summary>
        [JsonPropertyName("elementPath")]
        public string ElementPath { get; set; } = string.Empty;

        /// <summary>
        /// The suggested default value (as a string literal or XPath expression).
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable explanation of why this default was chosen.
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a potential risk or issue identified by the AI during schema analysis.
    /// </summary>
    public class MigrationRisk
    {
        /// <summary>
        /// Severity level: "low", "medium", or "high".
        /// </summary>
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "low";

        /// <summary>
        /// Short description of the risk.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The element path(s) affected by this risk.
        /// </summary>
        [JsonPropertyName("affectedElement")]
        public string AffectedElement { get; set; } = string.Empty;

        /// <summary>
        /// AI-suggested mitigation strategy.
        /// </summary>
        [JsonPropertyName("mitigation")]
        public string Mitigation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Describes a specific element mapping suggested by the AI
    /// (e.g., a rename, a computed value, or a conditional transformation).
    /// </summary>
    public class ElementMapping
    {
        /// <summary>
        /// Path of the element in the source schema.
        /// </summary>
        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Path of the element in the target schema.
        /// </summary>
        [JsonPropertyName("targetPath")]
        public string TargetPath { get; set; } = string.Empty;

        /// <summary>
        /// Type of mapping: "direct", "rename", "computed", "conditional", "split", "merge".
        /// </summary>
        [JsonPropertyName("mappingType")]
        public string MappingType { get; set; } = "direct";

        /// <summary>
        /// Optional XPath expression used when MappingType is "computed" or "conditional".
        /// </summary>
        [JsonPropertyName("transformExpression")]
        public string? TransformExpression { get; set; }

        /// <summary>
        /// Human-readable explanation of the mapping logic.
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }
}
