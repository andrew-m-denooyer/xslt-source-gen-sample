<?xml version="1.0" encoding="UTF-8"?>
<!--
  =============================================================================
  AUTO-GENERATED XSLT STYLESHEET
  =============================================================================
  Source schema : v1.0
  Target schema : v2.0
  Generated on  : 2024-01-15 00:00:00 UTC (pre-generated reference copy)
  Generator     : BasicXsltGenerator.tt (T4 template – no AI)

  Summary of detected changes
  ---------------------------
  Added elements    : 3  (PolicyVersion, AuditTimestamp, TaxId)
  Removed elements  : 2  (LegacyPolicyId, Notes)
  Moved elements    : 4  (address fields into nested Address element)
  Renamed elements  : 6  (EffDt, ExpDt, PremiumAmt, InsuredName, CoverageAmt, DeductibleAmt)
  Reordered elements: 4  (LineOfBusiness, DeductibleAmount/CoverageAmount)
  Unchanged elements: 7  (PolicyNumber, LineOfBusiness, PolicyStatus, DateOfBirth, etc.)

  IMPORTANT: Review this file before using in production.
  Default values for new required elements are empty strings – replace them
  with appropriate values for your environment.

  HOW TO REGENERATE THIS FILE
  ---------------------------
  1. Install dotnet-t4:  dotnet tool install -g dotnet-t4
  2. Build the project:  dotnet build
  3. Run the template:
       t4 templates/BasicXsltGenerator.tt \
          -o examples/output/Transform_v1_to_v2_basic.xslt

  HOW TO TEST THIS TRANSFORMATION
  --------------------------------
  Using xsltproc (Linux/macOS):
    xsltproc examples/output/Transform_v1_to_v2_basic.xslt \
             examples/schemas/sample-v1.xml

  Using .NET XslCompiledTransform:
    See examples/README.md for a code snippet.
  =============================================================================
-->
<xsl:stylesheet
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    version="1.0">

  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <!-- =========================================================
       ROOT TEMPLATE
       Matches the document root and triggers element-level
       templates for the top-level Policy element.
       ========================================================= -->
  <xsl:template match="/">
    <xsl:apply-templates select="Policy"/>
  </xsl:template>

  <!-- =========================================================
       POLICY ELEMENT TEMPLATE
       Rebuilds the Policy element with the v2.0 structure.
       ========================================================= -->
  <xsl:template match="Policy">
    <Policy>

      <!-- ~~~ RENAMED ELEMENTS ~~~ -->
      <!-- These elements were renamed between v1.0 and v2.0. -->

      <!-- Renamed: 'Policy/EffDt' → 'Policy/EffectiveDate' -->
      <EffectiveDate>
        <xsl:value-of select="EffDt"/>
      </EffectiveDate>

      <!-- Renamed: 'Policy/ExpDt' → 'Policy/ExpirationDate' -->
      <ExpirationDate>
        <xsl:value-of select="ExpDt"/>
      </ExpirationDate>

      <!-- Renamed: 'Policy/PremiumAmt' → 'Policy/PremiumAmount' -->
      <PremiumAmount>
        <xsl:value-of select="PremiumAmt"/>
      </PremiumAmount>

      <!-- Renamed: 'Policy/Insured/InsuredName' → 'Policy/Insured/InsuredFullName'
           (handled in the Insured template below) -->

      <!-- Renamed: 'Policy/Coverage/CoverageAmt' → 'Policy/Coverage/CoverageAmount'
           (handled in the Coverage template below) -->

      <!-- Renamed: 'Policy/Coverage/DeductibleAmt' → 'Policy/Coverage/DeductibleAmount'
           (handled in the Coverage template below) -->

      <!-- ~~~ ADDED ELEMENTS ~~~ -->
      <!-- These elements are new in v2.0 and have no source equivalent. -->
      <!-- TODO: Replace empty defaults with appropriate values for your domain. -->

      <!-- Added: 'Policy/PolicyVersion' [string] REQUIRED -->
      <PolicyVersion><!-- TODO: provide default, e.g. '2.0' --></PolicyVersion>

      <!-- Added: 'Policy/AuditTimestamp' [dateTime] -->
      <AuditTimestamp><!-- TODO: provide default, e.g. current datetime --></AuditTimestamp>

      <!-- ~~~ UNCHANGED ELEMENTS ~~~ -->
      <!-- These elements are identical in both schema versions. -->
      <PolicyNumber><xsl:value-of select="PolicyNumber"/></PolicyNumber>
      <LineOfBusiness><xsl:value-of select="LineOfBusiness"/></LineOfBusiness>
      <PolicyStatus><xsl:value-of select="PolicyStatus"/></PolicyStatus>

      <!-- ~~~ INSURED SECTION ~~~ -->
      <!-- Apply the Insured template which handles the address restructure -->
      <xsl:apply-templates select="Insured"/>

      <!-- ~~~ COVERAGE SECTION ~~~ -->
      <xsl:apply-templates select="Coverage"/>

    </Policy>
  </xsl:template>

  <!-- =========================================================
       INSURED ELEMENT TEMPLATE
       Handles: rename (InsuredName→InsuredFullName),
                structural move (flat address → nested Address),
                added element (TaxId)
       ========================================================= -->
  <xsl:template match="Insured">
    <Insured>

      <!-- Renamed: InsuredName → InsuredFullName -->
      <InsuredFullName>
        <xsl:value-of select="InsuredName"/>
      </InsuredFullName>

      <!-- Unchanged -->
      <DateOfBirth><xsl:value-of select="DateOfBirth"/></DateOfBirth>

      <!-- Added: TaxId (new in v2.0 – optional) -->
      <TaxId><!-- TODO: provide value if available --></TaxId>

      <!-- Unchanged -->
      <EmailAddress><xsl:value-of select="EmailAddress"/></EmailAddress>
      <PhoneNumber><xsl:value-of select="PhoneNumber"/></PhoneNumber>

      <!-- MOVED: flat address fields wrapped in nested Address element -->
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
       Handles: renames (CoverageAmt→CoverageAmount, etc.),
                reordering (DeductibleAmount now before CoverageAmount),
                removed element (Notes)
       ========================================================= -->
  <xsl:template match="Coverage">
    <Coverage>

      <!-- Unchanged -->
      <CoverageType><xsl:value-of select="CoverageType"/></CoverageType>

      <!-- REORDERED + RENAMED: DeductibleAmt → DeductibleAmount (now comes first) -->
      <DeductibleAmount>
        <xsl:value-of select="DeductibleAmt"/>
      </DeductibleAmount>

      <!-- RENAMED: CoverageAmt → CoverageAmount -->
      <CoverageAmount>
        <xsl:value-of select="CoverageAmt"/>
      </CoverageAmount>

      <!-- NOTE: 'Notes' element removed in v2.0 – value is not migrated -->

    </Coverage>
  </xsl:template>

</xsl:stylesheet>
