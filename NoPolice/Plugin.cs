using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace NoPolice;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Logger { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    
    private readonly CancellationTokenSource  _cts = new();
    
    private readonly Dictionary<string, string> _nameCache = new();
    private readonly HashSet<uint> hiddenPlayersIds = new();
    private List<uint> _playersToShowIds = new();
    
    private Configuration _cfg;
    private ChatHandler _chatHandler;

    private List<ConditionFlag> forbiddenConditions = new()
    {
        ConditionFlag.InCombat,
        ConditionFlag.BetweenAreas,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.DutyRecorderPlayback,
        ConditionFlag.BoundByDuty
    };

    private readonly VisibilityFlags Invisible = VisibilityFlags.Model | VisibilityFlags.Nameplate;

    public Plugin()
    {
        _cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        _chatHandler = new ChatHandler(_cfg, _nameCache);

        _ = BlockListManager.RefreshBlockList(PluginInterface, _cfg, Logger, _cts);

        Framework.Update += CheckPlayers;
    }

    private void CheckPlayers(IFramework framework)
    {
        try
        {
            unsafe
            {
                bool badCondition = forbiddenConditions.Any(flag => Condition[flag]);
                    
                if (badCondition) return;
            
                if (_cfg.BlocklistNames.Count == 0) return;

                foreach (var gameObj in ObjectTable)
                {
                    GameObject* gameObject = GameObjectManager.Instance()->Objects.GameObjectIdSorted[gameObj.ObjectIndex];
                    
                    if ((ObjectKind)gameObject->ObjectKind != ObjectKind.Pc) continue;
                    Character* characterPtr = (Character*)gameObject;
                    
                    if (gameObject->NameString.IsNullOrEmpty()) continue;

                    var name = gameObject->NameString;
                
                    if (!_nameCache.TryGetValue(name, out var normalizedName))
                    {
                        normalizedName = NormalizeName(name);
                        _nameCache[name] = normalizedName;
                    }

                    List<string> testNames = new List<string>() { "" };
                    
                    if (normalizedName.IsNullOrEmpty()) continue;
                    if (!_cfg.BlocklistNames.Contains(normalizedName) && !testNames.Contains(normalizedName)) continue;

                    HidePlayer(characterPtr);
                }
            }
        }
        catch (Exception e)
        {
            // ignored
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

    private unsafe void HidePlayer(Character* charPtr)
    {
        try
        {
            if (charPtr == null)
                return;
            
            if (_playersToShowIds.Contains(charPtr->EntityId)) return;
            
            if(charPtr->GameObject.RenderFlags.HasFlag(Invisible))
                return;
            
            charPtr->GameObject.RenderFlags |= (VisibilityFlags)RenderFlags.Invisible;
            hiddenPlayersIds.Add(charPtr->EntityId);
            
            foreach (var obj in ObjectTable)
            {
                if (obj.OwnerId != charPtr->EntityId) continue;

                var companionPtr = (GameObject*)obj.Address;
                if (companionPtr == null) continue;

                if (companionPtr->RenderFlags.HasFlag(Invisible)) continue;

                companionPtr->RenderFlags |= (VisibilityFlags)RenderFlags.Invisible;
                hiddenPlayersIds.Add(companionPtr->EntityId);
            }
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
                
                if(!character->GameObject.RenderFlags.HasFlag(Invisible)) return;

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
    
    public void Dispose()
    {
        _cts.Cancel();
        _chatHandler?.Dispose();
        ShowGameObjects();
        _cts.Dispose();
    }
}