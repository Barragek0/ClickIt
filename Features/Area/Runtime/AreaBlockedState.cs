namespace ClickIt.Features.Area.Runtime
{
    internal sealed class AreaBlockedState
    {
        public RectangleF FullScreenRectangle { get; set; }
        public RectangleF HealthAndFlaskRectangle { get; set; }
        public RectangleF ManaAndSkillsRectangle { get; set; }
        public RectangleF HealthSquareRectangle { get; set; }
        public RectangleF FlaskRectangle { get; set; }
        public RectangleF FlaskTertiaryRectangle { get; set; }
        public RectangleF SkillsRectangle { get; set; }
        public RectangleF SkillsTertiaryRectangle { get; set; }
        public RectangleF ManaSquareRectangle { get; set; }
        public RectangleF BuffsAndDebuffsRectangle { get; set; }
        public List<RectangleF> BuffsAndDebuffsRectangles { get; } = [];
        public RectangleF ChatPanelBlockedRectangle { get; set; }
        public RectangleF MapPanelBlockedRectangle { get; set; }
        public RectangleF XpBarBlockedRectangle { get; set; }
        public RectangleF MirageBlockedRectangle { get; set; }
        public RectangleF AltarBlockedRectangle { get; set; }
        public RectangleF RitualBlockedRectangle { get; set; }
        public RectangleF SentinelBlockedRectangle { get; set; }
        public List<RectangleF> QuestTrackerBlockedRectangles { get; } = [];
        public long LastQuestTrackerRectanglesSuccessTimestampMs { get; set; }
        public long LastBlockedUiRectanglesRefreshTimestampMs { get; set; }
        public long LastBuffsAndDebuffsRectanglesRefreshTimestampMs { get; set; }
        public long LastKnownAreaHash { get; set; } = long.MinValue;

        internal AreaBlockedSnapshot CreateSnapshot()
        {
            return new AreaBlockedSnapshot
            {
                FullScreenRectangle = FullScreenRectangle,
                HealthAndFlaskRectangle = HealthAndFlaskRectangle,
                ManaAndSkillsRectangle = ManaAndSkillsRectangle,
                HealthSquareRectangle = HealthSquareRectangle,
                FlaskRectangle = FlaskRectangle,
                FlaskTertiaryRectangle = FlaskTertiaryRectangle,
                SkillsRectangle = SkillsRectangle,
                SkillsTertiaryRectangle = SkillsTertiaryRectangle,
                ManaSquareRectangle = ManaSquareRectangle,
                BuffsAndDebuffsRectangle = BuffsAndDebuffsRectangle,
                ChatPanelBlockedRectangle = ChatPanelBlockedRectangle,
                MapPanelBlockedRectangle = MapPanelBlockedRectangle,
                XpBarBlockedRectangle = XpBarBlockedRectangle,
                MirageBlockedRectangle = MirageBlockedRectangle,
                AltarBlockedRectangle = AltarBlockedRectangle,
                RitualBlockedRectangle = RitualBlockedRectangle,
                SentinelBlockedRectangle = SentinelBlockedRectangle,
                BuffsAndDebuffsRectangles = [.. BuffsAndDebuffsRectangles],
                QuestTrackerBlockedRectangles = [.. QuestTrackerBlockedRectangles]
            };
        }

        internal void ApplySnapshot(AreaBlockedSnapshot snapshot)
        {
            FullScreenRectangle = snapshot.FullScreenRectangle;
            HealthAndFlaskRectangle = snapshot.HealthAndFlaskRectangle;
            ManaAndSkillsRectangle = snapshot.ManaAndSkillsRectangle;
            HealthSquareRectangle = snapshot.HealthSquareRectangle;
            FlaskRectangle = snapshot.FlaskRectangle;
            FlaskTertiaryRectangle = snapshot.FlaskTertiaryRectangle;
            SkillsRectangle = snapshot.SkillsRectangle;
            SkillsTertiaryRectangle = snapshot.SkillsTertiaryRectangle;
            ManaSquareRectangle = snapshot.ManaSquareRectangle;
            BuffsAndDebuffsRectangle = snapshot.BuffsAndDebuffsRectangle;
            ChatPanelBlockedRectangle = snapshot.ChatPanelBlockedRectangle;
            MapPanelBlockedRectangle = snapshot.MapPanelBlockedRectangle;
            XpBarBlockedRectangle = snapshot.XpBarBlockedRectangle;
            MirageBlockedRectangle = snapshot.MirageBlockedRectangle;
            AltarBlockedRectangle = snapshot.AltarBlockedRectangle;
            RitualBlockedRectangle = snapshot.RitualBlockedRectangle;
            SentinelBlockedRectangle = snapshot.SentinelBlockedRectangle;

            BuffsAndDebuffsRectangles.Clear();
            BuffsAndDebuffsRectangles.AddRange(snapshot.BuffsAndDebuffsRectangles);

            QuestTrackerBlockedRectangles.Clear();
            QuestTrackerBlockedRectangles.AddRange(snapshot.QuestTrackerBlockedRectangles);
        }
    }
}