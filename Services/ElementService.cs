using ExileCore.PoEMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#nullable enable
namespace ClickIt.Services
{
    public static class ElementService
    {
        [ThreadStatic]
        private static List<Element>? _threadLocalList;

        private static List<Element> GetThreadLocalList()
        {
            if (_threadLocalList == null)
            {
                _threadLocalList = new List<Element>();
            }
            return _threadLocalList;
        }

        public static List<Element> GetElementsByStringContains(Element? label, string str)
        {
            var elementsList = GetThreadLocalList();
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
                    foreach (Element c in children.Where(c => c != null))
                    {
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
                    foreach (Element c in children.Where(c => c != null))
                    {
                        stack.Push(c);
                    }
                }
            }
            return false;
        }
    }
}
