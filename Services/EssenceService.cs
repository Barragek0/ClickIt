using ClickIt.Utils;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using System;
#nullable enable
namespace ClickIt.Services
{
    public class EssenceService
    {
        private readonly ClickItSettings _settings;
        public EssenceService(ClickItSettings settings)
        {
            _settings = settings;
        }
        public bool ShouldCorruptEssence(Element? label)
        {
            if (label == null)
                return false;
            if (LabelUtils.ElementContainsAnyStrings(label, ["Corrupted"]))
                return false;
            if (_settings.CorruptAllEssences.Value)
                return true;
            if (_settings.CorruptMEDSEssences.Value)
            {
                string[] meds = new[]
                {
                    "Screaming Essence of Misery", "Screaming Essence of Envy", "Screaming Essence of Dread", "Screaming Essence of Scorn",
                    "Shrieking Essence of Misery", "Shrieking Essence of Envy", "Shrieking Essence of Dread", "Shrieking Essence of Scorn",
                    "Deafening Essence of Misery", "Deafening Essence of Envy", "Deafening Essence of Dread", "Deafening Essence of Scorn"
                };
                return LabelUtils.ElementContainsAnyStrings(label, meds);
            }
            return false;
        }
        public Vector2? GetCorruptionClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            Element? corruptElement = label.Label?.GetChildAtIndex(2)?.GetChildAtIndex(0)?.GetChildAtIndex(0);
            if (corruptElement == null)
                return null;
            var random = new System.Random();
            Vector2 offset = new(random.Next(0, 2), random.Next(0, 2));
            return corruptElement.GetClientRect().Center + windowTopLeft + offset;
        }
    }
}
