using MikuSB.Proto;
using MikuSB.GameServer.Game.Player;
using System.Text.Json;
using System.Text.Json.Serialization;

using MikuSB.Data;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Rogue3D;

// Selects the Rogue3D talent and persists it as player attribute (GroupId=124, TalentId=7).
// param: {"nTalentId": int}
// Response: {} on success, {"sErr": "key"} on failure
[CallGSApi("Rogue3D_SelectTalent")]
public class Rogue3D_SelectTalent : ICallGSHandler
{
    private const uint GroupId = AttrIds.Rogue3D.Gid;
    private const uint TalentIdSid = AttrIds.Rogue3D.TalentIdSid;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<SelectTalentParam>(param);
        if (req == null)
        {
            await CallGSRouter.SendScript(connection, "Rogue3D_SelectTalent", "{}");
            return;
        }

        var player = connection.Player!;
        var attr = player.Attributes.GetOrCreate(GroupId, TalentIdSid);
        attr.Val = req.TalentId;

        var sync = new NtfSyncPlayer();
        player.Attributes.SyncTo(sync, attr);

        await CallGSRouter.SendScript(connection, "Rogue3D_SelectTalent", "{}", sync);
    }
}

internal sealed class SelectTalentParam
{
    [JsonPropertyName("nTalentId")]
    public uint TalentId { get; set; }
}
