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
            if (ElementService.ElementContainsAnyStrings(label, ["Corrupted"]))
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
                return ElementService.ElementContainsAnyStrings(label, meds);
            }
            if (_settings.CorruptAnyNonShrieking.Value)
                return !CheckForAnyShriekingEssence(label);
            return false;
        }
        private static bool CheckForAnyShriekingEssence(Element label)
        {
            string[] shrieking = new[]
            {
                "Shrieking Essence of Greed", "Shrieking Essence of Contempt", "Shrieking Essence of Hatred",
                "Shrieking Essence of Woe", "Shrieking Essence of Fear", "Shrieking Essence of Anger",
                "Shrieking Essence of Torment", "Shrieking Essence of Sorrow", "Shrieking Essence of Rage",
                "Shrieking Essence of Suffering", "Shrieking Essence of Wrath", "Shrieking Essence of Doubt",
                "Shrieking Essence of Loathing", "Shrieking Essence of Zeal", "Shrieking Essence of Anguish",
                "Shrieking Essence of Spite", "Shrieking Essence of Scorn", "Shrieking Essence of Envy",
                "Shrieking Essence of Misery", "Shrieking Essence of Dread"
            };
            return ElementService.ElementContainsAnyStrings(label, shrieking);
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
