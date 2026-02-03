using System;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace NoPolice;

public class ChatHandler : IDisposable
{
    private readonly Configuration _config;

    public ChatHandler(Configuration config)
    {
        _config = config;
        Plugin.ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.ChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        if (isHandled)
            return;

        PlayerPayload? playerPayload = sender.Payloads
            .SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;

        PlayerPayload? emotePlayerPayload = message.Payloads
            .FirstOrDefault(x => x is PlayerPayload) as PlayerPayload;

        bool isEmoteType = type is XivChatType.CustomEmote or XivChatType.StandardEmote;

        if (playerPayload == null && (!isEmoteType || emotePlayerPayload == null))
            return;

        string? playerName = isEmoteType
            ? emotePlayerPayload?.PlayerName
            : playerPayload?.PlayerName;

        if (string.IsNullOrEmpty(playerName))
            return;

        string normalizedName = new string(playerName.Where(char.IsLetter).ToArray())
            .ToLowerInvariant();

        if (_config.BlocklistNames.Contains(normalizedName))
        {
            isHandled = true;
        }
    }
}