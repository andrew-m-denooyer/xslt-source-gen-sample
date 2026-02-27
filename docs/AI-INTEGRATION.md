# AI Integration Guide

This document explains the AI integration in the XSLT Source Generation Sample:
why it's useful, how the prompts are engineered, how to control costs, and how
to handle failures gracefully.

---

## Table of Contents

- [Why AI for Schema Analysis?](#why-ai-for-schema-analysis)
- [How the Integration Works](#how-the-integration-works)
- [Prompt Engineering](#prompt-engineering)
- [Cost Optimization](#cost-optimization)
- [Handling Failures Gracefully](#handling-failures-gracefully)
- [Security Considerations](#security-considerations)
- [Extending the AI Analysis](#extending-the-ai-analysis)

---

## Why AI for Schema Analysis?

Structural schema comparison (`SchemaComparer`) tells you *what* changed:

```
RENAMED: Policy/EffDt → Policy/EffectiveDate
ADDED:   Policy/PolicyVersion [string] (required)
```

But it doesn't tell you *why* or *what to do*:

- Is `EffDt` renamed to `EffectiveDate`? Or is `EffDt` removed and `EffectiveDate` is a completely new field?
- What value should `PolicyVersion` have for documents migrated from v1.0?
- Are there any data integrity risks in this migration?

A large language model trained on millions of documents (including XML, XSD, XSLT, and
insurance domain text) can answer these questions with surprisingly high accuracy when
given the right context.

**AI adds value by:**

| Capability | Example |
|---|---|
| Abbreviation recognition | Knows `EffDt` = `EffectiveDate` |
| Domain inference | Knows `PolicyVersion` for a v1→v2 migration should default to `"2.0"` |
| Risk identification | Flags that removing `Notes` may cause unstructured data loss |
| Pattern labelling | Identifies "address restructure" as a named migration pattern |
| Mapping type suggestion | Recommends `rename` vs `computed` vs `conditional` mappings |

---

## How the Integration Works

```
AiSchemaAnalyzer.AnalyseAsync(SchemaChanges, domainContext?)
    │
    ├── Check for OPENAI_API_KEY
    │     └── Missing → return CreateFallbackAnalysis()
    │
    ├── BuildSystemPrompt()   → defines role + JSON output schema
    ├── BuildUserPrompt()     → serialises SchemaChanges + domain context
    │
    ├── POST https://api.openai.com/v1/chat/completions
    │     model: gpt-4o-mini
    │     response_format: { type: "json_object" }
    │
    ├── Parse response JSON → AiSchemaAnalysis
    │
    └── Return EnhancedSchemaChanges
```

The response is a JSON object matching this schema:
```json
{
  "summary": "...",
  "defaultValues": [{ "elementPath": "...", "value": "...", "reason": "..." }],
  "risks": [{ "severity": "low|medium|high", "description": "...", "affectedElement": "...", "mitigation": "..." }],
  "mappings": [{ "sourcePath": "...", "targetPath": "...", "mappingType": "...", "transformExpression": null, "notes": "..." }],
  "patterns": ["..."]
}
```

---

## Prompt Engineering

### System prompt design principles

The system prompt in `BuildSystemPrompt()` follows these principles:

**1. Explicit role definition**
```
You are an expert XML schema migration analyst.
```
Setting a specific role improves response quality for domain-specific tasks.

**2. Strict output format**
```
You MUST respond with ONLY valid JSON that matches this exact structure: ...
Do NOT include any text outside the JSON.
Do NOT use markdown code fences.
```
Combined with `response_format: json_object`, this virtually eliminates non-JSON output.

**3. Inline schema in the prompt**
The JSON response schema is included verbatim in the system prompt. This is more
reliable than describing the structure in prose.

**4. Low temperature**
```csharp
temperature: 0.2
```
Lower temperature = more deterministic output. For structured JSON extraction,
temperatures above 0.5 tend to introduce variation that breaks deserialisation.

### User prompt structure

The user prompt serialises `SchemaChanges` into a human-readable format:
```
ADDED ELEMENTS (exist in target, not in source):
  + Policy/PolicyVersion [string] required=True
  + Policy/AuditTimestamp [dateTime] required=False

RENAMED ELEMENTS (different name, same structure):
  ≡ Policy/EffDt  →  Policy/EffectiveDate
```

This format is chosen because:
- It's compact (fewer tokens = lower cost)
- It uses symbols (`+`, `-`, `~`, `≡`) that are meaningful to the model
- It includes type information which helps the model suggest appropriate defaults
- It clearly separates categories, making the model's task well-defined

### Domain context injection

When `domain-dsl-dictionary.json` is provided, its contents are appended to the
user prompt under a `DOMAIN CONTEXT` heading:

```
DOMAIN CONTEXT (use this to apply domain knowledge):
{
  "abbreviations": { "EffDt": "EffectiveDate", ... },
  "businessRules": { "PolicyVersion": { "default": "2.0" } },
  ...
}
```

This allows the model to apply business rules and domain conventions without
needing to infer them from the schema names alone.

---

## Cost Optimization

### Model selection

| Model | Cost | Quality | Recommendation |
|---|---|---|---|
| `gpt-4o-mini` | ~$0.15/1M tokens | Very good for JSON | **Default – best value** |
| `gpt-4o` | ~$5/1M tokens | Excellent | Use for complex schemas |
| `gpt-3.5-turbo` | ~$0.50/1M tokens | Good | Lower quality JSON |

For a typical schema diff (50–100 elements changed), a single API call costs
approximately $0.001–$0.005 with `gpt-4o-mini`.

### Token budget

The default `maxTokens: 1000` in `ai-config.json` limits the response size.
For schemas with many changes, you may need to increase this. A rough guide:
- Simple migration (< 10 changes): 500 tokens
- Medium migration (10–50 changes): 1000 tokens
- Large migration (50+ changes): 2000 tokens

### Response caching

For development and CI, cache responses by hashing the schema diff:

```csharp
string cacheKey = Convert.ToHexString(
    SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(changes))));
string cacheFile = Path.Combine(cacheDir, $"{cacheKey}.json");

if (File.Exists(cacheFile))
    return JsonSerializer.Deserialize<AiSchemaAnalysis>(await File.ReadAllTextAsync(cacheFile))!;

var result = await CallOpenAiAsync(changes, domainContext);
await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(result));
return result;
```

### Batching

If you need to migrate multiple schema version pairs (v1→v2, v2→v3, v3→v4),
consider batching all diffs into a single API call using a structured prompt
that requests analysis for multiple migration pairs.

---

## Handling Failures Gracefully

The `AiSchemaAnalyzer` is designed to never block the XSLT generation pipeline.
Every failure mode falls back to `CreateFallbackAnalysis()`.

### Failure scenarios

| Scenario | Detection | Fallback |
|---|---|---|
| No API key | `string.IsNullOrWhiteSpace(_apiKey)` | `CreateFallbackAnalysis()` |
| HTTP error (4xx/5xx) | `!response.IsSuccessStatusCode` | Logs error + fallback |
| Rate limit (429) | Same as HTTP error | Logs error + fallback |
| Invalid JSON response | `JsonSerializer.Deserialize()` throws | `catch(Exception)` + fallback |
| Network timeout | `HttpClient` throws | `catch(Exception)` + fallback |
| Null deserialisation | `?? new AiSchemaAnalysis {...}` | Returns empty analysis |

### Fallback analysis

`CreateFallbackAnalysis()` generates a minimal but useful analysis:
- Direct-copy mappings for all unchanged elements
- Rename mappings for all detected renames
- Medium-risk warnings for new required elements (they need defaults)
- Empty `DefaultValue` entries as placeholders

This means the basic XSLT template always works, even when the AI is unavailable.
The AI-enhanced template produces the same output as the basic template in fallback mode.

### Retry logic (not included – recommended for production)

```csharp
var policy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var result = await policy.ExecuteAsync(() => CallOpenAiAsync(changes, domainContext));
```

Use the [Polly](https://github.com/App-vNext/Polly) library for production retry/circuit-breaker policies.

---

## Security Considerations

### API key management

- **Never** hard-code the API key in source files or config files.
- Read the key from the `OPENAI_API_KEY` environment variable at runtime.
- Use a secrets manager (Azure Key Vault, AWS Secrets Manager, etc.) in production.
- Rotate the key regularly and revoke it if it is accidentally exposed.

### Data sent to OpenAI

The following data is sent to the OpenAI API:
- Element names and paths from the schema diff
- Type information (xs:string, xs:date, etc.)
- Content of `domain-dsl-dictionary.json`

**Do not include** personally identifiable information (PII), production data, or
proprietary business logic in the domain dictionary or schema files before reviewing
your organisation's data governance policies.

### Response validation

The AI response is parsed and used to generate XSLT. Always review generated XSLT
before deploying it to production. The `AiSchemaAnalyzer` does not validate that:
- XPath expressions in `transformExpression` are syntactically correct
- Suggested defaults are semantically valid for your domain
- Risk assessments are complete (the AI may miss risks)

---

## Extending the AI Analysis

### Adding custom analysis dimensions

To request additional analysis from the AI, extend the JSON schema in `BuildSystemPrompt()`:

```csharp
// Add to the JSON schema in the system prompt:
"""
  "performanceConsiderations": [
    {
      "element": "string",
      "concern": "string",
      "recommendation": "string"
    }
  ]
"""
```

Then add a corresponding property to `AiSchemaAnalysis` in `Models.cs`.

### Using different AI providers

Replace the `CallOpenAiAsync()` method body with calls to other LLM APIs.
The prompt engineering approach (system + user messages, JSON output format) works
with any OpenAI-compatible API (Azure OpenAI, Anthropic Claude, Google Gemini, etc.),
with minor adjustments for provider-specific request formats.

### Fine-tuning

For high-volume production use with a specific domain, consider fine-tuning a model
on your organisation's historical schema migrations. This can dramatically improve
accuracy for domain-specific abbreviations and business rules while reducing prompt
length (and cost).
