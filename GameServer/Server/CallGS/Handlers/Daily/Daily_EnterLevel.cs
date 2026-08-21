using System.Text.Json;
using System.Text.Json.Serialization;
using MikuSB.Data;
using MikuSB.GameServer.Game.Quest;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Daily;

// Success response shape expected by Lua:
// { nSeed = random_number }
[CallGSApi("Daily_EnterLevel")]
public class Daily_EnterLevel : ICallGSHandler
{
    private static readonly Random Random = new();

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var req = JsonSerializer.Deserialize<DailyEnterLevelParam>(param);
        if (req == null || req.LevelId == 0 || req.TeamId == 0 || !GameData.DailyLevelData.ContainsKey(req.LevelId) ||
            !connection.Player!.QuestManager.CanEnterLevel(QuestLevelType.Daily, req.LevelId))
        {
            await CallGSRouter.SendScript(connection, "Daily_EnterLevel", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var seed = (uint)Random.Next(1, 1000000000);
        connection.Player.BeginLevelSession(QuestLevelType.Daily, req.LevelId, seed, req.TeamId);
        var rsp = $"{{\"nSeed\":{seed}}}";
        await CallGSRouter.SendScript(connection, "Daily_EnterLevel", rsp);
    }
}

internal sealed class DailyEnterLevelParam
{
    [JsonPropertyName("nLevelID")]
    public uint LevelId { get; set; }

    [JsonPropertyName("nTeamID")]
    public uint TeamId { get; set; }
}
