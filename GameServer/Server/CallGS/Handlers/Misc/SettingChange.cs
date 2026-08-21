using MikuSB.Proto;
using MikuSB.GameServer.Game.Player;
using System.Text.Json;
using System.Text.Json.Serialization;

using MikuSB.Data;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Misc;

[CallGSApi("SettingChange")]
public class SettingChange : ICallGSHandler
{
    private const uint PlayerSettingGid = AttrIds.Settings.Gid;

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var changes = JsonSerializer.Deserialize<List<SettingChangeParam>>(param) ?? [];
        var player = connection.Player!;
        var sync = new NtfSyncPlayer();

        foreach (var change in changes)
        {
            var value = player.Attributes.GetStringValue(PlayerSettingGid, change.Id);

            if (value == null)
                continue;

            player.Attributes.SyncTo(sync, player.Attributes.GetString(PlayerSettingGid, change.Id)!);
        }

        if (sync.CustomStr.Count > 0)
            await connection.SendPacket(CmdIds.NtfSyncAttr, sync);
    }
}

internal sealed class SettingChangeParam
{
    [JsonPropertyName("id")]
    public uint Id { get; set; }
}
