using System.Collections.Generic;

namespace ClickIt.Utils
{
    // Adapter-focused test seams to make LabelUtils traversal testable without ExileCore.Element
    internal static partial class LabelUtils
    {
        internal static bool ElementContainsAnyStringsForTests(Services.IElementAdapter? root, IEnumerable<string> patterns)
        {
            if (root == null) return false;
            var patList = patterns as string[] ?? System.Linq.Enumerable.ToArray(patterns);
            if (patList.Length == 0) return false;

            var stack = new Stack<Services.IElementAdapter>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var el = stack.Pop();
                var text = el.GetText(512);
                if (!string.IsNullOrEmpty(text))
                {
                    foreach (var p in patList)
                    {
                        if (text.Contains(p)) return true;
                    }
                }

                foreach (var c in EnumerateChildren(el)) stack.Push(c);
            }

            return false;
        }

        internal static List<Services.IElementAdapter> GetElementsByStringContainsForTests(Services.IElementAdapter? label, string str)
        {
            var list = new List<Services.IElementAdapter>();
            if (label == null) return list;

            var rootText = label.GetText(512);
            if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str)) list.Add(label);

            foreach (var child in EnumerateChildren(label))
            {
                var childText = child.GetText(512);
                if (!string.IsNullOrEmpty(childText) && childText.Contains(str)) list.Add(child);
            }

            return list;
        }

        internal static Services.IElementAdapter? GetElementByStringForTests(Services.IElementAdapter? root, string str)
        {
            if (root == null) return null;
            var stack = new Stack<Services.IElementAdapter>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var el = stack.Pop();
                var text = el.GetText(512);
                if (text != null && text.Equals(str)) return el;

                foreach (var c in EnumerateChildren(el)) stack.Push(c);
            }

            return null;
        }

        // enumerate children by probing GetChildFromIndices(containerIndex, childIndex)
        private static IEnumerable<Services.IElementAdapter> EnumerateChildren(Services.IElementAdapter parent)
        {
            // Some adapters may return the same child for multiple container indexes.
            // Keep track of yielded instances to avoid duplicates.
            var seen = new HashSet<Services.IElementAdapter>();
            for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
            {
                int childIndex = 0;
                while (true)
                {
                    var c = parent.GetChildFromIndices(containerIndex, childIndex);
                    if (c == null) break;
                    if (seen.Add(c)) yield return c;
                    childIndex++;
                }
            }
        }
    }
}
