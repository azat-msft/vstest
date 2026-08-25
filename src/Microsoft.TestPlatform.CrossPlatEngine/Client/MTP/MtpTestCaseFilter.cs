// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.Common.Filtering;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client.MTP;

/// <summary>
/// Evaluates a vstest <c>/TestCaseFilter</c> expression against the tests discovered from a
/// Microsoft.Testing.Platform (MTP) application.
///
/// MTP has no notion of the vstest filter expression - it addresses tests by node uid - so the filter
/// cannot be pushed down to the server. It is resolved here instead, against the discovered test cases,
/// with the same expression semantics (traits, boolean operators, operators such as <c>~</c> and
/// <c>!=</c>) as the classic path.
/// </summary>
internal static class MtpTestCaseFilter
{
    // Names under which the predefined TestCase members are exposed to a filter expression. DisplayName
    // is exposed twice: vstest labels that property "Name", while filters conventionally spell it
    // "DisplayName", and both spellings are in use.
    private const string DisplayNameLabel = "DisplayName";
    private const string NameLabel = "Name";
    private const string FullyQualifiedNameLabel = "FullyQualifiedName";
    private const string SourceLabel = "Source";
    private const string FilePathLabel = "FilePath";

    /// <summary>
    /// The label of the pseudo-property that carries the trait collection. Traits are exposed
    /// individually by trait name, so the collection itself must not be exposed as one value - matching
    /// the classic path, which skips it for the same reason.
    /// </summary>
    private const string TraitsLabel = "Traits";

    /// <summary>
    /// Parses <paramref name="filter"/> into an expression that can be matched against test cases.
    /// </summary>
    /// <exception cref="TestPlatformFormatException">The filter expression is not valid.</exception>
    public static TestCaseFilterExpression CreateExpression(string filter, FilterOptions? filterOptions)
    {
        var filterWrapper = new FilterExpressionWrapper(filter, filterOptions);
        return filterWrapper.ParseError.IsNullOrEmpty()
            ? new TestCaseFilterExpression(filterWrapper)
            : throw new TestPlatformFormatException(filterWrapper.ParseError, filter);
    }

    /// <summary>
    /// Returns the subset of <paramref name="tests"/> matching <paramref name="expression"/>.
    /// </summary>
    public static List<TestCase> Filter(IEnumerable<TestCase> tests, TestCaseFilterExpression expression)
    {
        var matched = new List<TestCase>();
        foreach (TestCase testCase in tests)
        {
            if (expression.MatchTestCase(testCase, CreatePropertyValueProvider(testCase)))
            {
                matched.Add(testCase);
            }
        }

        return matched;
    }

    /// <summary>
    /// Returns the union of the property names <see cref="CreatePropertyValueProvider"/> would expose
    /// across <paramref name="tests"/>, for reporting the properties a filter names that no test
    /// carries. Built from the provider itself so the two can never drift apart.
    /// </summary>
    public static List<string> SupportedProperties(IEnumerable<TestCase> tests)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (TestCase testCase in tests)
        {
            foreach (string name in CollectPropertyNames(testCase))
            {
                names.Add(name);
            }
        }

        return [.. names];
    }

    /// <summary>
    /// Builds the property lookup a <see cref="TestCaseFilterExpression"/> evaluates a filter against.
    ///
    /// Every property carried on the test case is exposed by its label, plus the <c>DisplayName</c> alias
    /// (vstest labels that property "Name") and every trait by trait name. Exposing only
    /// <see cref="TestCase.FullyQualifiedName"/> - which is what a naive provider ends up doing - makes
    /// filters such as <c>Source~Foo</c>, <c>TestCategory=Fast</c> or <c>Priority=1</c> silently match
    /// nothing rather than error, so the breadth here is the point.
    /// </summary>
    internal static Func<string, object?> CreatePropertyValueProvider(TestCase testCase)
    {
        Dictionary<string, List<string>> properties = BuildProperties(testCase);

        return name => properties.TryGetValue(name, out List<string>? values)
            ? values.Count == 1 ? values[0] : values.ToArray()
            : null;
    }

    /// <summary>
    /// The property names <see cref="CreatePropertyValueProvider"/> would expose for one test case.
    /// </summary>
    private static IEnumerable<string> CollectPropertyNames(TestCase testCase)
        => BuildProperties(testCase).Keys;

    private static Dictionary<string, List<string>> BuildProperties(TestCase testCase)
    {
        var properties = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void Add(string? key, string? value)
        {
            if (key.IsNullOrEmpty() || value is null)
            {
                return;
            }

            if (!properties.TryGetValue(key, out List<string>? values))
            {
                values = [];
                properties[key] = values;
            }

            // A predefined member is enumerated by TestCase.Properties AND added explicitly below, so
            // this dedupe runs on every call, not just for a round-tripped test case. Removing it turns
            // single values into two-element arrays and silently changes `=` semantics.
            if (!values.Contains(value, StringComparer.Ordinal))
            {
                values.Add(value);
            }
        }

        foreach (TestProperty property in testCase.Properties)
        {
            if (property.Label.Equals(TraitsLabel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (testCase.GetPropertyValue(property))
            {
                case null:
                    break;

                // Multi-valued properties (e.g. TestCategory) must contribute one value each, or a
                // filter would have to match the rendering of the whole array.
                case string[] multiValue:
                    foreach (string item in multiValue)
                    {
                        Add(property.Label, item);
                    }

                    break;

                case { } value:
                    Add(property.Label, value.ToString());
                    break;
            }
        }

        // TestCase.Properties is overridden to concatenate the predefined TestCaseProperties
        // (FullyQualifiedName, DisplayName, Source, CodeFilePath, Id, LineNumber, ExecutorUri) onto the
        // property store, so the loop above already enumerated them and GetPropertyValue routed them to
        // the backing fields. The adds below are therefore mostly re-adds, and the dedupe inside Add is
        // what keeps them harmless: without it Source, FullyQualifiedName and Name would each become a
        // two-element array, which changes the semantics of `=` from equality to "any value equals".
        // Only the DisplayName alias is genuinely new (vstest labels that property "Name"), but the
        // rest are kept explicitly so the provider stays correct if that override ever narrows.
        Add(FullyQualifiedNameLabel, testCase.FullyQualifiedName);
        Add(NameLabel, testCase.DisplayName);
        Add(DisplayNameLabel, testCase.DisplayName);
        Add(SourceLabel, testCase.Source);
        Add(FilePathLabel, testCase.CodeFilePath);

        foreach (Trait trait in testCase.Traits)
        {
            Add(trait.Name, trait.Value);
        }

        return properties;
    }
}
