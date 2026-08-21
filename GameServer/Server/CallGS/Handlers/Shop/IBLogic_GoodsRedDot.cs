using MikuSB.Database;
using MikuSB.GameServer.Game.Player;
using MikuSB.Proto;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Shop;

[CallGSApi("IBLogic_GoodsRedDot")]
public class IBLogic_GoodsRedDot : ICallGSHandler
{
    private const uint RedGroupId = AttrIds.Shop.RedDotGid;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<IbGoodsRedDotParam>(param);
        if (req?.GoodsIds == null || req.GoodsIds.Count == 0)
        {
            await CallGSRouter.SendScript(connection, "IBLogic_GoodsRedDot", "null");
            return;
        }

        var player = connection.Player!;
        var sync = new NtfSyncPlayer();
        var changed = false;

        foreach (var goodsId in req.GoodsIds.Where(x => x > 0).Distinct())
        {
            var attr = player.Attributes.GetOrCreate(RedGroupId, goodsId);
            if (attr.Val > 0)
                continue;

            attr.Val = 1;
            player.Attributes.SyncTo(sync, attr);
            changed = true;
        }

        if (changed)
            DatabaseHelper.SaveDatabaseType(player.Data);

        await CallGSRouter.SendScript(connection, "IBLogic_GoodsRedDot", "null", sync);
    }

}

internal sealed class IbGoodsRedDotParam
{
    [JsonPropertyName("tbList")]
    public List<uint> GoodsIds { get; set; } = [];
}
