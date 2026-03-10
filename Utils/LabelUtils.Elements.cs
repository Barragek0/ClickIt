using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Utils
{
    internal static partial class LabelUtils
    {
        private static readonly ThreadLocal<List<Element>> _threadLocalElementsList = new(() => []);

        private static List<Element> GetThreadLocalElementsList()
        {
            return _threadLocalElementsList.Value ?? [];
        }

        /// <summary>
        /// Clears the ThreadLocal storage to prevent issues during DLL reload.
        /// </summary>
        public static void ClearThreadLocalStorage()
        {
            _threadLocalElementsList.Value?.Clear();
        }

        public static List<Element> GetElementsByStringContains(Element? label, string str)
        {
            var elementsList = GetThreadLocalElementsList();
            elementsList.Clear();
            if (label == null)
                return elementsList;

            string rootText = label.GetText(512);
            if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str))
                elementsList.Add(label);

            for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
            {
                Element? container = label.GetChildAtIndex(containerIndex);
                if (container == null)
                    continue;

                IList<Element> children = container.Children;
                if (children == null)
                    continue;

                for (int i = 0; i < children.Count; i++)
                {
                    Element? child = children[i];
                    if (child == null)
                        continue;

                    string childText = child.GetText(512);
                    if (!string.IsNullOrEmpty(childText) && childText.Contains(str))
                        elementsList.Add(child);
                }
            }

            return elementsList;
        }

        public static Element? GetElementByString(Element? root, string str)
        {
            if (root == null)
                return null;

            Element? found = null;
            Stack<Element> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Element el = stack.Pop();
                string text = el.GetText(512);
                if (text != null && text.Equals(str))
                {
                    found = el;
                    break;
                }

                IList<Element> children = el.Children;
                if (children != null)
                {
                    foreach (Element c in children)
                    {
                        if (c != null)
                            stack.Push(c);
                    }
                }
            }

            return found;
        }

        public static bool ElementContainsAnyStrings(Element? root, IEnumerable<string> patterns)
        {
            if (root == null)
                return false;

            string[] patList = patterns as string[] ?? patterns.ToArray();
            if (patList.Length == 0)
                return false;

            Stack<Element> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Element el = stack.Pop();
                string text = el.GetText(512);
                if (!string.IsNullOrEmpty(text))
                {
                    for (int i = 0; i < patList.Length; i++)
                    {
                        if (text.Contains(patList[i]))
                            return true;
                    }
                }

                IList<Element> children = el.Children;
                if (children != null)
                {
                    foreach (Element c in children)
                    {
                        if (c != null)
                            stack.Push(c);
                    }
                }
            }

            return false;
        }
    }
}
