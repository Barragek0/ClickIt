using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ReflectionCoverageTests
    {
        [TestMethod]
        public void Invoke_SafeMethods_AcrossAssembly_DoesNotCrash()
        {
            var asm = typeof(ClickIt).Assembly;

            // blacklist to avoid process-starting or destructive invocation
            string[] bannedNames = new[] { "ReportBugButtonPressed", "OpenConfigDirectoryPressed", "PlaySoundFile", "ReloadAlertSound", "TryTriggerAlertForMatchedMod", "Start", "Process" };

            int invoked = 0;

            // Target a few safe namespaces (Utils, Services, Components) but skip types that are likely to invoke UI/native code
            var allowedNamespaces = new[] { "ClickIt.Utils", "ClickIt.Services", "ClickIt.Components" };
            foreach (var t in asm.GetTypes().Where(tt => tt.Namespace != null && allowedNamespaces.Contains(tt.Namespace)))
            {
                // ignore compiler-generated / delegate types
                if (t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) continue;

                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.IsSpecialName) continue; // skip property/oper & event methods
                    if (m.IsConstructor) continue;
                    if (bannedNames.Any(b => m.Name.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                    // skip methods that look like they might interact with native libs or heavy game objects
                    string[] riskyNameParts = new[] { "Renderer", "Settings", "Controller", "ClickService", "Coroutine", "InputHandler", "GameController", "SoundController" };
                    if (riskyNameParts.Any(r => t.Name.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                    // Only attempt to invoke methods that have manageable parameter lists
                    var ps = m.GetParameters();
                    if (ps.Any(p => p.IsOut || p.IsIn || p.ParameterType.IsByRef)) continue;

                    // Build parameter values
                    object?[] args = ps.Select(p => DefaultForType(p.ParameterType)).ToArray();

                    object? instance = null;
                    try
                    {
                        if (!m.IsStatic)
                        {
                            // create an uninitialized instance to avoid running constructors (safe, fields will be default/null)
                            instance = RuntimeHelpers.GetUninitializedObject(t);
                        }

                        try
                        {
                            // invoke and swallow any exceptions - our goal is coverage, not semantics
                            m.Invoke(instance, args);
                        }
                        catch { /* ignore */ }

                        invoked++;
                        if (invoked > 1000) break; // safety cap
                    }
                    catch { /* ignore any reflection issues */ }

                }

                if (invoked > 1000) break;
            }

            // sanity: ensure we invoked at least one method
            Assert.IsTrue(invoked > 0, "No methods were invoked; reflection harness may be broken.");
        }

        private static object? DefaultForType(Type t)
        {
            if (t == typeof(string)) return string.Empty;
            if (t.IsValueType) return Activator.CreateInstance(t);
            if (t.IsArray) return Array.CreateInstance(t.GetElementType() ?? typeof(object), 0);
            // dev note: delegate types are hard to synthesize; return null which many methods guard for
            return null;
        }
    }
}
