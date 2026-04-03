namespace ClickIt.Tests.UI
{
    [TestClass]
    public class RuntimeObjectIntrospectionTests
    {
        [TestMethod]
        public void GetFileNameForProfile_ReturnsExpectedNames()
        {
            RuntimeObjectIntrospection.GetFileNameForProfile(IntrospectionProfile.Default).Should().Be("memory.dat");
            RuntimeObjectIntrospection.GetFileNameForProfile(IntrospectionProfile.StructureFirst).Should().Be("structure.dat");
            RuntimeObjectIntrospection.GetFileNameForProfile(IntrospectionProfile.Full).Should().Be("full.dat");
        }

        [TestMethod]
        public void GetOptionsForProfile_ReturnsExpectedOptionShapes()
        {
            var defaultOptions = RuntimeObjectIntrospection.GetOptionsForProfile(IntrospectionProfile.Default);
            var structureOptions = RuntimeObjectIntrospection.GetOptionsForProfile(IntrospectionProfile.StructureFirst);
            var fullOptions = RuntimeObjectIntrospection.GetOptionsForProfile(IntrospectionProfile.Full);

            defaultOptions.Title.Should().Be("Runtime Object Introspection");
            structureOptions.Title.Should().Be("Structure-First Memory Dump");
            fullOptions.Title.Should().Be("Full Game Memory Dump");
            fullOptions.MaxTotalNodes.Should().BeGreaterThan(defaultOptions.MaxTotalNodes);
        }

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
        public void WriteMemorySnapshotToFile_UsesProfileOptions()
        {
            string path = GetTempFilePath();
            try
            {
                string full = RuntimeObjectIntrospection.WriteMemorySnapshotToFile(
                    new { A = 1 },
                    path,
                    IntrospectionProfile.StructureFirst);

                File.Exists(full).Should().BeTrue();
                File.ReadAllText(full).Should().Contain("Structure-First Memory Dump");
            }
            finally
            {
                SafeDelete(path);
            }
        }

        [TestMethod]
        public void WriteMemorySnapshotCoroutine_CompletesAndReportsProgress_ForNullRoot()
        {
            string path = GetTempFilePath();
            var progress = new List<int>();
            string? completionPath = null;
            string? completionError = null;

            try
            {
                IEnumerator routine = RuntimeObjectIntrospection.WriteMemorySnapshotCoroutine(
                    null,
                    path,
                    IntrospectionProfile.Default,
                    (p, e) =>
                    {
                        completionPath = p;
                        completionError = e;
                    },
                    p => progress.Add(p),
                    nodeBudgetPerYield: 3);

                while (routine.MoveNext())
                {
                }

                completionError.Should().BeNull();
                completionPath.Should().NotBeNullOrWhiteSpace();
                progress.Should().Contain(0);
                progress.Should().Contain(100);
                File.ReadAllText(path).Should().Contain("Root: unavailable");
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

        private static string GetTempFilePath()
            => Path.Combine(Path.GetTempPath(), "clickit-introspection-" + Guid.NewGuid().ToString("N") + ".txt");

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
    }
}