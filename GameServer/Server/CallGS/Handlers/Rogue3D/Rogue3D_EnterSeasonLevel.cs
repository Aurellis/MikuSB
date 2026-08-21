using MikuSB.Data;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Rogue3D;

// Enters the Rogue3D season level. Returns a random seed used by the client for map generation.
// Persists SeasonGameplayId (sid=1006) and SeasonEnterFlag (sid=1008) as player attributes (GroupId=124).
// param: {"nDiffId", "nTeamID", "tbTeam", "tbBuffList", "tbLog"}
// Response: {"nSeed": int} on success, {"sErr": "key"} on failure
[CallGSApi("Rogue3D_EnterSeasonLevel")]
public class Rogue3D_EnterSeasonLevel : ICallGSHandler
{
    private const uint GroupId = AttrIds.Rogue3D.Gid;
    private const uint SeasonGameplayIdSid = AttrIds.Rogue3D.SeasonGameplayIdSid;
    private const uint SeasonEnterFlagSid = AttrIds.Rogue3D.SeasonEnterFlagSid;
    private static readonly Random Random = new();

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<EnterSeasonLevelParam>(param);
        if (req == null)
        {
            await CallGSRouter.SendScript(connection, "Rogue3D_EnterSeasonLevel", "{\"nSeed\":0}");
            return;
        }

        if (!GameData.Rogue3DDifficultData.TryGetValue(req.DiffId, out var cfg) || cfg.GameplayGroup.Count == 0)
        {
            await CallGSRouter.SendScript(connection, "Rogue3D_EnterSeasonLevel", "{\"sErr\":\"rogue3.massage_gameProcessError\"}");
            return;
        }

        var player = connection.Player!;
        var sync = new NtfSyncPlayer();

        SetAttr(player, SeasonGameplayIdSid, cfg.GameplayGroup[0], sync);
        SetAttr(player, SeasonEnterFlagSid, 1, sync);

        var seed = Random.Next(1, 1_000_000_000);
        await CallGSRouter.SendScript(connection, "Rogue3D_EnterSeasonLevel", $"{{\"nSeed\":{seed}}}", sync);
    }

    private static void SetAttr(PlayerInstance player, uint sid, uint val, NtfSyncPlayer sync)
    {
        var attr = player.Attributes.GetOrCreate(GroupId, sid);

        if (attr.Val == val)
        {
            return;
        }

        attr.Val = val;
        player.Attributes.SyncTo(sync, attr);
    }
}

internal sealed class EnterSeasonLevelParam
{
    [JsonPropertyName("nDiffId")]
    public uint DiffId { get; set; }
}
