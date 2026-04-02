using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        internal IReadOnlyList<string> GetUltimatumModifierPriority()
            => UltimatumSettingsRuntimeService.GetModifierPriority(this);

        internal IReadOnlyCollection<string> GetUltimatumTakeRewardModifierNames()
            => UltimatumSettingsRuntimeService.GetTakeRewardModifierNames(this);

        internal bool ShouldTakeRewardForGruelingGauntletModifier(string? modifierName)
            => UltimatumSettingsRuntimeService.ShouldTakeRewardForGruelingGauntletModifier(this, modifierName);
    }
}