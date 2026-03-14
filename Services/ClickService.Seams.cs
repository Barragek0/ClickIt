namespace ClickIt.Services
{
    // Test-only seams for ClickService (kept separate to avoid polluting production class)
    internal static class ClickServiceSeams
    {
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
