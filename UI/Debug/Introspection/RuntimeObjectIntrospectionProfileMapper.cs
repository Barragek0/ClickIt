namespace ClickIt.UI.Debug.Introspection
{
    internal static class RuntimeObjectIntrospectionProfileMapper
    {
        public static string GetFileName(IntrospectionProfile profile)
        {
            return profile switch
            {
                IntrospectionProfile.StructureFirst => "structure.dat",
                IntrospectionProfile.Full => "full.dat",
                IntrospectionProfile.Default => "memory.dat",
                _ => "memory.dat"
            };
        }

        public static RuntimeObjectIntrospectionOptions GetOptions(IntrospectionProfile profile)
        {
            return profile switch
            {
                IntrospectionProfile.StructureFirst => RuntimeObjectIntrospectionOptions.StructureFirst,
                IntrospectionProfile.Full => RuntimeObjectIntrospectionOptions.VeryDeepAllData,
                IntrospectionProfile.Default => RuntimeObjectIntrospectionOptions.Default,
                _ => RuntimeObjectIntrospectionOptions.Default
            };
        }
    }
}
