using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace zukeQOL.zukeQOLCode;

/// <summary>
///     Adds a panel to the left of the player's hitbox showing information
///     about incoming attack damage this turn.
/// </summary>
[HarmonyPatch]
public static class IncomingDamageDisplay
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private static NHealthBar? _playerHealthBar;    // Reference to the player's NHealthBar node
    private static NCreature? _playerCreatureNode;  // Reference to the players NCreature node
    
    private static IncomingDamagePanel? _panel;     // Panel displaying damage information

    // -------------------------------------------------------------------------
    // Harmony Patches
    // -------------------------------------------------------------------------

    /// <summary>
    ///     PATCH: NHealthBar.SetCreature
    ///
    ///     The game calls SetCreature when it assigns a creature (player or monster)
    ///     to a health bar node. We use this moment to create a reference to the player's health bar
    ///     because at this point the bar's creature reference is valid.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.SetCreature))]
    public static void AfterSetCreature(NHealthBar __instance)
    {
        if (!IsPlayerBar(__instance)) return;
        _playerHealthBar = __instance;
        
        TryInitializePanel();

        if (_panel == null) return;
        RefreshPanel();
        RepositionPanel();
    }
    
    /// <summary>
    ///     PATCH: NCreature.UpdateBounds
    ///
    ///     The game calls UpdateBounds whenever the hitbox of a creature is updated.
    ///     We use this call to create and/or reposition the information panel next to this hitbox.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateBounds), typeof(Node))]
    public static void AfterCreatureUpdate(NCreature __instance)
    {
        if (!__instance.Entity.IsPlayer) return;

        var player = GetLocalPlayer();
        if (player == null || __instance.Entity.Player != player) return;

        _playerCreatureNode = __instance;
        TryInitializePanel();
        
        if (_panel == null) return;
        RefreshPanel();
        RepositionPanel();
    }

    /// <summary>
    ///     PATCH: CombatStateTracker.NotifyCombatStateChanged
    ///
    ///     The game calls this whenever the state of combat has changed.
    ///     Everytime it does so, we refresh the label.
    ///
    ///     Note: This is very overkill but takes into account every time
    ///     the amount of incoming damage can change - he says with a great
    ///     deal of hope.
    ///
    ///     TODO:
    ///     - Can refresh count be minimized more?
    /// </summary>
    /// <param name="caller"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatStateTracker), nameof(CombatStateTracker.NotifyCombatStateChanged))]
    public static void AfterCombatStateChanged(string caller)
    {
        // Only refresh on player turn
        if (CombatManager.Instance.IsEnemyTurnStarted) return;
        
        RefreshPanel();
    }
    
    /// <summary>
    ///     PATCH: NCreature._ExitTree
    ///
    ///     Fires when the player's creature node is removed from the scene tree
    ///     (e.g. between combats). We use this to explicitly clear all references
    ///     so that the next combat starts from a clean slate.
    ///     
    ///     The panel is a child of this creature, so it will be freed automatically
    ///     along with it — we just need to null out our reference so
    ///     CreatePanelIfNotExists knows to make a fresh one.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NCreature), nameof(NCreature._ExitTree))]
    public static void AfterCreatureExitTree(NCreature __instance)
    {
        if (_playerCreatureNode != __instance) return;
    
        _playerCreatureNode = null;
        _playerHealthBar = null;
        _panel = null; // Panel is a child of this creature; it's already being freed
    }

    // -------------------------------------------------------------------------
    // Panel Lifecycle Helpers
    // -------------------------------------------------------------------------
    
    /// <summary>
    ///     Creates and positions the panel, but only once both the creature node
    ///     and health bar references are valid. Safe to call from either patch —
    ///     whichever fires second will complete initialization.
    /// </summary>
    private static void TryInitializePanel()
    {
        if (_playerCreatureNode == null || !GodotObject.IsInstanceValid(_playerCreatureNode)) return;
        if (_playerHealthBar == null || !GodotObject.IsInstanceValid(_playerHealthBar)) return;

        CreatePanelIfNotExists();
    }
    
    /// <summary>
    ///     Creates the info panel object for the first time.
    ///     Safe to call multiple times.
    /// </summary>
    private static void CreatePanelIfNotExists()
    {
        // If panel already set, do nothing
        if (_panel != null) return;
        
        // Create the new panel
        _panel = new IncomingDamagePanel();
        
        if (_playerCreatureNode != null && GodotObject.IsInstanceValid(_playerCreatureNode))
        {
            _playerCreatureNode.AddChild(_panel.Root);
        }
    }

    /// <summary>
    ///     Positions the panel just left of the player's hitbox.
    /// </summary>
    private static void RepositionPanel()
    {
        if (_panel == null) return;
        if (_playerCreatureNode == null || !GodotObject.IsInstanceValid(_playerCreatureNode)) return;
        
        _panel.Reposition(_playerCreatureNode);
    }

    /// <summary>
    ///     Updates the panel based on the current combat state.
    /// </summary>
    private static void RefreshPanel()
    {
        if (_panel == null) return;
        if (_playerHealthBar == null || !GodotObject.IsInstanceValid(_playerHealthBar) || 
            !_playerHealthBar.Visible) return;

        var creature = _playerHealthBar._creature;
        var player = GetLocalPlayer();

        if (player == null)
        {
            _panel.SetVisible(false);
            return;
        }
        
        var damageInfo = CalculateIncomingDamage(creature, player);

        if (damageInfo == null)
        {
            _panel.SetVisible(false);
            return;
        }
        
        _panel.Update((IncomingDamageInfo)damageInfo);
        _panel.SetVisible(true);
    }

    // -------------------------------------------------------------------------
    // Damage Calculation
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Calculates data about incoming damage and returns it in the form of
    ///     an IncomingDamageInfo struct.
    ///     Returns null if it encounters an invalid state.
    ///
    ///     TODO:
    ///     - Introduce effects like buffer & intangible into the calculation
    ///     - Account for relics? (Player cannot take more than so much damage in a turn, damage reduced)
    ///     - Account for self inflicting damage cards
    ///
    ///     Future idea:
    ///     - Possibly introduce multiple calculations into the HUD:
    ///         - Raw
    ///         - Post-block
    ///         - Remaining block
    ///         - Source breakdown (this could be a lot -- consider minimizing the refresh count before this)
    /// </summary>
    private static IncomingDamageInfo? CalculateIncomingDamage(Creature creature, Player player)
    {
        if (creature.CombatState == null) return null;
        
        var raw = 0;

        // Calculates total incoming damage from all enemies
        foreach (var hittableEnemy in creature.CombatState.HittableEnemies)
        {
            if (hittableEnemy.Monster == null) continue;
            foreach (var intent in hittableEnemy.Monster.NextMove.Intents)
            {
                // Filter to only attack-type intents.
                // IntentType.DeathBlow is a special one-hit-kill intent that
                // still deals damage and should be counted.
                if (intent.IntentType is IntentType.Attack or IntentType.DeathBlow)
                {
                    raw += ((AttackIntent)intent).GetTotalDamage([creature], hittableEnemy);
                }
            }
        }

        var block = player.Creature._block;
        
        var total = Math.Max(0, raw - block);
        var blocked = Math.Min(block, raw);
        var blockRemaining = Math.Max(0, block - raw);
        
        return new IncomingDamageInfo { Raw = raw, Unblocked = total, Blocked = blocked, RemainingBlock = blockRemaining };
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------
    
    private static Player? GetLocalPlayer() =>
        LocalContext.GetMe(RunManager.Instance.State);

    /// <summary>
    ///     Returns true if this health bar belongs to the local player.
    ///     This check appears in every patch, so we centralize it here.
    /// </summary>
    private static bool IsPlayerBar(NHealthBar bar)
    {
        var player = GetLocalPlayer();
        return player != null && bar._creature.Player == player;
    }
}