using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace NoPolice;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    
    private static readonly string Url =
        "https://raw.githubusercontent.com/Noctrepo/NoPolice/refs/heads/master/NoPolice-data/blocklist.txt";
    
    private readonly CancellationTokenSource  _cts = new();
    private readonly List<uint> hiddenPlayersIds = new();
    private List<uint> _playersToShowIds = new();
    private bool _showing = false;

    private HashSet<string> _police = new();

    public Plugin()
    {
        _ = Initialize();
    }

    private async Task Initialize()
    {
        _police = await GetList();
        await Framework.RunOnFrameworkThread(PlayerPoll);
    }

    private void PlayerPoll()
    {
        try
        {
            if (_cts.IsCancellationRequested)
                return;
            
            foreach (IGameObject actor in ObjectTable)
            {
                if (actor is not IPlayerCharacter player) continue;

                string name = player.Name.TextValue;
                string normalizedName = new string(name.Where(char.IsLetter).ToArray()).ToLowerInvariant();

                if (!_police.Contains(normalizedName)) continue;
                
                HidePlayer(actor);
            }

            Framework.RunOnTick(PlayerPoll, TimeSpan.FromSeconds(3), cancellationToken: _cts.Token);
        }
        catch (Exception e)
        {
            Framework.RunOnTick(PlayerPoll, TimeSpan.FromSeconds(3), cancellationToken: _cts.Token);
        }
    }
    
    static async Task<HashSet<string>> GetList()
    {
        using var http = new HttpClient();
        using var resp = await http.GetAsync(Url).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        
        var bannedListRaw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        var bannedListLines = bannedListRaw.Split("\n");
        
        return bannedListLines.Where(bannedListLine => !bannedListLine.StartsWith("#")).ToHashSet();
    }

    private unsafe void HidePlayer(IGameObject player)
    {
        var charPtr = (Character*)player.Address;
        
        if (charPtr == null)
            return;

        var flags = (RenderFlags)charPtr->GameObject.RenderFlags;
        
        if(_playersToShowIds.Contains(player.EntityId)) return;

        if (!flags.HasFlag(RenderFlags.Invisible))
        {
            charPtr->GameObject.RenderFlags |= (int)RenderFlags.Invisible;
        }
        
        hiddenPlayersIds.Add(player.EntityId);
    }

    private unsafe void ShowGameObjects()
    {
        foreach (var id in hiddenPlayersIds.ToList())
        {
            var obj = ObjectTable.SearchById(id);
            if (obj == null) continue;

            var character = (Character*)obj.Address;
            
            if (character != null)
                character->GameObject.RenderFlags = (int)RenderFlags.None;

            hiddenPlayersIds.Remove(id);
        }
    }

    
    public void Dispose()
    {
        _playersToShowIds = hiddenPlayersIds;
        _cts.Cancel();

        ShowGameObjects();
        
        _cts.Dispose();
    }
}