using System.Text.Json.Nodes;
using MikuSB.GameServer.Game.Quest;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Chapter;

[CallGSApi("Chapter_SyncGuideLevelPassData")]
public class Chapter_SyncGuideLevelPassData : ICallGSHandler
{
    public Task Handle(Connection connection, string param, ushort seqNo)
    {
        var payload = JsonNode.Parse(param);
        connection.Player!.QuestManager.SyncGuideLevelPassData(payload);
        return Task.CompletedTask;
    }
}
