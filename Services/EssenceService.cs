using ClickIt.Utils;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using SharpDX;
namespace ClickIt.Services
{
    public class EssenceService(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public bool ShouldCorruptEssence(Element? label)
        {
            if (label == null)
                return false;
            if (LabelUtils.ElementContainsAnyStrings(label, ["Corrupted"]))
                return false;

            IReadOnlyList<string> selectedEssences = _settings.GetCorruptEssenceNames();
            if (selectedEssences.Count == 0)
                return false;

            return LabelUtils.ElementContainsAnyStrings(label, selectedEssences);
        }

        public static Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            Element? corruptElement = label.Label?.GetChildAtIndex(2)?.GetChildAtIndex(0)?.GetChildAtIndex(0);
            if (corruptElement == null)
                return null;
            var random = new Random();
            Vector2 offset = new(random.Next(0, 2), random.Next(0, 2));
            return corruptElement.GetClientRect().Center + windowTopLeft + offset;
        }
    }
}
