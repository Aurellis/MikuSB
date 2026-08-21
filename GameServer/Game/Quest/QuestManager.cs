using System.Numerics;
using System.Text.Json.Nodes;
using MikuSB.Data;
using MikuSB.Data.Excel;
using MikuSB.Database;
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
    private const uint LevelStateGroupId = AttrIds.Quest.LevelStateGid;
    private const uint LevelPassGroupId = AttrIds.Quest.LevelPassGid;
    private const uint SettlementSeedGroupId = AttrIds.Quest.SettlementSeedGid;
    private const uint ChapterStarAwardGroupId = AttrIds.Quest.ChapterStarAwardGid;
    private const uint ChapterStarAwardMaskVersionSid = AttrIds.Quest.ChapterStarAwardMaskVersionSid;
    private const uint ChapterStarAwardMaskVersion = 1;
    private const uint LevelStarMask = 0b111;
    private const int MaxChapterStarAwardIndex = 31;
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
            var passAttr = Player.Attributes.Get(LevelPassGroupId, levelId);
            if (passAttr?.Val != LegacyUnlockedLevelPassTime)
                continue;

            Player.Attributes.Remove(LevelPassGroupId, levelId);

            Player.Attributes.Remove(LevelStateGroupId, levelId);
        }
    }

    public void MigrateChapterStarAwardMasks()
    {
        var versionAttr = Player.Attributes.Get(
            ChapterStarAwardGroupId,
            ChapterStarAwardMaskVersionSid);
        if (versionAttr?.Val >= ChapterStarAwardMaskVersion)
            return;

        foreach (var attr in Player.Attributes.All.Where(x =>
                     x.Gid == ChapterStarAwardGroupId && x.Sid != ChapterStarAwardMaskVersionSid))
        {
            attr.Val <<= 1;
        }

        if (versionAttr == null)
            versionAttr = Player.Attributes.GetOrCreate(
                ChapterStarAwardGroupId,
                ChapterStarAwardMaskVersionSid);

        versionAttr.Val = ChapterStarAwardMaskVersion;
        DatabaseHelper.SaveDatabaseType(Player.Data);
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
            var levelPass = Player.Attributes.GetOrCreate(LevelPassGroupId, levelId);
            var settlementSeed = Player.Attributes.GetOrCreate(SettlementSeedGroupId, levelId);
            var levelState = Player.Attributes.GetOrCreate(LevelStateGroupId, levelId);
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
            Player.Attributes.SyncTo(sync, levelState);
            Player.Attributes.SyncTo(sync, levelPass);
            settlementSeed.Val = seed;
            Player.Attributes.SyncTo(sync, settlementSeed);

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
            var levelState = Player.Attributes.GetOrCreate(LevelStateGroupId, levelId);
            if ((levelState.Val & (1u << 8)) != 0)
                return new QuestSettlementResult(new JsonArray(), Player.RewardManager.BuildFullSync());

            var levelPass = Player.Attributes.GetOrCreate(LevelPassGroupId, levelId);
            var sync = new NtfSyncPlayer();
            await Player.RewardManager.GrantLevelRewardsAsync(levelConfig, true, levelId, sync);

            levelState.Val |= 1u << 8;
            if (levelPass.Val == 0)
                levelPass.Val = 1;

            Player.Attributes.SyncTo(sync, levelState);
            Player.Attributes.SyncTo(sync, levelPass);

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
            var attr = Player.Attributes.GetOrCreate(LevelPassGroupId, levelId);
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
            var claimedAttr = Player.Attributes.GetOrCreate(
                ChapterStarAwardGroupId,
                (chapterId << 8) | difficult);
            var starCount = GetChapterStarCount(chapter);
            var selected = new List<(int Index, ChapterStarAward Award)>();

            if (awardIndex == -1)
            {
                for (var i = 0; i < awards.Count && i < MaxChapterStarAwardIndex; i++)
                {
                    var index = i + 1;
                    var claimBit = 1u << index;
                    if ((claimedAttr.Val & claimBit) == 0 && starCount >= awards[i].RequiredStars)
                        selected.Add((index, awards[i]));
                }
            }
            else
            {
                if (awardIndex < 1 || awardIndex > awards.Count || awardIndex > MaxChapterStarAwardIndex)
                    return null;

                var index = awardIndex - 1;
                var claimBit = 1u << awardIndex;
                if ((claimedAttr.Val & claimBit) != 0 || starCount < awards[index].RequiredStars)
                    return null;

                selected.Add((awardIndex, awards[index]));
            }

            if (selected.Count == 0)
                return null;

            var sync = new NtfSyncPlayer();
            var rewardRows = selected.SelectMany(x => x.Award.Rewards).ToArray();
            var grantedRewards = await Player.RewardManager.GrantConfiguredRewardsAsync(rewardRows, sync);
            foreach (var (index, _) in selected)
                claimedAttr.Val |= 1u << index;

            Player.Attributes.SyncTo(sync, claimedAttr);
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
        Player.Attributes.GetValue(LevelPassGroupId, levelId);

    private uint GetChapterStarCount(ChapterExcel chapter)
    {
        ulong total = 0;
        foreach (var levelId in chapter.Level)
        {
            var value = Player.Attributes.GetValue(LevelStateGroupId, levelId);
            total += (uint)BitOperations.PopCount(value & LevelStarMask);
        }

        return (uint)Math.Min(uint.MaxValue, total);
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
