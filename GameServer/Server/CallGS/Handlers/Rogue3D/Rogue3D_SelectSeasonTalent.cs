using MikuSB.Proto;
using MikuSB.GameServer.Game.Player;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Rogue3D;

// Selects the Rogue3D season talent and persists it as player attribute (GroupId=124, TalentId=1007).
// param: {"nTalentId": int}
// Response: {} on success, {"sErr": "key"} on failure
[CallGSApi("Rogue3D_SelectSeasonTalent")]
public class Rogue3D_SelectSeasonTalent : ICallGSHandler
{
    private const uint GroupId = AttrIds.Rogue3D.Gid;
    private const uint SeasonTalentIdSid = AttrIds.Rogue3D.SeasonTalentIdSid;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<SelectSeasonTalentParam>(param);
        if (req == null)
        {
            await CallGSRouter.SendScript(connection, "Rogue3D_SelectSeasonTalent", "{}");
            return;
        }

        var player = connection.Player!;
        var attr = player.Attributes.GetOrCreate(GroupId, SeasonTalentIdSid);
        attr.Val = req.TalentId;

        var sync = new NtfSyncPlayer();
        player.Attributes.SyncTo(sync, attr);

        await CallGSRouter.SendScript(connection, "Rogue3D_SelectSeasonTalent", "{}", sync);
    }
}

internal sealed class SelectSeasonTalentParam
{
    [JsonPropertyName("nTalentId")]
    public uint TalentId { get; set; }
}
