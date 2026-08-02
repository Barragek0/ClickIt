namespace ClickIt.Features.Blight;

internal static class BlightHelpers
{
    internal static BlightTowerType? MapTowerIdToType(string towerId)
    {
        if (string.IsNullOrEmpty(towerId))
            return null;
        return BlightTowerData.MapTowerIdToType(towerId);
    }

    internal static BlightTowerType DetectFoundationTypeFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return BlightTowerType.Chilling;

        int idx = path.LastIndexOf("BlightFoundation", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return BlightTowerType.Chilling;

        string suffix = path[(idx + "BlightFoundation".Length)..];
        return BlightTowerData.MapFoundationSuffix(suffix) ?? BlightTowerType.Chilling;
    }

    internal static int DetectUpgradeRankFromEntityPath(Entity? entity)
    {
        if (entity == null)
            return 0;
        try
        {
            string? path = entity.Path;
            if (string.IsNullOrEmpty(path))
                return 0;
            int rankIdx = path.LastIndexOf("Rank", StringComparison.OrdinalIgnoreCase);
            if (rankIdx < 0 || rankIdx + 5 >= path.Length)
                return 0;
            char rankChar = path[rankIdx + 4];
            if (char.IsDigit(rankChar))
                return rankChar - '0';
        }
        catch { }
        return 0;
    }

    internal static int ParseTowerIdLevel(string towerId)
    {
        for (int i = towerId.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(towerId[i]))
            {
                int start = i;
                while (start > 0 && char.IsDigit(towerId[start - 1]))
                    start--;
                if (int.TryParse(towerId.AsSpan(start, i - start + 1), out int level))
                    return level;
            }
        }
        return 0;
    }

    internal static NumVector2 GetGridPosition(Entity entity)
    {
        try { return new NumVector2(entity.GridPosNum.X, entity.GridPosNum.Y); }
        catch { return NumVector2.Zero; }
    }

    internal static bool SameGridPosition(NumVector2 a, NumVector2 b)
        => MathF.Abs(a.X - b.X) < 1f && MathF.Abs(a.Y - b.Y) < 1f;

    internal static int FindTowerIndexAt(IReadOnlyList<BlightCachedTower> towers, NumVector2 pos)
    {
        for (int i = 0; i < towers.Count; i++)
            if (SameGridPosition(towers[i].WorldPosition, pos))
                return i;
        return -1;
    }

    internal static BlightCachedTower? FindTowerAt(IReadOnlyList<BlightCachedTower> towers, NumVector2 pos)
    {
        int index = FindTowerIndexAt(towers, pos);
        return index >= 0 ? towers[index] : null;
    }

    internal static string GetBlightTowerId(BlightTower tower)
    {
        try
        {
            dynamic? dynTower = tower;
            dynamic? info = dynTower?.Info;
            return info?.Id as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
