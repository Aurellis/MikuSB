using System.Text.Json;
using System.Text.Json.Serialization;
using MikuSB.GameServer.Game.Quest;

namespace MikuSB.GameServer.Server.CallGS.Handlers.Chapter;

[CallGSApi("Chapter_GetStarAward")]
public sealed class Chapter_GetStarAward : ICallGSHandler
{
    public async Task Handle(Connection connection, string param, ushort seqNo)
    {
        var request = JsonSerializer.Deserialize<ChapterStarAwardParam>(param);
        if (request == null || request.ChapterId == 0 || request.Difficult == 0)
        {
            await CallGSRouter.SendScript(connection, "Chapter_GetStarAward", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        var result = await connection.Player!.QuestManager.ClaimChapterStarAwardsAsync(
            request.IsMain,
            request.Difficult,
            request.ChapterId,
            request.AwardIndex);
        if (result == null)
        {
            await CallGSRouter.SendScript(connection, "Chapter_GetStarAward", "{\"sErr\":\"error.BadParam\"}");
            return;
        }

        await CallGSRouter.SendScript(
            connection,
            "Chapter_GetStarAward",
            result.Value.Response.ToJsonString(),
            result.Value.Sync);
    }
}

internal sealed class ChapterStarAwardParam
{
    [JsonPropertyName("bMain")]
    public bool IsMain { get; set; }

    [JsonPropertyName("nDifficult")]
    public uint Difficult { get; set; }

    [JsonPropertyName("nChapterID")]
    public uint ChapterId { get; set; }

    [JsonPropertyName("nIndex")]
    public int AwardIndex { get; set; }
}
