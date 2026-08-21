using MikuSB.Database.Player;
using MikuSB.GameServer.Server.CallGS.Handlers.Misc;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Preview;

[CallGSApi("RecordConfession")]
public class RecordConfession : ICallGSHandler
{
    private const uint MainSceneGID = AttrIds.Scene.MainGid;
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<RecordConfessionParam>(param);
        if (req == null) return;
        var sid = req.Id + 10;
        var player = connection.Player!;
        var attr = player.Attributes.Set(MainSceneGID, sid, 1);
        var sync = new NtfSyncPlayer();
        player.Attributes.SyncTo(sync, attr);
        await CallGSRouter.SendScript(connection, "RecordConfession", "{}", sync);
    }
}

internal sealed class RecordConfessionParam
{
    [JsonPropertyName("nIdx")]
    public uint Id { get; set; }
}
