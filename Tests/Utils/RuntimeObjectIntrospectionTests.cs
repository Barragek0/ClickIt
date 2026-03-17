using ClickIt.Utils;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class RuntimeObjectIntrospectionTests
    {
        private sealed class NestedNode
        {
            public string Name { get; set; } = "node";
            public int Value { get; set; } = 42;
        }

        private sealed class RootNode
        {
            public int Id { get; set; } = 7;
            public string Label { get; set; } = "root";
            public NestedNode Child { get; set; } = new();
            public List<int> Values { get; set; } = [1, 2, 3, 4];
        }

        [TestMethod]
        public void BuildReport_IncludesTitleAndRootUnavailable_WhenNull()
        {
            string report = RuntimeObjectIntrospection.BuildReport(
                null,
                new RuntimeObjectIntrospectionOptions("Any Report", 2, 3, []));

            report.Should().Contain("--- Any Report ---");
            report.Should().Contain("Root: unavailable");
        }

        [TestMethod]
        public void BuildReport_UsesPriorityMembersBeforeOthers()
        {
            var root = new RootNode();

            string report = RuntimeObjectIntrospection.BuildReport(
                root,
                new RuntimeObjectIntrospectionOptions(
                    Title: "Priority Report",
                    MaxDepth: 1,
                    MaxCollectionItems: 2,
                    PriorityMembers: ["Label", "Id"]));

            int labelIndex = report.IndexOf("Root.Label", StringComparison.Ordinal);
            int idIndex = report.IndexOf("Root.Id", StringComparison.Ordinal);
            int childIndex = report.IndexOf("Root.Child", StringComparison.Ordinal);

            labelIndex.Should().BeGreaterThanOrEqualTo(0);
            idIndex.Should().BeGreaterThanOrEqualTo(0);
            childIndex.Should().BeGreaterThanOrEqualTo(0);
            labelIndex.Should().BeLessThan(childIndex);
            idIndex.Should().BeLessThan(childIndex);
        }

        [TestMethod]
        public void BuildReport_TruncatesCollectionOutput_WhenLimitExceeded()
        {
            var root = new RootNode();

            string report = RuntimeObjectIntrospection.BuildReport(
                root,
                new RuntimeObjectIntrospectionOptions(
                    Title: "Collection Report",
                    MaxDepth: 2,
                    MaxCollectionItems: 2,
                    PriorityMembers: []));

            report.Should().Contain("Root.Values: collection output truncated");
        }
    }
}