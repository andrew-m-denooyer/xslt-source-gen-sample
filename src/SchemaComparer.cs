using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace XsltSourceGenSample
{
    /// <summary>
    /// Compares two XSD schema files and produces a <see cref="SchemaChanges"/> report
    /// describing what elements were added, removed, moved, reordered, or renamed.
    ///
    /// <para>
    /// The comparer works by "flattening" each XSD into a dictionary of
    /// <c>path → ElementInfo</c> entries, then computing set differences and
    /// cross-referencing by element name to distinguish renames from true additions/removals.
    /// </para>
    /// </summary>
    public class SchemaComparer
    {
        // XML Schema namespace URI – used when navigating the XSD DOM.
        private const string XsNamespace = "http://www.w3.org/2001/XMLSchema";

        /// <summary>
        /// Loads and compares two XSD files.
        /// </summary>
        /// <param name="sourceXsdPath">File path to the "before" schema (e.g., v1.0.xsd).</param>
        /// <param name="targetXsdPath">File path to the "after" schema (e.g., v2.0.xsd).</param>
        /// <param name="sourceVersion">Human-readable version label for the source (e.g., "v1.0").</param>
        /// <param name="targetVersion">Human-readable version label for the target (e.g., "v2.0").</param>
        /// <returns>A populated <see cref="SchemaChanges"/> object.</returns>
        public SchemaChanges Compare(
            string sourceXsdPath,
            string targetXsdPath,
            string sourceVersion = "v1.0",
            string targetVersion = "v2.0")
        {
            Console.WriteLine($"[SchemaComparer] Loading source schema: {sourceXsdPath}");
            Console.WriteLine($"[SchemaComparer] Loading target schema: {targetXsdPath}");

            // Load both schemas into in-memory dictionaries keyed by element path.
            var sourceElements = LoadSchema(sourceXsdPath);
            var targetElements = LoadSchema(targetXsdPath);

            Console.WriteLine($"[SchemaComparer] Source elements found: {sourceElements.Count}");
            Console.WriteLine($"[SchemaComparer] Target elements found: {targetElements.Count}");

            // Determine the root element name from one of the schemas.
            // We take the shortest path because the root element has no "/" in its path.
            string rootElementName = sourceElements.Keys
                .OrderBy(p => p.Length)
                .FirstOrDefault() ?? string.Empty;

            var changes = new SchemaChanges
            {
                SourceVersion = sourceVersion,
                TargetVersion = targetVersion,
                RootElementName = rootElementName
            };

            // ----------------------------------------------------------------
            // Step 1 – Identify ADDED and REMOVED elements by path comparison
            // ----------------------------------------------------------------
            var sourcePaths = new HashSet<string>(sourceElements.Keys, StringComparer.OrdinalIgnoreCase);
            var targetPaths = new HashSet<string>(targetElements.Keys, StringComparer.OrdinalIgnoreCase);

            // Paths only in target = added; paths only in source = removed; both = potential rename/unchanged.
            var addedPaths   = targetPaths.Except(sourcePaths).ToList();
            var removedPaths = sourcePaths.Except(targetPaths).ToList();
            var sharedPaths  = sourcePaths.Intersect(targetPaths).ToList();

            // ----------------------------------------------------------------
            // Step 2 – Detect RENAMES: an element with the same name appears
            //          in both removed and added lists (path changed, name same).
            //          We match by element name to distinguish from true add/remove.
            // ----------------------------------------------------------------
            var removedByName = removedPaths
                .GroupBy(p => GetElementName(p))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var addedByName = addedPaths
                .GroupBy(p => GetElementName(p))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Element names that appear in exactly one removed path AND one added path
            // are strong candidates for renames.
            var renamedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var renamedTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in removedByName.Keys)
            {
                if (addedByName.TryGetValue(name, out var matchingAdded) &&
                    removedByName[name].Count == 1 &&
                    matchingAdded.Count == 1)
                {
                    string sourcePath = removedByName[name][0];
                    string targetPath = matchingAdded[0];

                    changes.RenamedElements[sourcePath] = targetPath;
                    renamedSourcePaths.Add(sourcePath);
                    renamedTargetPaths.Add(targetPath);

                    Console.WriteLine($"[SchemaComparer] Rename detected: '{sourcePath}' → '{targetPath}'");
                }
            }

            // ----------------------------------------------------------------
            // Step 3 – Detect MOVED elements: name unchanged but parent path differs.
            //          We compare shared elements whose immediate parent changed.
            // ----------------------------------------------------------------
            foreach (var sharedPath in sharedPaths)
            {
                string sourceParent = GetParentPath(sharedPath);
                // For moved detection we look at whether the element name exists
                // under a *different* parent in the target; this is more complex
                // and is handled here simply by flagging path collisions.
                // A full implementation would compare element names across all paths.
            }

            // Build a simpler moved-element detection: look for names that
            // appear in source under one parent and in target under a DIFFERENT parent.
            var sourceNameToPath = sourceElements
                .GroupBy(kv => GetElementName(kv.Key))
                .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList(), StringComparer.OrdinalIgnoreCase);

            var targetNameToPath = targetElements
                .GroupBy(kv => GetElementName(kv.Key))
                .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var name in sourceNameToPath.Keys)
            {
                if (!targetNameToPath.TryGetValue(name, out var targetPathList)) continue;

                var sourcePathList = sourceNameToPath[name];

                // If counts match and none are shared paths, check for moves.
                foreach (var sp in sourcePathList)
                {
                    foreach (var tp in targetPathList)
                    {
                        if (!string.Equals(sp, tp, StringComparison.OrdinalIgnoreCase) &&
                            !renamedSourcePaths.Contains(sp) &&
                            !renamedTargetPaths.Contains(tp) &&
                            !sourcePaths.Contains(tp) && // tp did not exist in source
                            !targetPaths.Contains(sp))   // sp does not exist in target
                        {
                            // This element moved from sp to tp.
                            if (!changes.MovedElements.ContainsKey(sp))
                            {
                                changes.MovedElements[sp] = tp;
                                Console.WriteLine($"[SchemaComparer] Move detected: '{sp}' → '{tp}'");
                            }
                        }
                    }
                }
            }

            // ----------------------------------------------------------------
            // Step 4 – Populate ADDED / REMOVED (excluding renames and moves)
            // ----------------------------------------------------------------
            var movedSourcePaths = new HashSet<string>(changes.MovedElements.Keys, StringComparer.OrdinalIgnoreCase);
            var movedTargetPaths = new HashSet<string>(changes.MovedElements.Values, StringComparer.OrdinalIgnoreCase);

            foreach (var path in addedPaths)
            {
                if (!renamedTargetPaths.Contains(path) && !movedTargetPaths.Contains(path))
                {
                    changes.AddedElements.Add(targetElements[path]);
                    Console.WriteLine($"[SchemaComparer] Added: '{path}'");
                }
            }

            foreach (var path in removedPaths)
            {
                if (!renamedSourcePaths.Contains(path) && !movedSourcePaths.Contains(path))
                {
                    changes.RemovedElements.Add(sourceElements[path]);
                    Console.WriteLine($"[SchemaComparer] Removed: '{path}'");
                }
            }

            // ----------------------------------------------------------------
            // Step 5 – Detect REORDERING among shared elements within the same parent.
            //          Group sibling elements and compare their relative Order values.
            // ----------------------------------------------------------------
            var sharedParentGroups = sharedPaths
                .GroupBy(p => GetParentPath(p));

            foreach (var group in sharedParentGroups)
            {
                var sourceOrder = group
                    .OrderBy(p => sourceElements[p].Order)
                    .Select(p => GetElementName(p))
                    .ToList();

                var targetOrder = group
                    .OrderBy(p => targetElements[p].Order)
                    .Select(p => GetElementName(p))
                    .ToList();

                if (!sourceOrder.SequenceEqual(targetOrder, StringComparer.OrdinalIgnoreCase))
                {
                    // The order within this parent group has changed – flag each element.
                    foreach (var path in group)
                    {
                        changes.ReorderedElements.Add(sourceElements[path]);
                        Console.WriteLine($"[SchemaComparer] Reordered: '{path}'");
                    }
                }
            }

            // ----------------------------------------------------------------
            // Step 6 – UNCHANGED elements (same path, same type, same order)
            // ----------------------------------------------------------------
            foreach (var path in sharedPaths)
            {
                var src = sourceElements[path];
                var tgt = targetElements[path];

                bool typeChanged  = !string.Equals(src.XsdType, tgt.XsdType, StringComparison.OrdinalIgnoreCase);
                bool orderChanged = src.Order != tgt.Order;

                if (!typeChanged && !orderChanged)
                {
                    changes.UnchangedElements.Add(src);
                }
            }

            Console.WriteLine($"[SchemaComparer] Analysis complete. " +
                $"Added={changes.AddedElements.Count}, " +
                $"Removed={changes.RemovedElements.Count}, " +
                $"Moved={changes.MovedElements.Count}, " +
                $"Reordered={changes.ReorderedElements.Count}, " +
                $"Renamed={changes.RenamedElements.Count}, " +
                $"Unchanged={changes.UnchangedElements.Count}");

            return changes;
        }

        // ====================================================================
        // Private helpers
        // ====================================================================

        /// <summary>
        /// Loads an XSD file and returns a flat dictionary of path → ElementInfo.
        /// The path uses "/" as a separator and starts at the root element name
        /// (no leading slash), e.g. "Policy/Insured/FirstName".
        /// </summary>
        private Dictionary<string, ElementInfo> LoadSchema(string xsdPath)
        {
            var elements = new Dictionary<string, ElementInfo>(StringComparer.OrdinalIgnoreCase);

            // We use XmlReader + XmlSchemaSet to load the file, then walk the
            // compiled schema element declarations.
            var schemaSet = new XmlSchemaSet();
            schemaSet.Add(null, xsdPath);

            // Compile the schema so we can walk the type hierarchy.
            // ValidationEventHandler suppresses non-fatal warnings during compilation.
            schemaSet.ValidationEventHandler += (s, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                    Console.WriteLine($"[SchemaComparer] XSD error: {e.Message}");
            };
            schemaSet.Compile();

            // Each XmlSchema in the set represents one XSD file.
            foreach (XmlSchema schema in schemaSet.Schemas())
            {
                // Walk only the top-level element declarations.
                foreach (XmlSchemaElement topLevelElement in schema.Elements.Values)
                {
                    // Recursively flatten all nested elements starting from this root.
                    FlattenElement(topLevelElement, parentPath: string.Empty, order: 0, elements: elements);
                }
            }

            return elements;
        }

        /// <summary>
        /// Recursively walks an <see cref="XmlSchemaElement"/> and its children,
        /// adding each leaf/container element to the <paramref name="elements"/> dictionary.
        /// </summary>
        /// <param name="element">The current XSD element being processed.</param>
        /// <param name="parentPath">The "/" separated path of the parent element (empty for root).</param>
        /// <param name="order">Zero-based position of this element within its parent sequence.</param>
        /// <param name="elements">The dictionary to populate.</param>
        private void FlattenElement(
            XmlSchemaElement element,
            string parentPath,
            int order,
            Dictionary<string, ElementInfo> elements)
        {
            // Build the path for this element.
            string currentPath = string.IsNullOrEmpty(parentPath)
                ? element.Name!
                : $"{parentPath}/{element.Name}";

            // Extract the XSD type name (null for anonymous complex types).
            string? xsdType = element.SchemaTypeName.IsEmpty
                ? null
                : element.SchemaTypeName.Name;

            // Determine whether this is a complex type (container).
            bool isComplex = element.ElementSchemaType is XmlSchemaComplexType;

            // Extract any xs:annotation/xs:documentation text.
            string? documentation = ExtractDocumentation(element.Annotation);

            // Record this element.
            var info = new ElementInfo
            {
                Name          = element.Name!,
                Path          = currentPath,
                XsdType       = xsdType,
                IsComplex     = isComplex,
                Order         = order,
                IsRequired    = element.MinOccurs >= 1,
                MaxOccurs     = element.MaxOccursString == "unbounded" ? 0 : (int)element.MaxOccurs,
                Documentation = documentation
            };

            elements[currentPath] = info;

            // If this element has a complex type, recurse into its children.
            if (element.ElementSchemaType is XmlSchemaComplexType complexType)
            {
                var children = GetChildElements(complexType);
                for (int i = 0; i < children.Count; i++)
                {
                    FlattenElement(children[i], currentPath, i, elements);
                }
            }
        }

        /// <summary>
        /// Extracts child element declarations from a complex type's content model.
        /// Handles sequences, choices, and all compositors.
        /// </summary>
        private List<XmlSchemaElement> GetChildElements(XmlSchemaComplexType complexType)
        {
            var result = new List<XmlSchemaElement>();

            // The content model is either a sequence, choice, or all group particle.
            if (complexType.ContentTypeParticle is XmlSchemaGroupBase groupBase)
            {
                CollectElements(groupBase, result);
            }

            return result;
        }

        /// <summary>
        /// Recursively collects <see cref="XmlSchemaElement"/> items from a compositor
        /// (xs:sequence, xs:choice, or xs:all), resolving nested groups if present.
        /// </summary>
        private void CollectElements(XmlSchemaGroupBase groupBase, List<XmlSchemaElement> result)
        {
            foreach (XmlSchemaParticle particle in groupBase.Items)
            {
                if (particle is XmlSchemaElement childElement)
                {
                    // Direct child element – add it.
                    result.Add(childElement);
                }
                else if (particle is XmlSchemaGroupBase nestedGroup)
                {
                    // Nested sequence/choice/all – recurse into it.
                    CollectElements(nestedGroup, result);
                }
                else if (particle is XmlSchemaGroupRef groupRef)
                {
                    // Referenced group – resolve it.
                    if (groupRef.Particle is XmlSchemaGroupBase resolvedGroup)
                        CollectElements(resolvedGroup, result);
                }
            }
        }

        /// <summary>
        /// Extracts text from an <c>xs:annotation/xs:documentation</c> node.
        /// Returns null when no annotation is present.
        /// </summary>
        private static string? ExtractDocumentation(XmlSchemaAnnotation? annotation)
        {
            if (annotation == null) return null;

            foreach (XmlSchemaObject item in annotation.Items)
            {
                if (item is XmlSchemaDocumentation doc && doc.Markup != null)
                {
                    // Markup is an array of XmlNode – join their text content.
                    return string.Concat(doc.Markup.Select(n => n?.InnerText)).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Returns just the element name (the last segment) from a "/" separated path.
        /// E.g., "Policy/Insured/FirstName" → "FirstName".
        /// </summary>
        private static string GetElementName(string path)
        {
            int lastSlash = path.LastIndexOf('/');
            return lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
        }

        /// <summary>
        /// Returns the parent path from a "/" separated path.
        /// E.g., "Policy/Insured/FirstName" → "Policy/Insured".
        /// Returns an empty string for root-level elements.
        /// </summary>
        private static string GetParentPath(string path)
        {
            int lastSlash = path.LastIndexOf('/');
            return lastSlash >= 0 ? path.Substring(0, lastSlash) : string.Empty;
        }
    }
}
