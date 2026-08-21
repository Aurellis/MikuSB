using MikuSB.Database.Player;
using MikuSB.Proto;
using MikuSB.GameServer.Game.Player;
using System.Text.Json;
using System.Text.Json.Serialization;

using MikuSB.Data;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Scene;

// Response:{sErr:true or false}
[CallGSApi("ChangeMainScene")]
public class ChangeMainScene : ICallGSHandler
{
    private const uint MainSceneGID = AttrIds.Scene.MainGid;
    private const uint MainSceneSID = AttrIds.Scene.MainSid;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        string rsp = $"{{\"sErr\":false}}";
        var req = JsonSerializer.Deserialize<ChangeMainSceneParam>(param);
        if (req == null) 
        {
            await CallGSRouter.SendScript(connection, "ChangeMainScene", rsp);
            return;
        } 

        var player = connection.Player!;
        var mainSceneAttr = player.Attributes.GetOrCreate(MainSceneGID, MainSceneSID);
        var sync = new NtfSyncPlayer();
        mainSceneAttr.Val = req.Id;

        player.Attributes.SyncTo(sync, mainSceneAttr);
        await CallGSRouter.SendScript(connection, "ChangeMainScene", rsp, sync);
    }
}

internal sealed class ChangeMainSceneParam
{
    [JsonPropertyName("nId")]
    public uint Id { get; set; }
}
