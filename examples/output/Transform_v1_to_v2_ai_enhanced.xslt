<?xml version="1.0" encoding="UTF-8"?>
<!--
  =============================================================================
  AI-ENHANCED AUTO-GENERATED XSLT STYLESHEET
  =============================================================================
  Source schema : v1.0
  Target schema : v2.0
  Generated on  : 2024-01-15 00:00:00 UTC (pre-generated reference copy)
  Generator     : AiEnhancedXsltGenerator.tt (T4 template + OpenAI gpt-4o-mini)
  AI model      : gpt-4o-mini

  AI ANALYSIS SUMMARY
  -------------------
  This migration transitions the insurance policy schema from v1.0 to v2.0.
  Key changes include: expansion of abbreviated field names (EffDt→EffectiveDate,
  PremiumAmt→PremiumAmount, etc.), restructuring of the insured address from flat
  fields into a nested Address complex type, addition of audit/compliance fields
  (PolicyVersion, AuditTimestamp, TaxId), removal of the legacy policy identifier
  and unstructured Notes field. The field ordering within Coverage has been reversed
  to place the deductible before the coverage amount.

  Detected patterns: field-expansion, address-restructure, audit-field-addition,
                     field-consolidation, deprecation-removal

  Migration risks identified by AI
  ----------------------------------
  [MEDIUM] Policy/PolicyVersion: New required field with no source equivalent.
           Mitigation: Default to literal string '2.0' for all migrated records.

  [LOW] Policy/Insured/TaxId: New optional field with no source data.
        Mitigation: Leave empty; populate from a separate identity data source post-migration.

  [LOW] Policy/Coverage/Notes: Free-text field removed in v2.0.
        Mitigation: Archive Notes value to a migration log before discarding.
        If any Notes contain structured data, extract it manually before migration.

  [LOW] Policy/LegacyPolicyId: Removed field.
        Mitigation: Archive the value to the migration log; map to PolicyNumber if needed.

  Structural change summary
  -------------------------
  Added elements    : 3
  Removed elements  : 2
  Moved elements    : 4
  Renamed elements  : 6
  Reordered elements: 4
  Unchanged elements: 7

  IMPORTANT: Review this file before using in production.
  =============================================================================
-->
<xsl:stylesheet
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    version="1.0">

  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <!-- =========================================================
       ROOT TEMPLATE
       ========================================================= -->
  <xsl:template match="/">
    <xsl:apply-templates select="Policy"/>
  </xsl:template>

  <!-- =========================================================
       POLICY ELEMENT TEMPLATE
       AI-enhanced: defaults derived from domain context,
       risk warnings embedded as comments.
       ========================================================= -->
  <xsl:template match="Policy">
    <Policy>

      <!-- ~~~ AI-SUGGESTED MAPPINGS ~~~ -->

      <!-- [DIRECT] Policy/PolicyNumber → Policy/PolicyNumber -->
      <!-- AI note: Element unchanged – direct copy. -->
      <PolicyNumber><xsl:value-of select="PolicyNumber"/></PolicyNumber>

      <!-- [DIRECT] Policy/PolicyVersion → Policy/PolicyVersion -->
      <!-- AI note: New required audit field. Default to '2.0' for all migrated records.
           Domain context confirms all v1→v2 migrations use version string '2.0'. -->
      <PolicyVersion>2.0</PolicyVersion>

      <!-- [DIRECT] Policy/LineOfBusiness → Policy/LineOfBusiness -->
      <!-- AI note: Element unchanged – direct copy. -->
      <LineOfBusiness><xsl:value-of select="LineOfBusiness"/></LineOfBusiness>

      <!-- [RENAME] Policy/EffDt → Policy/EffectiveDate -->
      <!-- AI note: EffDt is an abbreviation for EffectiveDate per domain DSL dictionary. Direct value copy. -->
      <EffectiveDate><xsl:value-of select="EffDt"/></EffectiveDate>

      <!-- [RENAME] Policy/ExpDt → Policy/ExpirationDate -->
      <!-- AI note: ExpDt is an abbreviation for ExpirationDate per domain DSL dictionary. Direct value copy. -->
      <ExpirationDate><xsl:value-of select="ExpDt"/></ExpirationDate>

      <!-- [DIRECT] Policy/PolicyStatus → Policy/PolicyStatus -->
      <!-- AI note: Element unchanged – direct copy. -->
      <PolicyStatus><xsl:value-of select="PolicyStatus"/></PolicyStatus>

      <!-- [RENAME] Policy/PremiumAmt → Policy/PremiumAmount -->
      <!-- AI note: PremiumAmt is an abbreviation for PremiumAmount. Direct value copy (xs:decimal type preserved). -->
      <PremiumAmount><xsl:value-of select="PremiumAmt"/></PremiumAmount>

      <!-- [COMPUTED] Policy/AuditTimestamp -->
      <!-- AI note: New optional audit field. The domain dictionary suggests using the
           migration run timestamp. XPath 1.0 does not have current-dateTime() so this
           is left as an empty string; populate via a post-processing step or parameter. -->
      <!-- RISK [LOW]: AuditTimestamp has no source value. Populate from migration pipeline. -->
      <AuditTimestamp><!-- Populate with migration timestamp --></AuditTimestamp>

      <!-- ~~~ INSURED SECTION ~~~ -->
      <xsl:apply-templates select="Insured"/>

      <!-- ~~~ COVERAGE SECTION ~~~ -->
      <xsl:apply-templates select="Coverage"/>

    </Policy>
  </xsl:template>

  <!-- =========================================================
       INSURED ELEMENT TEMPLATE
       AI-enhanced: rename mapping, address restructure,
       new optional TaxId field.
       ========================================================= -->
  <xsl:template match="Insured">
    <Insured>

      <!-- [RENAME] Insured/InsuredName → Insured/InsuredFullName -->
      <!-- AI note: InsuredName renamed to InsuredFullName per domain convention.
           Domain DSL confirms this is a simple rename with no format change. -->
      <InsuredFullName><xsl:value-of select="InsuredName"/></InsuredFullName>

      <!-- [DIRECT] Insured/DateOfBirth → Insured/DateOfBirth -->
      <DateOfBirth><xsl:value-of select="DateOfBirth"/></DateOfBirth>

      <!-- [ADDED] Insured/TaxId (new optional field) -->
      <!-- AI note: New regulatory field. No source data available in v1.0.
           Domain context indicates this should be populated from an identity service
           post-migration, not defaulted to an empty value for production use. -->
      <!-- RISK [LOW]: TaxId is new and optional. Leave empty; populate post-migration. -->
      <TaxId><!-- Populate from identity service post-migration --></TaxId>

      <!-- [DIRECT] Insured/EmailAddress and Insured/PhoneNumber unchanged -->
      <EmailAddress><xsl:value-of select="EmailAddress"/></EmailAddress>
      <PhoneNumber><xsl:value-of select="PhoneNumber"/></PhoneNumber>

      <!-- [RESTRUCTURED] Flat address fields → nested Address element -->
      <!-- AI note: v1.0 had flat StreetAddress/City/State/PostalCode fields directly under
           Insured. v2.0 wraps these in an Address complex type. The AI detected this as an
           'address-restructure' pattern, common in enterprise schema evolution. -->
      <Address>
        <StreetAddress><xsl:value-of select="StreetAddress"/></StreetAddress>
        <City><xsl:value-of select="City"/></City>
        <State><xsl:value-of select="State"/></State>
        <PostalCode><xsl:value-of select="PostalCode"/></PostalCode>
      </Address>

    </Insured>
  </xsl:template>

  <!-- =========================================================
       COVERAGE ELEMENT TEMPLATE
       AI-enhanced: rename mappings, reordering,
       removed Notes element with archive recommendation.
       ========================================================= -->
  <xsl:template match="Coverage">
    <Coverage>

      <!-- [DIRECT] Coverage/CoverageType unchanged -->
      <CoverageType><xsl:value-of select="CoverageType"/></CoverageType>

      <!-- [REORDERED + RENAMED] DeductibleAmt → DeductibleAmount (now appears BEFORE CoverageAmount) -->
      <!-- AI note: The v2.0 schema places deductible before coverage amount. The AI detected
           this reordering as intentional (deductible is now the primary financial constraint).
           Both the order change and the abbreviation expansion are handled here. -->
      <DeductibleAmount><xsl:value-of select="DeductibleAmt"/></DeductibleAmount>

      <!-- [RENAMED] CoverageAmt → CoverageAmount -->
      <!-- AI note: CoverageAmt expanded from abbreviation. Direct value copy. -->
      <CoverageAmount><xsl:value-of select="CoverageAmt"/></CoverageAmount>

      <!-- RISK [LOW]: 'Notes' element removed in v2.0.
           AI recommendation: Archive Notes value to migration log before discarding.
           If Notes contains structured data (e.g., "Deductible waived: true"),
           parse and map to appropriate v2.0 structured fields manually. -->
      <!-- <Notes> intentionally omitted – value was: <xsl:value-of select="Notes"/> -->

    </Coverage>
  </xsl:template>

</xsl:stylesheet>
