using System.Text.Json;
using System.Text.Json.Serialization;
using MikuSB.Data;
using MikuSB.GameServer.Game.Quest;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Role;

// Success response shape expected by Lua: { nSeed = random_number }
[CallGSApi("Role_EnterLevel")]
public class Role_EnterLevel : ICallGSHandler
{
    private static readonly Random _random = new Random();

    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        RoleEnterLevelParam request = JsonSerializer.Deserialize<RoleEnterLevelParam>(param)
            ?? throw new InvalidOperationException("Role_EnterLevel request is empty.");

        if (request.LevelId == 0 || request.TeamId == 0 || !GameData.RoleLevelData.ContainsKey(request.LevelId) ||
            !connection.Player!.QuestManager.CanEnterLevel(QuestLevelType.Role, request.LevelId))
        {
            await CallGSRouter.SendScript(connection, "Role_EnterLevel", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        uint seed = (uint)_random.Next(1, 1000000000);
        connection.Player.BeginLevelSession(QuestLevelType.Role, request.LevelId, seed, request.TeamId);

        string rsp = $"{{\"nSeed\":{seed}}}";
        await CallGSRouter.SendScript(connection, "Role_EnterLevel", rsp);
    }

    private sealed class RoleEnterLevelParam
    {
        [JsonPropertyName("nID")]
        public uint LevelId { get; set; }

        [JsonPropertyName("nTeamID")]
        public uint TeamId { get; set; }
    }
}
