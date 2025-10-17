using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace NoPolice;

public sealed class Plugin : IDalamudPlugin
{
    
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    
    private CancellationTokenSource  _cts = new CancellationTokenSource();

    private List<string> Police = new()
    {
        "weeaboopolice",
        "weebpolicelieutenant"
    };

    public Plugin()
    {
        if (!PluginInterface.GetIpcSubscriber<string, uint, string, object>("Visibility.AddToVoidList").HasFunction)
        {
            Chat.PrintError("Visibility plugin is required for this plugin to work.", "NoPolice");
        }
        
        Framework.RunOnFrameworkThread(VisibilityPoll);
        
        ClientState.Login += OnLogin;
    }

    private void OnLogin()
    {
        if (!PluginInterface.GetIpcSubscriber<string, uint, string, object>("Visibility.AddToVoidList").HasFunction)
        {
            Chat.PrintError("Visibility plugin is required for this plugin to work.", "NoPolice");
        }
    }

    private void VisibilityPoll()
    {
        try
        {
            ICallGateSubscriber<string, uint, string, object>? AddToVoidList = null;
            ICallGateSubscriber<IEnumerable<string>>? GetVoidListEntries = null;
            List<string> VisibilityVoidList = new();

            AddToVoidList = PluginInterface.GetIpcSubscriber<string, uint, string, object>("Visibility.AddToVoidList");
            GetVoidListEntries = PluginInterface.GetIpcSubscriber<IEnumerable<string>>("Visibility.GetVoidListEntries");
            VisibilityVoidList = GetVoidListEntries.InvokeFunc().ToList();

            foreach (IGameObject actor in ObjectTable)
            {
                if (actor is not IPlayerCharacter player) continue;

                string name = player.Name.TextValue;
                string pvisName = $"{player.Name.TextValue} {player.HomeWorld.RowId} NoPolice";

                if (!Police.Contains(NormalizeName(name))) continue;
                if (VisibilityVoidList.Contains(pvisName)) continue;

                AddToVoidList!.InvokeAction(name, player.HomeWorld.RowId, "NoPolice");
                VisibilityVoidList = GetVoidListEntries.InvokeFunc().ToList();
            }

            Framework.RunOnTick(VisibilityPoll, TimeSpan.FromSeconds(3), cancellationToken: _cts.Token);
        }
        catch (Exception e)
        {
            Framework.RunOnTick(VisibilityPoll, TimeSpan.FromSeconds(3), cancellationToken: _cts.Token);
        }
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