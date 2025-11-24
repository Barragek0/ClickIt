using System.Collections.Generic;
using SharpDX;

namespace ExileCore.PoEMemory
{
    // Minimal, test-friendly fakes for a subset of ExileCore types so unit tests can run in net8.
    public class Element
    {
        public string? Text { get; set; }
        public IList<Element>? Children { get; set; }
        public bool IsValid { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        public string? GetText(int _)
        {
            return Text;
        }

        public Element? GetChildAtIndex(int index)
        {
            if (Children == null || index < 0 || index >= Children.Count) return null;
            return Children[index];
        }

        public RectangleF? GetClientRect()
        {
            // Provide a small default rect if Text is present
            if (Text == null) return null;
            return new RectangleF(0, 0, 100, 20);
        }
    }

    namespace MemoryObjects
    {
        public class Entity
        {
            public float DistancePlayer { get; set; }
            public string? Path { get; set; }
            public Shared.Enums.EntityType Type { get; set; }

            public T? GetComponent<T>() where T : class, new()
            {
                // Provide a simple Chest component if requested. For tests, if the entity's Path contains
                // the word "locked" we'll return a Chest with IsLocked set to true.
                if (typeof(T).Name == "Chest")
                {
                    var chest = new Components.Chest();
                    if (!string.IsNullOrEmpty(Path) && Path.ToLowerInvariant().Contains("locked"))
                        chest.IsLocked = true;
                    return chest as T;
                }

                return new T();
            }
        }
    }

    namespace Elements
    {
        using MemoryObjects;

        public class LabelOnGround
        {
            public Entity ItemOnGround { get; set; } = new Entity();
            public Element? Label { get; set; }
            public bool IsVisible { get; set; } = true;
        }
    }

    namespace Components
    {
        public class Chest
        {
            public bool OpenOnDamage { get; set; } = false;
            public bool IsLocked { get; set; } = false;
        }
    }
}

namespace ExileCore.Shared.Enums
{
    public enum EntityType
    {
        WorldItem,
        AreaTransition,
        Chest,
        Monster
    }
}
