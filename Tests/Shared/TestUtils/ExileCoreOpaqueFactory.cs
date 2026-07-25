namespace ClickIt.Tests.Shared.TestUtils
{
    internal sealed record ExileCoreOpaqueUsageNote(
        string TypeName,
        IReadOnlyList<string> SafePatterns,
        IReadOnlyList<string> UnsafeMembers,
        string Rationale);

    internal static class ExileCoreOpaqueFactory
    {
        private static readonly IReadOnlyDictionary<string, ExileCoreOpaqueUsageNote> NotesByTypeName =
            new Dictionary<string, ExileCoreOpaqueUsageNote>(StringComparer.Ordinal)
            {
                [typeof(Entity).FullName!] = new(
                    TypeName: typeof(Entity).FullName!,
                    SafePatterns:
                    [
                        "Treat Entity as an opaque token when the test only needs reference identity.",
                        "Use uninitialized Entity instances only when collaborators are stubbed so no remote-memory-backed getters run.",
                        "Prefer a dedicated Entity probe subclass when the test needs Path, DistancePlayer, PosNum, GridPosNum, or component reads."
                    ],
                    UnsafeMembers:
                    [
                        nameof(Entity.DistancePlayer),
                        nameof(Entity.Path),
                        nameof(Entity.Rarity),
                        nameof(Entity.IsAlive),
                        nameof(Entity.IsHostile),
                        nameof(Entity.PosNum),
                        "GetComponent<T>()"
                    ],
                    Rationale: "Many Entity properties derive from ExileCore memory access or dependent component state rather than local in-memory fields."),
                [typeof(LabelOnGround).FullName!] = new(
                    TypeName: typeof(LabelOnGround).FullName!,
                    SafePatterns:
                    [
                        "Treat LabelOnGround as opaque only when the test never dereferences ItemOnGround, Label, or geometry.",
                        "Prefer testing through seams that accept LabelOnGround but do not inspect remote-memory-backed members."
                    ],
                    UnsafeMembers:
                    [
                        nameof(LabelOnGround.ItemOnGround),
                        nameof(LabelOnGround.Label),
                        nameof(LabelOnGround.Address)
                    ],
                    Rationale: "LabelOnGround getters resolve nested remote-memory-backed objects and UI elements, which are not safe on uninitialized instances."),
                [typeof(GameController).FullName!] = new(
                    TypeName: typeof(GameController).FullName!,
                    SafePatterns:
                    [
                        "Use uninitialized GameController only when every accessed collaborator is injected or stubbed before dereference.",
                        "Prefer service seams that accept delegates over direct GameController traversal in tests.",
                        "Inspect nested Game and Window metadata before assuming CurrentAreaHash, IngameUi, or window-rectangle reads are directly seedable in tests."
                    ],
                    UnsafeMembers:
                    [
                        nameof(GameController.IngameState),
                        nameof(GameController.Window),
                        nameof(GameController.Area),
                        nameof(GameController.EntityListWrapper),
                        nameof(GameController.Player),
                        "Game.CurrentAreaHash",
                        "Game.IngameState.IngameUi",
                        "Window.GetWindowRectangleTimeCache"
                    ],
                    Rationale: "GameController is a deep runtime root. Most useful members depend on live game state or fully initialized graph objects."),
            };

        internal static T CreateOpaque<T>() where T : class
            => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        internal static Entity CreateOpaqueEntity()
            => CreateOpaque<Entity>();

        internal static LabelOnGround CreateOpaqueLabel()
            => CreateOpaque<LabelOnGround>();

        internal static GameController CreateOpaqueGameController()
            => CreateOpaque<GameController>();

        internal static ExileCoreOpaqueUsageNote GetUsageNote<T>() where T : class
            => GetUsageNote(typeof(T));

        internal static ExileCoreOpaqueUsageNote GetUsageNote(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (!NotesByTypeName.TryGetValue(type.FullName ?? type.Name, out ExileCoreOpaqueUsageNote? note))
                throw new InvalidOperationException($"No ExileCore opaque-usage note registered for {type.FullName ?? type.Name}.");

            return note;
        }
    }
}