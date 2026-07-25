namespace ClickIt.Shared.Game
{
    internal static class LabelElementSearch
    {
        private static readonly ThreadLocal<List<Element>> ThreadLocalElementsList = new(() => []);

        public static void ClearThreadLocalStorage()
        {
            ThreadLocalElementsList.Value?.Clear();
        }

        public static List<Element> GetElementsByStringContains(Element? label, string str)
        {
            List<Element> elementsList = GetThreadLocalElementsList();
            elementsList.Clear();
            if (label == null)
                return elementsList;


            AddIfTextContains(label, str, elementsList);

            for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
                AddMatchingChildrenFromContainer(label.GetChildAtIndex(containerIndex), str, elementsList);


            return elementsList;
        }

        public static Element? GetElementByString(Element? root, string str)
        {
            if (root == null)
                return null;


            Stack<Element> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Element element = stack.Pop();
                string text = element.GetText(512);
                if (text != null && text.Equals(str, StringComparison.Ordinal))
                    return element;


                PushChildren(element, stack);
            }

            return null;
        }

        public static bool ElementContainsAnyStrings(Element? root, IEnumerable<string> patterns)
        {
            if (root == null)
                return false;


            string[] patList = patterns as string[] ?? [.. patterns];
            if (patList.Length == 0)
                return false;


            Stack<Element> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Element element = stack.Pop();
                if (ElementTextContainsAnyPattern(element, patList))
                    return true;


                PushChildren(element, stack);
            }

            return false;
        }

        internal static int GetThreadLocalElementsCount()
            => ThreadLocalElementsList.Value?.Count ?? 0;

        internal static void AddNullElementToThreadLocal()
            => ThreadLocalElementsList.Value?.Add(null!);

        internal static bool ElementContainsAnyStringsCore(IElementAdapter? root, IEnumerable<string> patterns)
        {
            if (root == null)
                return false;


            string[] patList = patterns as string[] ?? [.. patterns];
            if (patList.Length == 0)
                return false;


            Stack<IElementAdapter> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                IElementAdapter element = stack.Pop();
                string text = element.GetText(512);
                if (!string.IsNullOrEmpty(text))
                    for (int i = 0; i < patList.Length; i++)
                        if (text.Contains(patList[i], StringComparison.Ordinal))
                            return true;




                foreach (IElementAdapter child in EnumerateAdapterChildren(element))
                    stack.Push(child);

            }

            return false;
        }

        internal static List<IElementAdapter> GetElementsByStringContainsCore(IElementAdapter? label, string str)
        {
            List<IElementAdapter> list = [];
            if (label == null)
                return list;


            string rootText = label.GetText(512);
            if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str, StringComparison.Ordinal))
                list.Add(label);


            foreach (IElementAdapter child in EnumerateAdapterChildren(label))
            {
                string childText = child.GetText(512);
                if (!string.IsNullOrEmpty(childText) && childText.Contains(str, StringComparison.Ordinal))
                    list.Add(child);

            }

            return list;
        }

        internal static IElementAdapter? GetElementByStringCore(IElementAdapter? root, string str)
        {
            if (root == null)
                return null;


            Stack<IElementAdapter> stack = new();
            stack.Push(root);
            while (stack.Count > 0)
            {
                IElementAdapter element = stack.Pop();
                string text = element.GetText(512);
                if (text != null && text.Equals(str, StringComparison.Ordinal))
                    return element;


                foreach (IElementAdapter child in EnumerateAdapterChildren(element))
                    stack.Push(child);

            }

            return null;
        }

        private static List<Element> GetThreadLocalElementsList()
            => ThreadLocalElementsList.Value ?? [];

        private static void AddMatchingChildrenFromContainer(Element? container, string str, List<Element> elementsList)
        {
            if (container == null)
                return;


            IList<Element> children = container.Children;
            if (children == null)
                return;


            for (int i = 0; i < children.Count; i++)
                AddIfTextContains(children[i], str, elementsList);

        }

        private static void AddIfTextContains(Element? element, string str, List<Element> elements)
        {
            if (element == null)
                return;


            string text = element.GetText(512);
            if (!string.IsNullOrEmpty(text) && text.Contains(str, StringComparison.Ordinal))
                elements.Add(element);

        }

        private static bool ElementTextContainsAnyPattern(Element element, string[] patterns)
        {
            string text = element.GetText(512);
            if (string.IsNullOrEmpty(text))
                return false;


            for (int i = 0; i < patterns.Length; i++)
                if (text.Contains(patterns[i], StringComparison.Ordinal))
                    return true;



            return false;
        }

        private static void PushChildren(Element element, Stack<Element> stack)
        {
            IList<Element> children = element.Children;
            if (children == null)
                return;


            foreach (Element child in children)
                if (child != null)
                    stack.Push(child);


        }

        private static IEnumerable<IElementAdapter> EnumerateAdapterChildren(IElementAdapter parent)
        {
            HashSet<IElementAdapter> seen = [];
            for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
            {
                int childIndex = 0;
                while (true)
                {
                    IElementAdapter? child = parent.GetChildFromIndices(containerIndex, childIndex);
                    if (child == null)
                        break;


                    if (seen.Add(child))
                        yield return child;


                    childIndex++;
                }
            }
        }
    }
}