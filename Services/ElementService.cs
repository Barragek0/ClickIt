using ExileCore.PoEMemory;
using System;
using System.Collections.Generic;
using System.Linq;
#nullable enable
namespace ClickIt.Services
{
    public static class ElementService
    {
        private static readonly List<Element> ElementsByStringContainsList = new();
        public static List<Element> GetElementsByStringContains(Element? label, string str)
        {
            ElementsByStringContainsList.Clear();
            if (label == null)
                return ElementsByStringContainsList;
            try
            {
                string rootText = label.GetText(512);
                if (!string.IsNullOrEmpty(rootText) && rootText.Contains(str))
                    ElementsByStringContainsList.Add(label);
                for (int containerIndex = 0; containerIndex <= 1; containerIndex++)
                {
                    try
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
                                ElementsByStringContainsList.Add(child);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            return ElementsByStringContainsList;
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
                try
                {
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
                catch
                {
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
                try
                {
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
                catch
                {
                }
            }
            return false;
        }
    }
}
