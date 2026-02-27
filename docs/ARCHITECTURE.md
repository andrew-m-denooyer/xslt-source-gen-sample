# Architecture

This document describes the internal architecture of the XSLT Source Generation Sample,
including component relationships, data flow, extension points, and design decisions.

---

## Table of Contents

- [High-Level Overview](#high-level-overview)
- [Component Detail](#component-detail)
- [Data Flow](#data-flow)
- [Extension Points](#extension-points)
- [Design Decisions](#design-decisions)

---

## High-Level Overview

The pipeline consists of three stages:

```
Stage 1: Schema Analysis
  SchemaComparer reads and diffs two XSD files → SchemaChanges

Stage 2: AI Enhancement (optional)
  AiSchemaAnalyzer sends diff to OpenAI → EnhancedSchemaChanges

Stage 3: Code Generation
  T4 Template consumes EnhancedSchemaChanges → XSLT stylesheet
```

Each stage is independent and loosely coupled:
- Stage 1 can be used alone (basic XSLT generation).
- Stage 2 enhances the output of Stage 1 but does not modify it.
- Stage 3 consumes either `SchemaChanges` or `EnhancedSchemaChanges`.

---

## Component Detail

### `SchemaComparer` (`src/SchemaComparer.cs`)

**Responsibility:** Structural comparison of two XSD schemas.

**Algorithm:**
1. Load each XSD into an `XmlSchemaSet` and compile it.
2. Recursively flatten all element declarations into `Dictionary<path, ElementInfo>`.
3. Compute set differences (added/removed paths).
4. Cross-reference by element name to detect renames vs. true additions/removals.
5. Compare element ordering within each parent group to detect reordering.
6. Detect moves: same name, different parent path, not a rename.

**Inputs:**
- Two XSD file paths
- Version labels (strings)

**Output:**
- `SchemaChanges` with six categorised lists/dictionaries

**Limitations:**
- Handles `xs:sequence`, `xs:choice`, and `xs:all` compositors.
- Does not handle XSD attributes, groups defined outside elements, or substitution groups.
- Rename detection uses a simple heuristic (same name, count=1 in both added and removed).

---

### `AiSchemaAnalyzer` (`src/AiSchemaAnalyzer.cs`)

**Responsibility:** Semantic enhancement of structural schema diffs using OpenAI.

**Prompt structure:**
```
SYSTEM: Role definition + JSON output schema
USER:   Serialised SchemaChanges + optional domain context
```

**Response parsing:**
- Requests `response_format: { type: "json_object" }` to enforce JSON output.
- Deserialises response into `AiSchemaAnalysis` using `System.Text.Json`.

**Fallback behaviour:**
- No API key → returns `CreateFallbackAnalysis()` with direct-copy mappings.
- API error → catches exception, logs message, returns fallback.
- Invalid JSON → `JsonSerializer` throws; caught and returns fallback.

**Statefulness:**
- The `HttpClient` instance is reused across calls (thread-safe for concurrent use).
- The `Authorization` header is reset on each call (supports key rotation).

---

### Models (`src/Models.cs`)

All models use C# records or classes with auto-properties. JSON property names
use camelCase (matching OpenAI response format) via `[JsonPropertyName]` attributes.

```
SchemaChanges
├── ElementInfo[] AddedElements
├── ElementInfo[] RemovedElements
├── Dictionary<string,string> MovedElements
├── ElementInfo[] ReorderedElements
├── Dictionary<string,string> RenamedElements
└── ElementInfo[] UnchangedElements

EnhancedSchemaChanges : SchemaChanges
└── AiSchemaAnalysis? AiAnalysis
    ├── string Summary
    ├── DefaultValue[] DefaultValues
    ├── MigrationRisk[] Risks
    ├── ElementMapping[] Mappings
    └── string[] Patterns
```

---

### T4 Templates (`templates/`)

T4 templates are C# code embedded in text. The template engine executes the C# at
"design time" (or via dotnet-t4) and emits the text portions plus any `<#= expr #>` values.

**Template execution model:**
```
T4 Engine
  ├── Loads assembly (XsltSourceGenSample.dll)
  ├── Executes <# control blocks #>
  │    ├── Calls SchemaComparer.Compare()
  │    └── (AI template only) Calls AiSchemaAnalyzer.AnalyseAsync().GetAwaiter().GetResult()
  └── Emits text + <#= expressions #> to output file
```

**Why `.GetAwaiter().GetResult()` instead of `await`?**
T4 templates run synchronously on a single thread. The `async/await` pattern is not
supported in T4 control blocks. Calling `.GetAwaiter().GetResult()` blocks the thread
until the async operation completes. This is safe here because templates are run
as standalone tools, not in an ASP.NET synchronisation context.

---

## Data Flow

```
v1.0.xsd ─┐
           ├──▶ SchemaComparer.Compare() ──▶ SchemaChanges
v2.0.xsd ─┘                                      │
                                                  │ (basic template)
domain-dsl-dictionary.json ─┐                    │
                             ├──▶ AiSchemaAnalyzer.AnalyseAsync()
SchemaChanges ───────────────┘         │
                                       ▼
                               EnhancedSchemaChanges
                                       │
                    ┌──────────────────┴──────────────────┐
                    ▼                                     ▼
         BasicXsltGenerator.tt              AiEnhancedXsltGenerator.tt
                    │                                     │
                    ▼                                     ▼
         Transform_v1_to_v2_basic.xslt    Transform_v1_to_v2_ai_enhanced.xslt
```

---

## Extension Points

### 1. Custom change detectors

Subclass `SchemaComparer` and override specific detection methods to add domain rules:
```csharp
public class MyComparer : SchemaComparer
{
    // Override to add custom rename heuristics
}
```

### 2. Custom AI prompts

Replace the system or user prompt by modifying `AiSchemaAnalyzer`:
- Override `BuildSystemPrompt()` to change the response schema.
- Override `BuildUserPrompt()` to change how diffs are serialised for the AI.

### 3. Additional T4 templates

Add new `.tt` files for:
- Different XSLT versions (2.0/3.0)
- Different output formats (C# migration code, SQL scripts, documentation)
- Different schema languages (JSON Schema, Avro, Protobuf)

### 4. Domain dictionary extensions

The `domain-dsl-dictionary.json` format is free-form JSON. Add any domain-specific
sections and read them in `AiSchemaAnalyzer.BuildUserPrompt()` to enrich the prompt.

### 5. Response caching

Add a caching layer around `CallOpenAiAsync()` keyed by a hash of the schema diff:
```csharp
string cacheKey = SHA256(JsonSerializer.Serialize(changes));
if (cache.TryGetValue(cacheKey, out var cached)) return cached;
var result = await CallOpenAiAsync(changes, domainContext);
cache[cacheKey] = result;
return result;
```

---

## Design Decisions

### Why XSLT 1.0 (not 2.0)?

XSLT 1.0 is universally supported and can be processed by .NET's built-in
`XslCompiledTransform`. XSLT 2.0/3.0 requires Saxon or similar third-party processor.
The templates can be modified to target 2.0/3.0 by changing the version attribute
and adding XPath 2.0 expressions.

### Why T4 templates (not Roslyn source generators or Razor)?

T4 is explicitly designed for generating arbitrary text (not just C#). It has
first-class Visual Studio integration and the `dotnet-t4` CLI tool for CI usage.
Roslyn source generators are limited to C# and run at compile time. Razor generates
HTML. T4 is the most natural fit for generating XML/XSLT.

### Why OpenAI Chat API (not structured completions)?

The Chat API with `response_format: json_object` gives the best combination of:
- Instruction following (system prompt respected)
- Structured output (parseable JSON)
- Context window (large schemas fit in the prompt)
- Cost control (gpt-4o-mini is ~20x cheaper than gpt-4)

### Why not use the official OpenAI .NET SDK for HTTP calls?

The `AiSchemaAnalyzer` uses raw `HttpClient` to minimise dependencies and make
the HTTP interaction fully visible/educational. The official `OpenAI` NuGet package
is listed in the `.csproj` as an available dependency but not used in the core
library, so developers can choose either approach.

### Why a flat path dictionary for schema comparison?

XSD schemas can be deeply nested. A flat dictionary keyed by full path makes
set operations (add, remove, intersect) trivial and avoids complex recursive
tree diffing logic. The trade-off is that the path-based approach may miss
some semantic equivalences that a tree diff would catch.
