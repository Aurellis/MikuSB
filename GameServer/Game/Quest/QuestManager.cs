using System.Numerics;
using System.Text.Json.Nodes;
using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database;
using MikuSB.Database.Player;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using MikuSB.Util;

namespace MikuSB.GameServer.Game.Quest;

public enum QuestLevelType
{
    Chapter,
    Daily,
    Role
}

public readonly record struct QuestSettlementResult(JsonArray Rewards, NtfSyncPlayer Sync);
public readonly record struct ChapterStarAwardResult(JsonObject Response, NtfSyncPlayer Sync);

public class QuestManager(PlayerInstance player) : BasePlayerManager(player)
{
    private const uint LevelStateGroupId = 21;
    private const uint LevelPassGroupId = 22;
    private const uint SettlementSeedGroupId = 23;
    private const uint ChapterStarAwardGroupId = 20;
    private const uint LevelStarMask = 0b111;
    private const uint LegacyUnlockedLevelPassTime = 1_700_000_000;
    private static readonly Logger Logger = new("Quest");
    private readonly SemaphoreSlim settlementLock = new(1, 1);

    public void RemoveLegacyLevelUnlocks()
    {
        var levelIds = GameData.ChapterLevelData.Keys
            .Concat(GameData.DailyLevelData.Keys)
            .Concat(GameData.RoleLevelData.Keys)
            .Distinct();

        foreach (var levelId in levelIds)
        {
            var passAttr = Player.Data.Attrs.FirstOrDefault(x => x.Gid == LevelPassGroupId && x.Sid == levelId);
            if (passAttr?.Val != LegacyUnlockedLevelPassTime)
                continue;

            Player.Data.Attrs.Remove(passAttr);

            var stateAttr = Player.Data.Attrs.FirstOrDefault(x => x.Gid == LevelStateGroupId && x.Sid == levelId);
            if (stateAttr != null)
                Player.Data.Attrs.Remove(stateAttr);
        }
    }

    public async ValueTask<QuestSettlementResult?> SettleLevelAsync(
        QuestLevelType levelType,
        uint levelId,
        int starMask,
        uint seed)
    {
        if (seed == 0 || starMask < 0 || starMask > LevelStarMask ||
            !Player.IsLevelSession(levelType, levelId, seed))
            return null;

        var levelConfig = ResolveLevelConfig(levelType, levelId);
        if (levelConfig == null)
            return null;

        await settlementLock.WaitAsync();
        try
        {
            var levelPass = GetOrCreateAttr(LevelPassGroupId, levelId);
            var settlementSeed = GetOrCreateAttr(SettlementSeedGroupId, levelId);
            var levelState = GetOrCreateAttr(LevelStateGroupId, levelId);
            var isFirstClear = levelPass.Val == 0 &&
                               (levelType != QuestLevelType.Daily || (levelState.Val & (1u << 8)) == 0);

            if (settlementSeed.Val == seed)
            {
                var duplicateSync = Player.RewardManager.BuildFullSync();
                var duplicateRewards = Player.RewardManager.ResolveLevelRewards(levelConfig, levelPass.Val <= 1, seed);
                return new QuestSettlementResult(duplicateRewards, duplicateSync);
            }

            var sync = new NtfSyncPlayer();
            await Player.RewardManager.GrantLevelRewardsAsync(levelConfig, isFirstClear, seed, sync);

            levelState.Val |= (uint)starMask & LevelStarMask;
            if (levelType == QuestLevelType.Daily)
                levelState.Val |= 1u << 8;
            levelPass.Val = levelPass.Val == uint.MaxValue ? uint.MaxValue : levelPass.Val + 1;
            SyncAttr(sync, levelState);
            SyncAttr(sync, levelPass);
            settlementSeed.Val = seed;
            SyncAttr(sync, settlementSeed);

            Logger.Info($"Level settlement saved. uid={Player.Uid} levelType={levelType} levelId={levelId} " +
                        $"starMask={starMask} stateVal={levelState.Val} passVal={levelPass.Val}");

            DatabaseHelper.SaveDatabaseType(Player.Data);
            DatabaseHelper.SaveDatabaseType(Player.InventoryManager.InventoryData);
            DatabaseHelper.SaveDatabaseType(Player.CharacterManager.CharacterData);
            return new QuestSettlementResult(
                Player.RewardManager.ResolveLevelRewards(levelConfig, isFirstClear, seed),
                sync);
        }
        finally
        {
            settlementLock.Release();
        }
    }

    public bool IsPlotLevel(uint levelId) =>
        GameData.ChapterLevelData.TryGetValue(levelId, out var levelConfig) && levelConfig.IsPlot();

    public async ValueTask<QuestSettlementResult?> SettlePlotLevelAsync(uint levelId)
    {
        if (!GameData.ChapterLevelData.TryGetValue(levelId, out var levelConfig) || !levelConfig.IsPlot())
            return null;

        await settlementLock.WaitAsync();
        try
        {
            var levelState = GetOrCreateAttr(LevelStateGroupId, levelId);
            if ((levelState.Val & (1u << 8)) != 0)
                return new QuestSettlementResult(new JsonArray(), Player.RewardManager.BuildFullSync());

            var levelPass = GetOrCreateAttr(LevelPassGroupId, levelId);
            var sync = new NtfSyncPlayer();
            await Player.RewardManager.GrantLevelRewardsAsync(levelConfig, true, levelId, sync);

            levelState.Val |= 1u << 8;
            if (levelPass.Val == 0)
                levelPass.Val = 1;

            SyncAttr(sync, levelState);
            SyncAttr(sync, levelPass);

            Logger.Info($"Plot settlement saved. uid={Player.Uid} levelId={levelId} " +
                        $"stateVal={levelState.Val} passVal={levelPass.Val}");

            DatabaseHelper.SaveDatabaseType(Player.Data);
            DatabaseHelper.SaveDatabaseType(Player.InventoryManager.InventoryData);
            DatabaseHelper.SaveDatabaseType(Player.CharacterManager.CharacterData);
            return new QuestSettlementResult(
                Player.RewardManager.ResolveLevelRewards(levelConfig, true, levelId),
                sync);
        }
        finally
        {
            settlementLock.Release();
        }
    }

    private static ILevelRewardConfig? ResolveLevelConfig(QuestLevelType levelType, uint levelId) =>
        levelType switch
        {
            QuestLevelType.Chapter => GameData.ChapterLevelData.GetValueOrDefault(levelId),
            QuestLevelType.Daily => GameData.DailyLevelData.GetValueOrDefault(levelId),
            QuestLevelType.Role => GameData.RoleLevelData.GetValueOrDefault(levelId),
            _ => null
        };

    public bool CanEnterLevel(QuestLevelType levelType, uint levelId)
    {
        if (ResolveLevelConfig(levelType, levelId) == null)
            return false;

        var predecessorIds = GetLevelConfigs(levelType)
            .Where(level => level.NextId() == levelId)
            .Select(level => level.ID)
            .ToArray();

        return predecessorIds.Length == 0 || predecessorIds.Any(id => GetPassCount(id) > 0);
    }

    public bool SyncGuideLevelPassData(JsonNode? payload)
    {
        if (payload is not JsonObject root || root["tbData"] is not JsonArray rows)
            return false;

        var updates = new List<(uint LevelId, uint PassCount)>();
        foreach (var rowNode in rows)
        {
            if (rowNode is not JsonArray row || row.Count < 2 ||
                !TryGetUInt(row[0], out var levelId) ||
                !TryGetUInt(row[1], out var passCount) ||
                levelId == 0 || !GameData.ChapterLevelData.ContainsKey(levelId))
                return false;

            updates.Add((levelId, passCount));
        }

        var changed = false;
        foreach (var (levelId, passCount) in updates)
        {
            var attr = GetOrCreateAttr(LevelPassGroupId, levelId);
            if (passCount <= attr.Val)
                continue;

            attr.Val = passCount;
            changed = true;
        }

        if (changed)
            DatabaseHelper.SaveDatabaseType(Player.Data);

        return true;
    }

    public async ValueTask<ChapterStarAwardResult?> ClaimChapterStarAwardsAsync(
        bool isMain,
        uint difficult,
        uint chapterId,
        int awardIndex)
    {
        if (!GameData.ChapterData.TryGetValue(ChapterExcel.GetKey(isMain, difficult, chapterId), out var chapter))
            return null;

        await settlementLock.WaitAsync();
        try
        {
            var awards = chapter.StarAwards;
            var claimedAttr = GetOrCreateAttr(
                ChapterStarAwardGroupId,
                (chapterId << 8) | difficult);
            var starCount = GetChapterStarCount(chapter);
            var selected = new List<(int Index, ChapterStarAward Award)>();

            if (awardIndex == -1)
            {
                for (var i = 0; i < awards.Count && i < 32; i++)
                {
                    var claimBit = 1u << i;
                    if ((claimedAttr.Val & claimBit) == 0 && starCount >= awards[i].RequiredStars)
                        selected.Add((i, awards[i]));
                }
            }
            else
            {
                if (awardIndex < 1 || awardIndex > awards.Count || awardIndex > 32)
                    return null;

                var index = awardIndex - 1;
                var claimBit = 1u << index;
                if ((claimedAttr.Val & claimBit) != 0 || starCount < awards[index].RequiredStars)
                    return null;

                selected.Add((index, awards[index]));
            }

            if (selected.Count == 0)
                return null;

            var sync = new NtfSyncPlayer();
            var rewardRows = selected.SelectMany(x => x.Award.Rewards).ToArray();
            var grantedRewards = await Player.RewardManager.GrantConfiguredRewardsAsync(rewardRows, sync);
            foreach (var (index, _) in selected)
                claimedAttr.Val |= 1u << index;

            SyncAttr(sync, claimedAttr);
            DatabaseHelper.SaveDatabaseType(Player.Data);
            DatabaseHelper.SaveDatabaseType(Player.InventoryManager.InventoryData);
            DatabaseHelper.SaveDatabaseType(Player.CharacterManager.CharacterData);

            return new ChapterStarAwardResult(
                new JsonObject { ["tbAward"] = grantedRewards },
                sync);
        }
        finally
        {
            settlementLock.Release();
        }
    }

    private IEnumerable<ILevelRewardConfig> GetLevelConfigs(QuestLevelType levelType) =>
        levelType switch
        {
            QuestLevelType.Chapter => GameData.ChapterLevelData.Values,
            QuestLevelType.Daily => GameData.DailyLevelData.Values,
            QuestLevelType.Role => GameData.RoleLevelData.Values,
            _ => []
        };

    private uint GetPassCount(uint levelId) =>
        Player.Data.Attrs.FirstOrDefault(x => x.Gid == LevelPassGroupId && x.Sid == levelId)?.Val ?? 0;

    private uint GetChapterStarCount(ChapterExcel chapter)
    {
        ulong total = 0;
        foreach (var levelId in chapter.Level)
        {
            var value = Player.Data.Attrs
                .FirstOrDefault(x => x.Gid == LevelStateGroupId && x.Sid == levelId)?.Val ?? 0;
            total += (uint)BitOperations.PopCount(value & LevelStarMask);
        }

        return (uint)Math.Min(uint.MaxValue, total);
    }

    private PlayerAttr GetOrCreateAttr(uint gid, uint sid)
    {
        var attr = Player.Data.Attrs.FirstOrDefault(x => x.Gid == gid && x.Sid == sid);
        if (attr != null)
            return attr;

        attr = new PlayerAttr
        {
            Gid = gid,
            Sid = sid
        };
        Player.Data.Attrs.Add(attr);
        return attr;
    }

    private void SyncAttr(NtfSyncPlayer sync, PlayerAttr attr)
    {
        sync.Custom[Player.ToPackedAttrKey(attr.Gid, attr.Sid)] = attr.Val;
        sync.Custom[Player.ToShiftedAttrKey(attr.Gid, attr.Sid)] = attr.Val;
    }

    private static bool TryGetUInt(JsonNode? node, out uint value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<uint>(out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0;
        return false;
    }
}
