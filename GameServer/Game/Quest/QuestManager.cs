using MikuSB.Data;
using MikuSB.Database;
using MikuSB.Database.Player;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using MikuSB.Util;

namespace MikuSB.GameServer.Game.Quest;

public class QuestManager(PlayerInstance player) : BasePlayerManager(player)
{
    private const uint LevelStateGroupId = 21;
    private const uint LevelPassGroupId = 22;
    private const uint LevelStarMask = 0b111;
    private const uint LegacyUnlockedLevelPassTime = 1_700_000_000;
    private static readonly Logger Logger = new("Quest");

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

    public NtfSyncPlayer SettleLevel(uint levelId, int starMask)
    {
        var sync = new NtfSyncPlayer();
        var levelState = GetOrCreateAttr(LevelStateGroupId, levelId);
        levelState.Val |= (uint)starMask & LevelStarMask;
        SyncAttr(sync, levelState);

        var levelPass = GetOrCreateAttr(LevelPassGroupId, levelId);
        levelPass.Val = Math.Max(1u, levelPass.Val + 1);
        SyncAttr(sync, levelPass);

        Logger.Info($"Level settlement saved. uid={Player.Uid} levelId={levelId} " +
                    $"starMask={starMask} stateVal={levelState.Val} passVal={levelPass.Val}");

        DatabaseHelper.SaveDatabaseType(Player.Data);
        return sync;
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
}
