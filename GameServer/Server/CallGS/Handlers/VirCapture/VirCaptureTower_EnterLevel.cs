using MikuSB.Data;
using MikuSB.GameServer.Game.Player;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MikuSB.GameServer.Server.CallGS.Handlers.VirCapture;

[CallGSApi("VirCaptureTower_EnterLevel")]
public class VirCaptureTower_EnterLevel : ICallGSHandler
{
    private const uint LaunchPassGroupId = AttrIds.Tower.PassGid;
    private const uint VirCaptureGroupId = AttrIds.VirCapture.Gid;
    private const uint VirCaptureLevelSid = AttrIds.VirCapture.CurrentLevelSid;
    private static readonly Random Random = new();

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<VirCaptureTowerEnterLevelParam>(param);
        if (req == null || req.LevelId <= 0 || req.TeamId <= 0)
        {
            await CallGSRouter.SendScript(connection, "VirCaptureTower_EnterLevel", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        if (!GameData.VirCaptureTowerData.TryGetValue((uint)req.LevelId, out var levelCfg))
        {
            await CallGSRouter.SendScript(connection, "VirCaptureTower_EnterLevel", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var player = connection.Player!;
        if (!CheckConditions(player, levelCfg.Condition))
        {
            await CallGSRouter.SendScript(connection, "VirCaptureTower_EnterLevel", "{\"sErr\":\"tip.LevelLocked\"}");
            return;
        }

        await CallGSRouter.SendScript(connection, "VirCaptureTower_EnterLevel", $"{{\"nSeed\":{Random.Next(1, 1_000_000_000)}}}");
    }

    private static bool CheckConditions(PlayerInstance player, IReadOnlyDictionary<int, uint> conditions)
    {
        foreach (var (key, value) in conditions)
        {
            switch (key)
            {
                case 1:
                    if (player.Data.Level < value)
                        return false;
                    break;
                case 2:
                {
                    var pass = player.Attributes.GetValue(LaunchPassGroupId, value);
                    if (pass == 0)
                        return false;
                    break;
                }
                case 20:
                {
                    var virLevel = player.Attributes.GetValue(VirCaptureGroupId, VirCaptureLevelSid);
                    if (virLevel < value)
                        return false;
                    break;
                }
            }
        }

        return true;
    }
}

internal sealed class VirCaptureTowerEnterLevelParam
{
    [JsonPropertyName("nID")]
    public int LevelId { get; set; }

    [JsonPropertyName("nTeamID")]
    public int TeamId { get; set; }
}
