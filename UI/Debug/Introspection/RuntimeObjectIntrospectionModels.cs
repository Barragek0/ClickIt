namespace ClickIt.Shared
{
    internal enum IntrospectionProfile
    {
        Default,
        StructureFirst,
        Full
    }

    internal readonly record struct RuntimeObjectIntrospectionOptions(
        string Title,
        int MaxDepth,
        int MaxCollectionItems,
        IReadOnlyList<string>? PriorityMembers,
        int MaxMembersPerObject = 48,
        bool IncludeNonPublicMembers = false,
        int MaxValueChars = 120,
        int MaxTotalNodes = 25000,
        int MaxElapsedMs = 12000)
    {
        public static RuntimeObjectIntrospectionOptions Default => new(
            Title: "Runtime Object Introspection",
            MaxDepth: 8,
            MaxCollectionItems: 5,
            PriorityMembers: [],
            MaxMembersPerObject: 20,
            IncludeNonPublicMembers: false,
            MaxValueChars: 256,
            MaxTotalNodes: 25000,
            MaxElapsedMs: 12000);

        public static RuntimeObjectIntrospectionOptions StructureFirst => new(
            Title: "Structure-First Memory Dump",
            MaxDepth: 64,
            MaxCollectionItems: 5,
            PriorityMembers: [],
            MaxMembersPerObject: 20,
            IncludeNonPublicMembers: false,
            MaxValueChars: int.MaxValue,
            MaxTotalNodes: 25000,
            MaxElapsedMs: 12000);

        public static RuntimeObjectIntrospectionOptions VeryDeepAllData => new(
            Title: "Full Game Memory Dump",
            MaxDepth: int.MaxValue,
            MaxCollectionItems: 3,
            PriorityMembers: [],
            MaxMembersPerObject: int.MaxValue,
            IncludeNonPublicMembers: false,
            MaxValueChars: int.MaxValue,
            MaxTotalNodes: 60000,
            MaxElapsedMs: 30000);
    }
}