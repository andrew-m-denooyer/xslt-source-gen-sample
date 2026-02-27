# Examples Walkthrough

This guide walks you through running the example schemas, understanding what changed
between v1.0 and v2.0, and testing the generated XSLT transformations.

---

## Table of Contents

- [Example Schemas Overview](#example-schemas-overview)
- [What Changed Between v1.0 and v2.0](#what-changed-between-v10-and-v20)
- [Running the Examples](#running-the-examples)
- [Interpreting the Generated XSLT](#interpreting-the-generated-xslt)
- [Testing the Transformation](#testing-the-transformation)
- [Using Your Own Schemas](#using-your-own-schemas)

---

## Example Schemas Overview

The `examples/schemas/` directory contains a simple insurance policy schema in two versions:

| File | Description |
|---|---|
| `v1.0.xsd` | Source schema – the "before" version |
| `v2.0.xsd` | Target schema – the "after" version |
| `sample-v1.xml` | A sample XML document valid against v1.0 |

The schemas model a simplified insurance policy with:
- Policy identifiers and dates
- Insured party information (name, address, contact)
- Coverage details (type, amounts)

---

## What Changed Between v1.0 and v2.0

The v2.0 schema introduces changes that represent common real-world migration patterns:

### Added fields (new requirements)

| Element | Type | Required | Reason |
|---|---|---|---|
| `Policy/PolicyVersion` | `xs:string` | Yes | Audit/compatibility tracking |
| `Policy/AuditTimestamp` | `xs:dateTime` | No | Regulatory audit trail |
| `Insured/TaxId` | `xs:string` | No | New regulatory compliance requirement |

> **Note:** `PolicyVersion` is required but has no source value, so the generator
> inserts a TODO comment (basic) or the AI-recommended default `"2.0"` (AI-enhanced).

### Removed fields (deprecated)

| Element | Reason for removal |
|---|---|
| `Policy/LegacyPolicyId` | Superseded by `PolicyNumber` |
| `Coverage/Notes` | Replaced by structured fields in v2.0 |

### Renamed fields (abbreviations expanded)

| v1.0 name | v2.0 name |
|---|---|
| `Policy/EffDt` | `Policy/EffectiveDate` |
| `Policy/ExpDt` | `Policy/ExpirationDate` |
| `Policy/PremiumAmt` | `Policy/PremiumAmount` |
| `Insured/InsuredName` | `Insured/InsuredFullName` |
| `Coverage/CoverageAmt` | `Coverage/CoverageAmount` |
| `Coverage/DeductibleAmt` | `Coverage/DeductibleAmount` |

### Restructured fields (address nested)

In v1.0, address was flat under `Insured`. In v2.0 it's wrapped in a nested `Address` element:

```
v1.0:                          v2.0:
Insured/                       Insured/
  StreetAddress                  Address/
  City                             StreetAddress
  State                            City
  PostalCode                       State
                                   PostalCode
```

### Reordered elements

- In `Coverage`: `DeductibleAmount` now appears **before** `CoverageAmount` (was after in v1.0).
- In `Policy`: `LineOfBusiness` now follows `PolicyVersion` (was in position 3 in v1.0).

---

## Running the Examples

### Step 1: Build the project

```bash
cd xslt-source-gen-sample
dotnet build
```

### Step 2: Run the basic comparison

```bash
dotnet run -- basic
```

Expected output shows all detected changes across the six categories.

### Step 3: Run the AI-enhanced analysis

```bash
# Set your API key first
export OPENAI_API_KEY=sk-...your-key...

dotnet run -- ai
```

The AI analysis adds:
- Plain-English summary of the migration
- Recommended default: `PolicyVersion = "2.0"`
- Risk warnings for removed `Notes` element
- Pattern identification: "field-expansion", "address-restructure"

### Step 4: Generate the XSLT files

```bash
# Install the T4 CLI tool (one-time setup)
dotnet tool install -g dotnet-t4

# Basic XSLT (no AI)
t4 templates/BasicXsltGenerator.tt \
   -o examples/output/Transform_v1_to_v2_basic.xslt

# AI-enhanced XSLT
t4 templates/AiEnhancedXsltGenerator.tt \
   -o examples/output/Transform_v1_to_v2_ai_enhanced.xslt
```

> **Note:** Pre-generated copies of both files are already in `examples/output/`
> so you can review the expected output without running the templates.

---

## Interpreting the Generated XSLT

Both generated XSLT files use XSLT 1.0 and the `xsl:value-of` pattern for element construction.
Here's what to look for in each section:

### Renamed elements

```xml
<!-- Renamed: 'Policy/EffDt' → 'Policy/EffectiveDate' -->
<EffectiveDate>
  <xsl:value-of select="EffDt"/>
</EffectiveDate>
```

The element is created with the new name, and its value is copied from the old path.

### Added elements (basic – with TODO)

```xml
<!-- Added: 'Policy/PolicyVersion' [string] REQUIRED -->
<PolicyVersion><!-- TODO: provide default --></PolicyVersion>
```

Replace `<!-- TODO: provide default -->` with the appropriate default for your domain.

### Added elements (AI-enhanced – with real default)

```xml
<!-- [DIRECT] Policy/PolicyVersion → Policy/PolicyVersion -->
<!-- AI note: New required audit field. Default to '2.0' for all migrated records. -->
<PolicyVersion>2.0</PolicyVersion>
```

The AI has inserted the recommended default based on domain context.

### Moved/restructured elements (address)

```xml
<!-- MOVED: flat address fields wrapped in nested Address element -->
<Address>
  <StreetAddress><xsl:value-of select="StreetAddress"/></StreetAddress>
  <City><xsl:value-of select="City"/></City>
  <State><xsl:value-of select="State"/></State>
  <PostalCode><xsl:value-of select="PostalCode"/></PostalCode>
</Address>
```

Source values are read from the flat path; output wraps them in the new structure.

### Removed elements

Removed elements are simply **omitted** from the XSLT output. The AI-enhanced version
adds a comment noting the removal:

```xml
<!-- NOTE: 'Notes' element removed in v2.0 – value is not migrated -->
<!-- RISK [LOW]: 'Notes' element removed. Archive Notes value to migration log. -->
```

---

## Testing the Transformation

### Using xsltproc (Linux/macOS)

```bash
xsltproc \
  examples/output/Transform_v1_to_v2_basic.xslt \
  examples/schemas/sample-v1.xml
```

Expected output:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<Policy>
  <EffectiveDate>2024-01-01</EffectiveDate>
  <ExpirationDate>2025-01-01</ExpirationDate>
  <PremiumAmount>1250.00</PremiumAmount>
  <PolicyVersion/>
  ...
  <Insured>
    <InsuredFullName>Jane Smith</InsuredFullName>
    <DateOfBirth>1985-06-15</DateOfBirth>
    ...
    <Address>
      <StreetAddress>742 Evergreen Terrace</StreetAddress>
      <City>Springfield</City>
      <State>IL</State>
      <PostalCode>62701</PostalCode>
    </Address>
  </Insured>
</Policy>
```

Notice that `PolicyVersion` is empty (`<PolicyVersion/>`) in the basic output –
this is the TODO placeholder. In the AI-enhanced XSLT it would contain `2.0`.

### Using .NET XslCompiledTransform

```csharp
using System.Xml;
using System.Xml.Xsl;

var xslt = new XslCompiledTransform();
xslt.Load("examples/output/Transform_v1_to_v2_basic.xslt");

using var reader = XmlReader.Create("examples/schemas/sample-v1.xml");
using var writer = XmlWriter.Create(Console.Out, new XmlWriterSettings { Indent = true });
xslt.Transform(reader, writer);
```

### Validating against v2.0 schema

After transformation, validate the output against `v2.0.xsd`:

```csharp
var settings = new XmlReaderSettings();
settings.Schemas.Add(null, "examples/schemas/v2.0.xsd");
settings.ValidationType = ValidationType.Schema;
settings.ValidationEventHandler += (s, e) => Console.WriteLine($"Validation: {e.Message}");

using var validatingReader = XmlReader.Create(transformedOutput, settings);
while (validatingReader.Read()) { } // Walk the document to trigger validation
```

> **Note:** The basic XSLT will likely fail validation because `PolicyVersion` (required
> in v2.0) is empty. Replace the TODO placeholder with `2.0` to fix this.

---

## Using Your Own Schemas

1. **Replace the example schemas:**
   ```bash
   cp your-v1-schema.xsd examples/schemas/v1.0.xsd
   cp your-v2-schema.xsd examples/schemas/v2.0.xsd
   ```

2. **Update the domain dictionary** with your domain's abbreviations and rules:
   ```bash
   # Edit config/domain-dsl-dictionary.json
   ```

3. **Run the basic comparison** to verify the diff looks correct:
   ```bash
   dotnet run -- basic
   ```

4. **Generate the XSLT:**
   ```bash
   t4 templates/BasicXsltGenerator.tt -o my-transform.xslt
   ```

5. **Review and customise** the generated XSLT for any TODO placeholders.

6. **Test** with a sample XML document from your domain.
