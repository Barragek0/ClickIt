using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Reflection;
using System.Threading;
using ClickIt.Tests;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class ElementServiceThreadLocalTests
    {
        private MethodInfo GetGetThreadLocalListMethod()
        {
            // Prefer Type.GetType to avoid forcing the runtime to load dependent assemblies
            var t = Type.GetType("ClickIt.Services.ElementService, ClickIt");
            if (t == null)
            {
                Assert.Inconclusive("ElementService type not resolvable in this environment; skipping ElementService thread-local tests.");
            }
            var m = t.GetMethod("GetThreadLocalList", BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull();
            return m!;
        }

        [TestMethod]
        public void GetThreadLocalList_ReturnsSameInstanceOnSameThread()
        {
            var m = GetGetThreadLocalListMethod();
            try
            {
                var a = m.Invoke(null, Array.Empty<object>());
                var b = m.Invoke(null, Array.Empty<object>());
                a.Should().NotBeNull();
                b.Should().NotBeNull();
                ReferenceEquals(a, b).Should().BeTrue();
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Assert.Inconclusive("ElementService depends on ExileCore types which are not available: " + ex.Message);
            }
        }

        [TestMethod]
        public void GetThreadLocalList_ReturnsDifferentInstancesOnDifferentThreads()
        {
            var m = GetGetThreadLocalListMethod();
            try
            {
                var main = m.Invoke(null, Array.Empty<object>());
                object other = null;
                Exception threadEx = null;
                var mre = new ManualResetEvent(false);
                var t = new Thread(() =>
                {
                    try
                    {
                        other = m.Invoke(null, Array.Empty<object>());
                    }
                    catch (Exception e)
                    {
                        threadEx = e;
                    }
                    finally
                    {
                        mre.Set();
                    }
                });
                t.Start();
                mre.WaitOne(2000);
                if (threadEx != null)
                {
                    if (threadEx is System.Reflection.TargetInvocationException tie && tie.InnerException is System.IO.FileNotFoundException)
                    {
                        Assert.Inconclusive("ElementService depends on ExileCore types which are not available in worker thread.");
                    }
                    throw threadEx;
                }

                main.Should().NotBeNull();
                other.Should().NotBeNull();
                ReferenceEquals(main, other).Should().BeFalse();
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Assert.Inconclusive("ElementService depends on ExileCore types which are not available: " + ex.Message);
            }
        }
    }
    [TestClass]
    public class ElementServiceTests
    {
        [TestMethod]
        public void GetElementByString_ReturnsElement_WhenTextContains()
        {
            var root = MockElementService.CreateBasicElement();

            var found = MockElementService.GetElementByString(root, "basic");

            found.Should().NotBeNull();
            found.Text.Should().Contain("basic");
        }

        [TestMethod]
        public void GetElementsByStringContains_ReturnsList_WhenMatches()
        {
            var root = MockElementService.CreateBasicElement();

            var results = MockElementService.GetElementsByStringContains(root, "basic");

            results.Should().NotBeNull();
            results.Should().HaveCountGreaterThan(0);
            results[0].Text.Should().Contain("basic");
        }

        [TestMethod]
        public void IsValidElement_ReturnsFalse_ForNullOrEmptyText()
        {
            MockElement nullEl = null;
            MockElement emptyEl = new MockElement { Text = "" };

            MockElementService.IsValidElement(nullEl).Should().BeFalse();
            MockElementService.IsValidElement(emptyEl).Should().BeFalse();
        }

        [TestMethod]
        public void CreateAltarElement_IncludesModsAndType()
        {
            var el = MockElementService.CreateAltarElement("PlayerDropsItemsOnDeath", new[] { "Up1", "Up2" }, new[] { "-Down1" });

            el.Should().NotBeNull();
            el.Text.Should().Contain("PlayerDropsItemsOnDeath");
            el.Text.Should().Contain("Up1");
            el.Text.Should().Contain("-Down1");
        }

        [TestMethod]
        public void GetElementCenter_ReturnsPosition_WhenSet()
        {
            var el = MockElementService.CreateElementAtPosition(123.4f, 567.8f);

            var center = MockElementService.GetElementCenter(el);

            center.X.Should().BeApproximately(123.4f, 0.01f);
            center.Y.Should().BeApproximately(567.8f, 0.01f);
        }
    }
}
