using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtils_StringCasesTests
    {
        [DataTestMethod]
        [DataRow("DelveMineral/col1", true)]
        [DataRow("some/Delve/Objects/Encounter/abc", true)]
        [DataRow("CleansingFireAltar/something", true)]
        [DataRow("copper_altar", true)]
        [DataRow("Leagues/Ritual/blah", true)]
        [DataRow("not/a/match", false)]
        [DataRow("", false)]
        [DataRow("DELVeMINERAL", false)] // case-sensitive contains -> should be false
        public void IsPathForClickableObject_VariousPatterns(string path, bool expected)
        {
            LabelUtils.IsPathForClickableObject(path).Should().Be(expected);
        }
    }
}
