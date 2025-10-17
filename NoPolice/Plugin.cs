using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    
    private CancellationTokenSource  _cts = new CancellationTokenSource();
    private List<uint> hiddenPlayersIds = new();
    private List<uint> PlayersToShowIds = new();
    private bool _showing = false;

    private List<string> Police = new()
    {
        "weeaboopolice",
        "weebpolicelieutenant"
    };

    public Plugin()
    {
        Framework.RunOnFrameworkThread(PlayerPoll);
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

                if (!Police.Contains(normalizedName)) continue;
                
                HidePlayer(actor);
            }

            Framework.RunOnTick(PlayerPoll, TimeSpan.FromSeconds(3), cancellationToken: _cts.Token);
        }
        catch (Exception e)
        {
            Framework.RunOnTick(PlayerPoll, TimeSpan.FromSeconds(3), cancellationToken: _cts.Token);
        }
    }

    private unsafe void HidePlayer(IGameObject player)
    {
        var charPtr = (Character*)player.Address;
        
        if (charPtr == null)
            return;

        var flags = (RenderFlags)charPtr->GameObject.RenderFlags;
        
        if(PlayersToShowIds.Contains(player.EntityId)) return;

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
        PlayersToShowIds = hiddenPlayersIds;
        _cts.Cancel();

        ShowGameObjects();
        
        _cts.Dispose();
    }
}