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
using Lumina.Excel.Sheets;

namespace NoPolice;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Logger { get; private set; } = null!;
    
    private readonly CancellationTokenSource  _cts = new();
    private readonly List<uint> hiddenPlayersIds = new();
    private List<uint> _playersToShowIds = new();
    private Configuration _cfg = null!;
    private bool _showing = false;
    
    private static readonly HashSet<uint> allowedTerritories = [
        0, // Hub Cities
        1, // Overworld
        13, // Residential Area
        19, // Unknown
        21, // The Firmament
        23, // Gold Saucer
        44, // Leap of Faith
        46, // Ocean Fishing
        47, // The Diadem
        60, // Stellar Exploration
    ];

    public Plugin()
    {
        _cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        _ = BlockListManager.RefreshBlockList(PluginInterface, _cfg, Logger, _cts);
        
        Framework.RunOnFrameworkThread(PlayerPoll);
    }

    private void PlayerPoll()
    {
        try
        {
            if (_cts.IsCancellationRequested)
                return;

            // Ensure the plugin does not effect combat
            var territoryType = DataManager.GetExcelSheet<TerritoryType>()[ClientState.TerritoryType];
            if (!IsAllowedTerritory(territoryType)) return;
            
            foreach (IGameObject actor in ObjectTable)
            {
                if (actor is not IPlayerCharacter player) continue;

                string name = player.Name.TextValue;
                string normalizedName = new string(name.Where(char.IsLetter).ToArray()).ToLowerInvariant();

                if (!_cfg.BlocklistNames.Contains(normalizedName)) continue;
                
                HidePlayer(actor);
            }

            Framework.RunOnTick(PlayerPoll, TimeSpan.FromSeconds(1), cancellationToken: _cts.Token);
        }
        catch (Exception e)
        {
            Framework.RunOnTick(PlayerPoll, TimeSpan.FromSeconds(1), cancellationToken: _cts.Token);
        }
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
    
    public static bool IsAllowedTerritory(TerritoryType territory)
    {
        return (allowedTerritories.Contains(territory.TerritoryIntendedUse.RowId)) && !territory.Name.IsEmpty;
    }
    
    public void Dispose()
    {
        _cts.Cancel();

        ShowGameObjects();
        
        _cts.Dispose();
    }
}