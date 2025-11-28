using System;

namespace ClickIt.Services
{
    // Test-only seams for ClickService (kept separate to avoid polluting production class)
    internal static class ClickServiceSeams
    {
        // Test helper: determine which label index should be selected given a UIHover address
        // labelAddresses: array of label->Element.Address values (0 for null)
        // candidateIndex: index of initially chosen candidate
        // uiHoverAddress: the UIHover element's Address value (0 = none)
        // Returns the index to use (either the hovered label if present, otherwise the candidate)
        internal static int? ChooseLabelIndexByUIHoverForTests(ulong[] labelAddresses, int candidateIndex, ulong uiHoverAddress)
        {
            if (labelAddresses == null) return null;
            if (uiHoverAddress == 0) return candidateIndex;
            for (int i = 0; i < labelAddresses.Length; i++)
            {
                if (labelAddresses[i] != 0 && labelAddresses[i] == uiHoverAddress)
                    return i;
            }
            return candidateIndex;
        }
    }
}
