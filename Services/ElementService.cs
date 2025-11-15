using ExileCore.PoEMemory;
using System;
using System.Collections.Generic;
using System.Linq;
#nullable enable
using System.Threading;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public static class ElementService
    {
        [ThreadStatic]
        internal static List<Element>? _threadLocalList;

        internal static List<Element> GetThreadLocalList()
        {
            if (_threadLocalList == null)
            {
                _threadLocalList = new List<Element>();
            }
            return _threadLocalList;
        }
    }
}
