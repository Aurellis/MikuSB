using MikuSB.Data;
using MikuSB.Database;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Tower;

[CallGSApi("ClimbTowerLogic_SetLevelDiff")]
public class ClimbTowerLogic_SetLevelDiff : ICallGSHandler
{
    private const uint TowerGroupId = AttrIds.Tower.Gid;
    private const uint DiffSid = AttrIds.Tower.DiffSid;
    private const uint HisDiffSid = AttrIds.Tower.HistoryDiffSid;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var player = connection.Player!;
        var req = JsonSerializer.Deserialize<ClimbTowerSetLevelDiffParam>(param);
        if (req == null || req.Diff <= 0)
        {
            await CallGSRouter.SendScript(connection, "ClimbTowerLogic_SetLevelDiff", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        if (!GameData.ClimbTowerDiffData.ContainsKey((uint)req.Diff))
        {
            await CallGSRouter.SendScript(connection, "ClimbTowerLogic_SetLevelDiff", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var hisDiff = player.Attributes.GetValue(TowerGroupId, HisDiffSid);
        if (req.Diff > hisDiff + 1)
        {
            await CallGSRouter.SendScript(connection, "ClimbTowerLogic_SetLevelDiff", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var diffAttr = player.Attributes.GetOrCreate(TowerGroupId, DiffSid);
        diffAttr.Val = (uint)req.Diff;

        var sync = new NtfSyncPlayer();
        player.Attributes.SyncTo(sync, diffAttr);

        DatabaseHelper.SaveDatabaseType(player.Data);
        await CallGSRouter.SendScript(connection, "ClimbTowerLogic_SetLevelDiff", "{}", sync);
    }

}

internal sealed class ClimbTowerSetLevelDiffParam
{
    [JsonPropertyName("nDiff")]
    public int Diff { get; set; }
}
