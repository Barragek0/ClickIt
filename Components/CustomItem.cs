using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using System;
namespace ClickIt.Components
{
    public class CustomItem
    {
        public Func<bool> IsTargeted;
        public bool IsValid;
        public string BaseName { get; } = "";
        public string ClassName { get; } = "";
        public LabelOnGround LabelOnGround { get; }
        public float Distance { get; }
        public Entity GroundItem { get; }
        public int Height { get; }
        public string Path { get; }
        public int Width { get; }
        public CustomItem(LabelOnGround item, FilesContainer fs)
        {
            LabelOnGround = item;
            var itemItemOnGround = item.ItemOnGround;
            var worldItem = itemItemOnGround?.GetComponent<WorldItem>();
            if (worldItem == null)
            {
                return;
            }
            var groundItem = worldItem.ItemEntity;
            GroundItem = groundItem;
            Path = groundItem?.Path;
            if (GroundItem == null)
            {
                return;
            }
            if (Path != null && Path.Length < 1)
            {
                DebugWindow.LogMsg($"World: {worldItem.Address:X} P: {Path}", 2);
                DebugWindow.LogMsg($"Ground: {GroundItem.Address:X} P {Path}", 2);
                return;
            }
            IsTargeted = () => itemItemOnGround?.GetComponent<Targetable>()?.isTargeted == true;
            var baseItemType = fs.BaseItemTypes.Translate(Path);
            if (baseItemType != null)
            {
                ClassName = baseItemType.ClassName;
                BaseName = baseItemType.BaseName;
                Width = baseItemType.Width;
                Height = baseItemType.Height;
            }
            IsValid = true;
        }
        public override string ToString()
        {
            return $"{BaseName} ({ClassName}) Dist: {GroundItem.DistancePlayer}";
        }
    }
}
