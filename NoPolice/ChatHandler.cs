using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace NoPolice;

public class ChatHandler : IDisposable
{
    private readonly Configuration _config;
    private readonly Dictionary<string, string> _nameCache;

    public ChatHandler(Configuration config, Dictionary<string, string> nameCache)
    {
        _config = config;
        _nameCache = nameCache;
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
        
        if (_config.BlocklistNames.Count == 0)
            return;

        PlayerPayload? playerPayload = null;
        foreach (var payload in sender.Payloads)
        {
            if (payload is not PlayerPayload pp) continue;
            
            playerPayload = pp;
            break;
        }

        PlayerPayload? emotePlayerPayload = null;
        bool isEmoteType = type is XivChatType.CustomEmote or XivChatType.StandardEmote;

        if (isEmoteType)
        {
            foreach (var payload in message.Payloads)
            {
                if (payload is not PlayerPayload pp) continue;
                
                emotePlayerPayload = pp;
                break;
            }
        }

        if (playerPayload == null && (!isEmoteType || emotePlayerPayload == null))
            return;

        string? playerName = isEmoteType
            ? emotePlayerPayload?.PlayerName
            : playerPayload?.PlayerName;

        if (string.IsNullOrEmpty(playerName))
            return;
        
        if (!_nameCache.TryGetValue(playerName, out var normalizedName))
        {
            normalizedName = NormalizeName(playerName);
            _nameCache[playerName] = normalizedName;
        }

        if (_config.BlocklistNames.Contains(normalizedName))
        {
            isHandled = true;
        }
    }

    private static string NormalizeName(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        int writeIndex = 0;

        foreach (char c in name)
        {
            if (char.IsLetter(c))
            {
                buffer[writeIndex++] = char.ToLowerInvariant(c);
            }
        }

        return new string(buffer[..writeIndex]);
    }
}