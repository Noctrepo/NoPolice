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
using FFXIVClientStructs.FFXIV.Client.Game.Object;
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
    
    private TerritoryType _territoryType;
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

        ClientState.TerritoryChanged += TerritoryChange;
        
        Framework.RunOnFrameworkThread(Poll);
    }

    private void TerritoryChange(ushort @ushort)
    {
        try
        {
            var territoryType = DataManager.GetExcelSheet<TerritoryType>()[ClientState.TerritoryType];
            if (!IsAllowedTerritory(territoryType)) return;
            CheckPlayers();
        }
        catch (Exception e)
        {
            Logger.Logger.Error(e.ToString());
        }
    }

    private void Poll()
    {
        try
        {
            if (_cts.IsCancellationRequested)
                return;

            CheckPlayers();

            Framework.RunOnTick(Poll, TimeSpan.FromSeconds(1), cancellationToken: _cts.Token);
        }
        catch (Exception e)
        {
            Logger.Logger.Error(e.ToString());
            Framework.RunOnTick(Poll, TimeSpan.FromSeconds(1), cancellationToken: _cts.Token);
        }
    }

    private void CheckPlayers()
    {
        try
        {
            foreach (IGameObject actor in ObjectTable)
            {
                if (actor is not IPlayerCharacter player) continue;

                string name = player.Name.TextValue;
                string normalizedName = new string(name.Where(char.IsLetter).ToArray()).ToLowerInvariant();

                if (!_cfg.BlocklistNames.Contains(normalizedName)) continue;

                HidePlayer(actor);
            }
        }
        catch (Exception e)
        {
            Logger.Logger.Error(e.ToString());
        }
    }

    private unsafe void HidePlayer(IGameObject player)
    {
        try
        {
            var charPtr = (Character*)player.Address;

            if (charPtr == null)
                return;

            var flags = (RenderFlags)charPtr->GameObject.RenderFlags;

            if (_playersToShowIds.Contains(player.EntityId)) return;

            if (!flags.HasFlag(RenderFlags.Invisible))
            {
                charPtr->GameObject.RenderFlags |= (VisibilityFlags)RenderFlags.Invisible;
            }

            hiddenPlayersIds.Add(player.EntityId);
        }
        catch (Exception e)
        {
            Logger.Logger.Error(e.ToString());
        }
    }

    private unsafe void ShowGameObjects()
    {
        try
        {
            foreach (var id in hiddenPlayersIds.ToList())
            {
                var obj = ObjectTable.SearchById(id);
                if (obj == null) continue;

                var character = (Character*)obj.Address;

                if (character != null)
                    character->GameObject.RenderFlags = (VisibilityFlags)RenderFlags.None;

                hiddenPlayersIds.Remove(id);
            }
        }
        catch (Exception e)
        {
            Logger.Logger.Error(e.ToString());
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