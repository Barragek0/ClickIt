namespace ClickIt.Tests.UI
{
    [TestClass]
    public class RuntimeObjectIntrospectionTests
    {
        [TestMethod]
        public void BuildReport_ReturnsUnavailable_WhenRootIsNull()
        {
            string report = RuntimeObjectIntrospection.BuildReport(null, RuntimeObjectIntrospectionOptions.Default);

            report.Should().Contain("Runtime Object Introspection");
            report.Should().Contain("Root: unavailable");
        }

        [TestMethod]
        public void BuildReport_UsesCustomTitle_WhenProvided()
        {
            var options = RuntimeObjectIntrospectionOptions.Default with { Title = "Custom" };

            string report = RuntimeObjectIntrospection.BuildReport(new { Value = 5 }, options);

            report.Should().Contain("--- Custom ---");
            report.Should().Contain("Root.Value");
        }

        [TestMethod]
        public void BuildReport_FallsBackToDefaultTitle_WhenBlankProvided()
        {
            var options = RuntimeObjectIntrospectionOptions.Default with { Title = "   " };

            string report = RuntimeObjectIntrospection.BuildReport(new { Value = 1 }, options);

            report.Should().Contain("--- Runtime Object Introspection ---");
        }

        [TestMethod]
        public void BuildReport_HandlesCycles_AndTruncatesCollections()
        {
            var root = new CycleNode { Name = "a" };
            root.Next = root;

            var options = RuntimeObjectIntrospectionOptions.Default with
            {
                MaxCollectionItems = 2,
                MaxDepth = 8
            };

            string report = RuntimeObjectIntrospection.BuildReport(new { Root = root, Items = new[] { 1, 2, 3, 4 } }, options);

            report.Should().Contain("(cycle)");
            report.Should().Contain("collection output truncated");
        }

        [TestMethod]
        public void BuildReport_DeduplicatesRemoteMemoryObjects_ByUnderlyingAddress()
        {
            // Game memory wrappers are re-created per read, so the same underlying element appears as a distinct CLR instance each time; two wrappers with the same Address must count as a cycle.
            LabelOnGround first = CreateOpaqueLabel(0x1234);
            LabelOnGround sameAddress = CreateOpaqueLabel(0x1234);
            LabelOnGround otherAddress = CreateOpaqueLabel(0x5678);

            string report = RuntimeObjectIntrospection.BuildReport(
                new { Root = first, Again = sameAddress, Other = otherAddress },
                RuntimeObjectIntrospectionOptions.Default with { MaxDepth = 4, MaxTotalNodes = 1000 });

            report.Should().Contain("Root");
            report.Should().Contain("Root.Again");
            report.Should().Contain("(cycle)");
            report.Should().Contain("Root.Other");
        }

        [TestMethod]
        public void BuildReport_StopsAtMaxDepth_AndNodeBudget()
        {
            var deep = new DeepNode { Id = 1 };
            deep.Child = new DeepNode { Id = 2, Child = new DeepNode { Id = 3 } };

            var byDepth = RuntimeObjectIntrospection.BuildReport(
                deep,
                RuntimeObjectIntrospectionOptions.Default with { MaxDepth = 1, MaxTotalNodes = 100 });

            var byNodes = RuntimeObjectIntrospection.BuildReport(
                deep,
                RuntimeObjectIntrospectionOptions.Default with { MaxDepth = 100, MaxTotalNodes = 1 });

            byDepth.Should().Contain("max depth reached");
            byNodes.Should().Contain("node budget reached");
        }

        [TestMethod]
        public void BuildReport_ReportsUnavailableMember_WhenGetterThrows()
        {
            string report = RuntimeObjectIntrospection.BuildReport(
                new ThrowingGetterNode(),
                RuntimeObjectIntrospectionOptions.Default);

            report.Should().Contain("<unavailable>");
            report.Should().Contain("Ok");
        }

        [TestMethod]
        public void BuildReport_CanIncludeNonPublicMembers()
        {
            var options = RuntimeObjectIntrospectionOptions.Default with { IncludeNonPublicMembers = true };

            string report = RuntimeObjectIntrospection.BuildReport(new NonPublicMemberNode(), options);

            report.Should().Contain("HiddenValue");
        }

        [TestMethod]
        public void FormatValue_ReturnsNullLiteral_StringAndToStringResults()
        {
            RuntimeObjectIntrospectionValueFormatter.FormatValue(null).Should().Be("null");
            RuntimeObjectIntrospectionValueFormatter.FormatValue("hello").Should().Be("hello");
            RuntimeObjectIntrospectionValueFormatter.FormatValue(new FormatValueNode()).Should().Be("node-value");
        }

        [TestMethod]
        public void FormatValue_TruncatesOnlyWhenMaxLengthIsPositive()
        {
            RuntimeObjectIntrospectionValueFormatter.FormatValue("abcdef", maxLen: 3).Should().Be("abc...");
            RuntimeObjectIntrospectionValueFormatter.FormatValue("abcdef", maxLen: 0).Should().Be("abcdef");
            RuntimeObjectIntrospectionValueFormatter.FormatValue("abcdef", maxLen: -1).Should().Be("abcdef");
        }

        [TestMethod]
        public void WriteReportToFile_WritesExpectedContent()
        {
            string path = GetTempFilePath();
            try
            {
                string full = RuntimeObjectIntrospection.WriteReportToFile(
                    new { A = 1, B = "x" },
                    path,
                    RuntimeObjectIntrospectionOptions.Default);

                File.Exists(full).Should().BeTrue();
                string text = File.ReadAllText(full);
                text.Should().Contain("Root.A");
                text.Should().Contain("Root.B");
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [TestMethod]
        public void WriteReportToFileCoroutine_ResilientToCallbackExceptions()
        {
            string path = GetTempFilePath();
            string? completionPath = null;
            string? completionError = null;

            try
            {
                IEnumerator routine = RuntimeObjectIntrospection.WriteReportToFileCoroutine(
                    new { Items = new[] { 1, 2, 3, 4, 5, 6 } },
                    path,
                    RuntimeObjectIntrospectionOptions.Default with { MaxCollectionItems = 2, MaxTotalNodes = 30 },
                    (p, e) =>
                    {
                        completionPath = p;
                        completionError = e;
                        throw new InvalidOperationException("ignored completion callback error");
                    },
                    _ => throw new InvalidOperationException("ignored progress callback error"),
                    nodeBudgetPerYield: 1);

                while (routine.MoveNext())
                {
                }

                completionPath.Should().NotBeNullOrWhiteSpace();
                completionError.Should().BeNull();
                File.Exists(path).Should().BeTrue();
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [TestMethod]
        public void BuildReport_SkipsConfiguredMembers()
        {
            var options = RuntimeObjectIntrospectionOptions.Default with
            {
                SkipMemberNames = ["Big"],
            };
            string report = RuntimeObjectIntrospection.BuildReport(new { Big = 1, Small = 2 }, options);

            report.Should().Contain("Root.Big: <skipped>");
            report.Should().Contain("Root.Small: 2");
        }

        [TestMethod]
        public void WriteReportCoroutine_StreamsToWriter_AndReportsProgressAndTotalNodes()
        {
            using MemoryStream stream = new();
            using StreamWriter writer = new(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var progress = new List<int>();
            int totalNodes = 0;

            IEnumerator routine = RuntimeObjectIntrospection.WriteReportCoroutine(
                new { A = 1, Items = new[] { 1, 2, 3, 4, 5 } },
                writer,
                RuntimeObjectIntrospectionOptions.Default with { MaxCollectionItems = 2, MaxTotalNodes = 40 },
                onProgress: p => progress.Add(p),
                onTotalNodes: n => totalNodes = n,
                nodeBudgetPerYield: 1);

            while (routine.MoveNext())
            {
            }
            writer.Flush();

            string text = Encoding.UTF8.GetString(stream.ToArray());
            text.Should().Contain("Root.A");
            text.Should().Contain("Root.Items");
            progress.Should().Contain(100);
            totalNodes.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void WriteReportCoroutine_WithMeasureProgressTotal_ReportsAgainstMeasuredTotal()
        {
            using MemoryStream stream = new();
            using StreamWriter writer = new(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var progress = new List<int>();

            // A small graph with a huge fixed projection: without measuring, the bar would sit near 0 and jump to 100; with measurement the write phase climbs toward 100 against the real count.
            IEnumerator routine = RuntimeObjectIntrospection.WriteReportCoroutine(
                new { A = 1, Items = new[] { 1, 2, 3, 4, 5, 6, 7, 8 } },
                writer,
                RuntimeObjectIntrospectionOptions.Default with
                {
                    MaxTotalNodes = 100000,
                    MaxElapsedMs = 60000,
                    ProgressNodeTotal = 100000,
                    MaxCollectionItems = 100,
                },
                onProgress: p => progress.Add(p),
                nodeBudgetPerYield: 1,
                measureProgressTotal: true);

            while (routine.MoveNext())
            {
            }
            writer.Flush();

            progress.Should().Contain(50, "the count phase ends at the midpoint marker");
            progress.Should().Contain(100);
            progress.Should().Contain(p => p > 50 && p < 100, "the write phase must climb against the measured total, not the fixed projection");
        }

        [TestMethod]
        public void BuildReport_IncludesExtraChildrenFromProvider()
        {
            object root = new { A = 1 };
            var options = RuntimeObjectIntrospectionOptions.Default with
            {
                ExtraChildrenProvider = value => value == root
                    ? [("ExtraChild", new { Value = 7 })]
                    : null,
            };

            string report = RuntimeObjectIntrospection.BuildReport(root, options);

            report.Should().Contain("Root.ExtraChild.Value: 7");
        }

        [TestMethod]
        public void WriteReportCoroutine_ProgressUsesProgressNodeTotal_NotUnlimitedNodeCap()
        {
            using MemoryStream stream = new();
            using StreamWriter writer = new(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var progress = new List<int>();

            IEnumerator routine = RuntimeObjectIntrospection.WriteReportCoroutine(
                Enumerable.Range(0, 100).ToArray(),
                writer,
                RuntimeObjectIntrospectionOptions.Default with
                {
                    MaxTotalNodes = int.MaxValue,
                    MaxElapsedMs = int.MaxValue,
                    ProgressNodeTotal = 100,
                    MaxCollectionItems = 100,
                },
                onProgress: p => progress.Add(p),
                nodeBudgetPerYield: 5);

            while (routine.MoveNext())
            {
            }
            writer.Flush();

            progress.Should().ContainInOrder(0, 25, 50, 75);
            progress.Should().Contain(100);
        }

        private static string GetTempFilePath()
            => Path.Combine(Path.GetTempPath(), "clickit-introspection-" + Guid.NewGuid().ToString("N") + ".txt");

        private static LabelOnGround CreateOpaqueLabel(long address)
        {
            LabelOnGround label = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            System.Reflection.PropertyInfo addressProperty = typeof(RemoteMemoryObject).GetProperty(
                nameof(RemoteMemoryObject.Address),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
            addressProperty!.SetValue(label, address);
            return label;
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private sealed class CycleNode
        {
            public string Name { get; set; } = string.Empty;
            public CycleNode? Next { get; set; }
        }

        private sealed class DeepNode
        {
            public int Id { get; set; }
            public DeepNode? Child { get; set; }
        }

        private sealed class ThrowingGetterNode
        {
            public string Ok => "ok";
            public string Boom => throw new InvalidOperationException("boom");
        }

        private sealed class NonPublicMemberNode
        {
            private string HiddenValue => "secret";
            public string VisibleValue => "visible";
        }

        private sealed class FormatValueNode
        {
            public override string ToString() => "node-value";
        }
    }
}
