# XSLT Source Generation Sample

> **A sample application demonstrating how to use C# T4 templates and OpenAI to automatically generate XSLT transformations for XML schema migrations.**

This repository is intended as **educational reference material** – a starting point for building production-ready schema migration pipelines. Every file is extensively commented to explain the *why*, not just the *what*.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Getting Started](#getting-started)
- [How It Works](#how-it-works)
- [Customization Guide](#customization-guide)
- [Sample Output](#sample-output)
- [Limitations & Next Steps](#limitations--next-steps)
- [References](#references)

---

## Overview

### What this sample demonstrates

When an XML schema evolves (v1.0 → v2.0 → v3.0 …), every system that exchanges documents in that format needs an **XSLT transformation** to convert older documents to the newer format. Writing these transformations by hand is tedious, error-prone, and doesn't scale well for schemas with hundreds of elements.

This sample shows how to:

1. **Parse and compare two XSD schemas** programmatically to detect what changed.
2. **Feed the diff to OpenAI** to get semantic understanding of the changes (not just structural).
3. **Use T4 templates** to generate a correct, documented XSLT stylesheet automatically.

### Use cases

- **n+1 XML schema versioning** – generate transformations for every consecutive version pair automatically.
- **Schema migration documentation** – the AI analysis produces human-readable explanations of each change.
- **Migration risk assessment** – the AI identifies elements that may cause data loss or require special handling.
- **Code generation pipelines** – demonstrates how to integrate AI into a build/generation pipeline.

### Key technologies

| Technology | Role |
|---|---|
| **C# / .NET 8** | Core library and program entry point |
| **T4 Templates** | Text generation engine for producing XSLT |
| **XML Schema (XSD)** | Schema definition format |
| **XSLT 1.0** | The generated transformation language |
| **OpenAI Chat API** | Semantic analysis of schema changes |
| **System.Xml.Schema** | XSD parsing and navigation |
| **System.Text.Json** | JSON serialisation for AI responses |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    XSLT Source Gen Pipeline                      │
│                                                                  │
│  ┌──────────┐    ┌──────────────────┐    ┌───────────────────┐  │
│  │ v1.0.xsd │───▶│                  │    │  BasicXslt        │  │
│  └──────────┘    │  SchemaComparer  │───▶│  Generator.tt     │──┼──▶ basic.xslt
│                  │                  │    └───────────────────┘  │
│  ┌──────────┐    │  (structural     │                            │
│  │ v2.0.xsd │───▶│   diff)          │    ┌───────────────────┐  │
│  └──────────┘    │                  │    │  AiEnhanced       │  │
│                  └────────┬─────────┘    │  XsltGenerator.tt │──┼──▶ ai_enhanced.xslt
│                           │              └─────────▲─────────┘  │
│                           │                        │             │
│                           ▼                        │             │
│                  ┌──────────────────┐              │             │
│                  │  AiSchema        │──────────────┘             │
│  ┌──────────┐    │  Analyzer        │                            │
│  │ OpenAI   │◀──▶│                  │                            │
│  │ Chat API │    │  (semantic       │                            │
│  └──────────┘    │   analysis)      │                            │
│                  └──────────────────┘                            │
│                           ▲                                      │
│  ┌──────────────────────┐ │                                      │
│  │ domain-dsl-dict.json │─┘                                      │
│  └──────────────────────┘                                        │
└─────────────────────────────────────────────────────────────────┘
```

### Component summary

| Component | File | Purpose |
|---|---|---|
| `SchemaComparer` | `src/SchemaComparer.cs` | Parses two XSD files; detects added, removed, moved, renamed, and reordered elements |
| `AiSchemaAnalyzer` | `src/AiSchemaAnalyzer.cs` | Sends the diff to OpenAI; returns structured `AiSchemaAnalysis` |
| Models | `src/Models.cs` | Data classes used throughout the pipeline |
| `BasicXsltGenerator.tt` | `templates/` | T4 template; generates XSLT using structural diff only |
| `AiEnhancedXsltGenerator.tt` | `templates/` | T4 template; generates XSLT enriched with AI insights |
| Domain dictionary | `config/domain-dsl-dictionary.json` | Domain context fed to the AI prompt |
| AI config | `config/ai-config.json` | Model settings, feature toggles, cost controls |
| Example schemas | `examples/schemas/` | v1.0 and v2.0 insurance policy XSD files |
| Example XML | `examples/schemas/sample-v1.xml` | Sample document for testing the transformation |
| Example output | `examples/output/` | Pre-generated XSLT files checked in for reference |

### Workflow

```
Schema files (XSD)
      │
      ▼
 SchemaComparer
      │  produces SchemaChanges
      ▼
 AiSchemaAnalyzer  ◀── OPENAI_API_KEY (optional)
      │  produces EnhancedSchemaChanges
      ▼
  T4 Template
      │  generates
      ▼
  XSLT Stylesheet
```

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An OpenAI API key *(optional – required only for AI-enhanced mode)*
- `xsltproc` or equivalent XSLT processor for testing transformations *(optional)*

### Clone and build

```bash
git clone https://github.com/andrew-m-denooyer/xslt-source-gen-sample.git
cd xslt-source-gen-sample
dotnet build
```

### Set up your OpenAI API key

```bash
# Linux/macOS
export OPENAI_API_KEY=sk-...your-key-here...

# Windows (PowerShell)
$env:OPENAI_API_KEY = "sk-...your-key-here..."

# Windows (cmd)
set OPENAI_API_KEY=sk-...your-key-here...
```

> **Note:** The API key is *never* stored in source code or config files. It is read from the environment variable at runtime.

### Run the basic example

```bash
dotnet run
# or explicitly:
dotnet run -- basic
```

This runs `SchemaComparer` against the example schemas and prints a human-readable diff. No API calls are made.

Expected output:

```
=======================================================
 XSLT Source Generation Sample
 C# + T4 Templates + OpenAI
=======================================================

MODE: Basic Schema Comparison (no AI)
--------------------------------------

Schema Comparison: v1.0 → v2.0
Root element: Policy

ADDED ELEMENTS (3):
  + Policy/PolicyVersion [string] (required)
  + Policy/AuditTimestamp [dateTime]
  + Policy/Insured/TaxId [string]

REMOVED ELEMENTS (2):
  - Policy/LegacyPolicyId [string]
  - Policy/Coverage/Notes [string]

RENAMED ELEMENTS (6):
  ≡ Policy/EffDt  →  Policy/EffectiveDate
  ≡ Policy/ExpDt  →  Policy/ExpirationDate
  ...
```

### Run the AI-enhanced example

```bash
export OPENAI_API_KEY=sk-...
dotnet run -- ai
```

This calls the OpenAI API and prints AI-generated insights including:
- A plain-English summary of the migration
- Suggested default values for new required fields
- Migration risk warnings
- Recommended element mappings

### Generate XSLT files using T4 templates

The T4 templates are processed by the `dotnet-t4` CLI tool:

```bash
# Install the T4 CLI tool (once)
dotnet tool install -g dotnet-t4

# Generate basic XSLT (no AI)
t4 templates/BasicXsltGenerator.tt \
   -o examples/output/Transform_v1_to_v2_basic.xslt

# Generate AI-enhanced XSLT (requires OPENAI_API_KEY)
t4 templates/AiEnhancedXsltGenerator.tt \
   -o examples/output/Transform_v1_to_v2_ai_enhanced.xslt
```

> **Note:** The `examples/output/` directory already contains pre-generated reference copies. You only need to run the templates if you modify the schemas or want to see live AI output.

---

## How It Works

### 1. Schema comparison (`SchemaComparer`)

`SchemaComparer` loads each XSD file using `System.Xml.Schema.XmlSchemaSet`, compiles it, and then recursively walks the element declarations to build a flat dictionary of `path → ElementInfo` entries.

For example, the element `<xs:element name="StreetAddress">` nested under `Insured` would produce the path `Policy/Insured/StreetAddress`.

Once both schemas are flattened, the comparer:
- Takes the **set difference** to find added and removed paths.
- Looks for elements with the **same name at different paths** to detect renames vs. moves.
- Compares element **ordering** within each parent to detect reordering.

See `src/SchemaComparer.cs` for the detailed, commented implementation.

### 2. AI-enhanced analysis (`AiSchemaAnalyzer`)

The structural diff is serialised into a concise text prompt and sent to the OpenAI Chat Completions API. The system prompt instructs the model to return **only valid JSON** matching the `AiSchemaAnalysis` schema.

The JSON response is deserialised into typed C# models:
- `DefaultValue[]` – recommended defaults for new required elements
- `MigrationRisk[]` – potential data-loss or breaking-change warnings
- `ElementMapping[]` – explicit source→target mappings with XPath expressions
- `string[]` patterns – high-level patterns detected (e.g., "address-restructure")

**Prompt engineering approach:**
- Low temperature (0.2) for consistent, deterministic output
- `response_format: { type: "json_object" }` to prevent prose in the response
- Domain context appended to the user message when a DSL dictionary is provided
- Clear schema definition in the system prompt reduces hallucination

See `src/AiSchemaAnalyzer.cs` for the full prompt and response handling.

### 3. T4 template generation

T4 templates are text files with embedded C# code (similar to PHP or Razor). When processed, they execute the C# and emit text to the output file.

The templates use three block types:
- `<# code #>` – control flow (loops, conditions)
- `<#= expression #>` – emit a value inline
- `<#+ method #>` – helper methods used throughout the template

The `BasicXsltGenerator.tt` template:
1. Creates a `SchemaComparer` and calls `Compare()`
2. Iterates through each category of change
3. Emits the appropriate XSLT construct for each change type

The `AiEnhancedXsltGenerator.tt` template additionally:
1. Creates an `AiSchemaAnalyzer` and calls `AnalyseAsync()`
2. Uses AI-recommended defaults instead of empty placeholders
3. Embeds AI risk warnings as XSLT comments
4. Follows AI-suggested mapping types (computed, conditional, etc.)

### 4. Example output walkthrough

Given `sample-v1.xml`:
```xml
<Policy>
  <PolicyNumber>HOME-123456</PolicyNumber>
  <EffDt>2024-01-01</EffDt>
  <PremiumAmt>1250.00</PremiumAmt>
  ...
</Policy>
```

The generated XSLT produces:
```xml
<Policy>
  <PolicyNumber>HOME-123456</PolicyNumber>
  <PolicyVersion>2.0</PolicyVersion>       ← AI default: '2.0'
  <EffectiveDate>2024-01-01</EffectiveDate> ← renamed from EffDt
  <PremiumAmount>1250.00</PremiumAmount>    ← renamed from PremiumAmt
  ...
</Policy>
```

---

## Customization Guide

### Adapting for your own schemas

1. Replace `examples/schemas/v1.0.xsd` and `v2.0.xsd` with your own XSD files.
2. Update the paths at the top of each T4 template.
3. Run `t4 templates/BasicXsltGenerator.tt` to generate the XSLT.

### Customizing the DSL dictionary

Edit `config/domain-dsl-dictionary.json` to add your domain's:
- **Abbreviations** – so the AI knows `Eff` means `Effective`
- **Business rules** – constraints the AI should respect when suggesting defaults
- **Migration hints** – specific guidance for known changes
- **Common patterns** – recurring change patterns in your domain

### Modifying T4 templates

The templates are designed to be readable starting points. Common modifications:
- **Custom XSLT version** – change `version="1.0"` to `2.0` and add XPath 2.0 expressions
- **Namespace handling** – add namespace declarations for schemas with namespaces
- **Complex type templates** – add dedicated `<xsl:template>` blocks for complex types
- **Conditional logic** – use the AI's `conditional` mapping type to add `<xsl:choose>` blocks

### Adding custom transformation rules

For domain-specific rules not captured by schema analysis:
1. Add entries to the `migrationHints` section of `domain-dsl-dictionary.json`
2. Or subclass `SchemaComparer` and override the comparison logic
3. Or add custom rule handlers in the T4 templates after the AI mapping loop

---

## Sample Output

### Basic (no AI) output snippet

```xml
<!-- Renamed: 'Policy/EffDt' → 'Policy/EffectiveDate' -->
<EffectiveDate>
  <xsl:value-of select="EffDt"/>
</EffectiveDate>

<!-- Added: 'Policy/PolicyVersion' [string] REQUIRED -->
<PolicyVersion><!-- TODO: provide default --></PolicyVersion>
```

### AI-enhanced output snippet

```xml
<!-- [RENAME] Policy/EffDt → Policy/EffectiveDate -->
<!-- AI note: EffDt is an abbreviation for EffectiveDate per domain DSL dictionary. -->
<EffectiveDate><xsl:value-of select="EffDt"/></EffectiveDate>

<!-- [DIRECT] Policy/PolicyVersion → Policy/PolicyVersion -->
<!-- AI note: New required audit field. Default to '2.0' for all migrated records. -->
<PolicyVersion>2.0</PolicyVersion>
```

Notice that the AI-enhanced version:
- Provides a real default value (`2.0`) instead of a `TODO` comment
- Includes a rationale for the default
- Identifies the mapping type explicitly
- Embeds risk warnings inline

The full pre-generated files are in `examples/output/`.

---

## Limitations & Next Steps

### What this sample doesn't cover

- **Complex XPath expressions** – computed fields that require multi-source XPath are generated as TODOs
- **Attribute migrations** – the current `SchemaComparer` handles elements only, not XSD attributes
- **Multiple root elements** – assumes a single root element per schema
- **Schema namespaces** – elements in custom namespaces may not be handled correctly
- **Production error handling** – retry logic, circuit breakers, and structured logging are not included
- **Test coverage** – no automated tests are included (by design, to keep the sample focused)
- **Cost optimisation** – no batching or prompt compression for large schemas

### Recommendations for production

1. **Add retry logic** for OpenAI API calls (rate limits, transient errors)
2. **Cache AI responses** keyed by a hash of the schema diff to avoid repeat API calls
3. **Add automated tests** that validate the generated XSLT against known transformations
4. **Use XSLT 2.0/3.0** for more expressive transformations (dates, sequences, etc.)
5. **Validate generated XSLT** against the target schema before deployment
6. **Integrate into CI/CD** – run generation as a build step and fail if the output changes unexpectedly
7. **Version the AI prompts** – prompt changes can alter output; treat prompts like code

### Ideas for enhancement

- **Interactive mode** – let users review and approve AI suggestions before generating XSLT
- **Multiple schema versions** – generate a transformation chain v1→v2→v3 automatically
- **Reverse migration** – generate transformations in both directions
- **Migration report** – generate an HTML report alongside the XSLT documenting each decision
- **GitHub Actions integration** – automatically generate XSLT when schema files change in a PR

---

## References

### T4 Templates
- [T4 Text Templates – Microsoft Docs](https://learn.microsoft.com/en-us/visualstudio/modeling/code-generation-and-t4-text-templates)
- [dotnet-t4 CLI tool](https://github.com/mono/t4)
- [T4 Template Syntax Reference](https://learn.microsoft.com/en-us/visualstudio/modeling/writing-a-t4-text-template)

### OpenAI API
- [OpenAI Chat Completions API](https://platform.openai.com/docs/api-reference/chat)
- [OpenAI .NET SDK](https://github.com/openai/openai-dotnet)
- [Structured Outputs / JSON mode](https://platform.openai.com/docs/guides/structured-outputs)

### XSLT & XSD
- [XSLT 1.0 Specification (W3C)](https://www.w3.org/TR/xslt/)
- [XML Schema (XSD) Specification (W3C)](https://www.w3.org/XML/Schema)
- [System.Xml.Schema Namespace – Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.xml.schema)
- [XslCompiledTransform – .NET XSLT Processor](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xsl.xslcompiledtransform)

### Related concepts
- [Schema Evolution Patterns](https://en.wikipedia.org/wiki/Schema_migration)
- [Code Generation with T4 (Visual Studio Magazine)](https://visualstudiomagazine.com/articles/2012/10/01/code-generation-with-t4.aspx)