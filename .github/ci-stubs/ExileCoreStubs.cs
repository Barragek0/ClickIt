using System;
using System.Collections.Generic;
using SharpDX;

namespace ExileCore
{
    public class GameController { }
    public class Graphics { }
    public class BaseSettingsPlugin<T> { }
    public class Coroutine { }
}

namespace ExileCore.Shared
{
    public static class Helpers { }
    public enum SomeEnum { None }
}

namespace ExileCore.Shared.Cache
{
    public class TimeCache<T>
    {
        public TimeCache() { }
    }
}

namespace ExileCore.Shared.Enums
{
    public enum EntityType { None = 0 }
}

namespace ExileCore.PoEMemory
{
    public class Element
    {
        public bool IsValid { get; set; }
        public string Text { get; set; }
        public RectangleF GetClientRect() => default(RectangleF);
    }

    namespace Elements
    {
        public class LabelOnGround : Element
        {
            public Vector2 GetWorldPos() => default(Vector2);
        }
    }

    namespace MemoryObjects
    {
        public class Entity { }
        public class Camera { }
    }

    namespace Components
    {
        public class SomeComponent { }
    }
}

// Minimal placeholders for other referenced assemblies (types may be light)
namespace ExileCore.Shared.Nodes
{
    public class ToggleNode<T>
    {
        public T Value { get; set; }
        public ToggleNode(T v) { Value = v; }
    }
}

namespace ExileCore.Shared.Attributes
{
    public class MenuAttribute : Attribute
    {
        public MenuAttribute(string name, int priority = 0) { }
    }

}
