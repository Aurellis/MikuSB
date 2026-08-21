using MikuSB.Database;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

using MikuSB.Data;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Misc;

[CallGSApi("Adjust_Record")]
public class Adjust_Record : ICallGSHandler
{
    private const uint GroupId = AttrIds.Adjust.Gid;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<AdjustRecordParam>(param);
        if (req == null || req.Type == 0)
        {
            await CallGSRouter.SendScript(connection, "Adjust_Record", "null");
            return;
        }

        var player = connection.Player!;
        var sync = new NtfSyncPlayer();
        var attr = player.Attributes.GetOrCreate(GroupId, req.Type);

        if (attr.Val == 0)
        {
            attr.Val = 1;
            player.Attributes.SyncTo(sync, attr);
            DatabaseHelper.SaveDatabaseType(player.Data);
        }

        await CallGSRouter.SendScript(connection, "Adjust_Record", "null", sync);
    }

}

internal sealed class AdjustRecordParam
{
    [JsonPropertyName("nType")]
    public uint Type { get; set; }
}
