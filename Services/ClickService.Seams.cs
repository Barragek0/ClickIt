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

        // Ground-label Ultimatum exposes exactly three options; clamp pre-hover work accordingly.
        internal static int[] GetUltimatumPreHoverIndices(int optionCount)
        {
            if (optionCount <= 0)
                return [];

            int count = optionCount > 3 ? 3 : optionCount;
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = i;
            }

            return result;
        }
    }
}
