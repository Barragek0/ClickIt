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

            AddIfTextContains(label, str, elementsList);

            for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
            {
                AddMatchingChildrenFromContainer(label.GetChildAtIndex(containerIndex), str, elementsList);
            }

            return elementsList;
        }

        private static void AddMatchingChildrenFromContainer(Element? container, string str, List<Element> elementsList)
        {
            if (container == null)
                return;

            IList<Element> children = container.Children;
            if (children == null)
                return;

            for (int i = 0; i < children.Count; i++)
            {
                AddIfTextContains(children[i], str, elementsList);
            }
        }

        private static void AddIfTextContains(Element? element, string str, List<Element> elements)
        {
            if (element == null)
                return;

            string text = element.GetText(512);
            if (!string.IsNullOrEmpty(text) && text.Contains(str))
            {
                elements.Add(element);
            }
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
                if (ElementTextContainsAnyPattern(el, patList))
                    return true;

                PushChildren(el, stack);
            }

            return false;
        }

        private static bool ElementTextContainsAnyPattern(Element element, string[] patterns)
        {
            string text = element.GetText(512);
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < patterns.Length; i++)
            {
                if (text.Contains(patterns[i]))
                    return true;
            }

            return false;
        }

        private static void PushChildren(Element element, Stack<Element> stack)
        {
            IList<Element> children = element.Children;
            if (children == null)
                return;

            foreach (Element child in children)
            {
                if (child != null)
                {
                    stack.Push(child);
                }
            }
        }
    }
}
