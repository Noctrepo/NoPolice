using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace NoPolice;

public sealed class Plugin : IDalamudPlugin
{
    
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    
    private ICallGateSubscriber<string, uint, string, object> AddToVoidList;
    private ICallGateSubscriber<IEnumerable<string>> GetVoidListEntries;
    private readonly CancellationTokenSource _cts = new();
    private List<string> HiddenPlayers = new();
    private DateTime lastScan;
    
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            AddToVoidList = PluginInterface.GetIpcSubscriber<string, uint, string, object>("Visibility.AddToVoidList");
            
            GetVoidListEntries = PluginInterface.GetIpcSubscriber<IEnumerable<string>>("Visibility.GetVoidListEntries");
            HiddenPlayers = GetVoidListEntries.InvokeFunc().ToList();

            PluginLog.Information("NoPolice Connected to PVis successfully.");
        }
        catch
        {
            PluginLog.Warning("PVis not loaded.");
            Chat.Print("Visibility by SheepGoMeh required for this plugin to function.");
        }

        Framework.RunOnFrameworkThread(PollPlayers);
    }
    
    private void PollPlayers()
    {
        foreach (var actor in ObjectTable)
        {
            if (actor is IPlayerCharacter player)
            {
                string name = player.Name.TextValue;
                string pvisName = $"{player.Name.TextValue} {player.HomeWorld.RowId} NoPolice";

                if (NormalizeName(name) == "weeaboopolice")
                {
                    if (!HiddenPlayers.Contains(pvisName))
                    {
                        AddToVoidList.InvokeAction(name, player.HomeWorld.RowId, "NoPolice");
                    }

                    HiddenPlayers = GetVoidListEntries.InvokeFunc().ToList();
                }
                
            }
        }
        
        Framework.RunOnTick(PollPlayers, TimeSpan.FromSeconds(3), cancellationToken: _cts.Token);
    }
    
    private static string NormalizeName(string name)
    {
        return new string(name.Where(char.IsLetter).ToArray()).ToLowerInvariant();
    }
    
    public void Dispose()
    {
        _cts.Dispose();
    }
}