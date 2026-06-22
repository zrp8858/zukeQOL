using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace zukeQOL.zukeQOLCode;

/// <summary>
///     Adds a label to the right of the player's HP bar showing the total
///     incoming attack damage from all enemies this turn.
/// </summary>
[HarmonyPatch(typeof(NHealthBar))]
public static class IncomingDamageDisplay
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    /// <summary>
    ///     A reference to the player's health bar node.
    ///     We store this so we can find our label again when monsters die.
    ///     We wrap the check with GodotObject.IsInstanceValid() to make sure
    ///     the node hasn't been freed from the scene tree.
    /// </summary>
    private static NHealthBar? _playerHealthBar;
    
    private static IncomingDamagePanel? _panel;

    // -------------------------------------------------------------------------
    // Harmony Patches
    // -------------------------------------------------------------------------

    /// <summary>
    ///     PATCH: NHealthBar.SetCreature
    ///
    ///     The game calls SetCreature when it assigns a creature (player or monster)
    ///     to a health bar node. We use this moment to create our label for the
    ///     first time, because at this point the bar's creature reference is valid.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.SetCreature))]
    public static void AfterSetCreature(NHealthBar __instance)
    {
        if (!IsPlayerBar(__instance)) return;
        _playerHealthBar = __instance;
        CreatePanelIfNotExists(__instance);
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
    ///     - Minimize refresh count
    /// </summary>
    /// <param name="caller"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatStateTracker), nameof(CombatStateTracker.NotifyCombatStateChanged))]
    public static void AfterCombatStateChanged(string caller)
    {
        if (_playerHealthBar != null && GodotObject.IsInstanceValid(_playerHealthBar))
        {
            RefreshLabel(_playerHealthBar);
        }
    }
    
    /// <summary>
    ///     PATCH: NHealthBar.SetHpBarContainerSizeWithOffsets
    ///
    ///     The game calls this when the HP bar container changes size (e.g. when the
    ///     bar transitions between states). We need to reposition our label so it
    ///     stays anchored to the right edge of the bar.
    ///
    ///     Note: The `size` parameter here is automatically matched by Harmony
    ///     to the original method's parameter of the same type and name.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NHealthBar), "SetHpBarContainerSizeWithOffsets")]
    public static void AfterResize(NHealthBar __instance, Vector2 size)
    {
        if (!IsPlayerBar(__instance)) return;
        RepositionLabel(__instance);
    }

    // -------------------------------------------------------------------------
    // Label Lifecycle Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Creates our Label node and attaches it to the scene tree, but only
    ///     if we haven't done so already. Safe to call multiple times.
    /// </summary>
    private static void CreatePanelIfNotExists(NHealthBar bar)
    {
        if (_panel != null) return;
        _panel = new IncomingDamagePanel();

        var container = bar.HpBarContainer;
        var parent = container.GetParent() as Control ?? bar;
        parent.AddChild(_panel.Root);
    }

    /// <summary>
    ///     Positions the label to sit just to the right of the HP bar container.
    /// </summary>
    private static void RepositionLabel(NHealthBar bar)
    {
        if (_panel == null) return;

        _panel.Reposition(bar);
    }

    /// <summary>
    ///     Updates the label's text and visibility based on the current combat state.
    /// </summary>
    private static void RefreshLabel(NHealthBar bar)
    {
        if (_panel == null || !bar.Visible) return;

        // During the enemy's turn the damage is already happening — no need
        // to show a forecast. Hide the label.
        if (CombatManager.Instance.IsEnemyTurnStarted)
        {
            _panel.SetVisible(false);
            return;
        }

        // Sanity check: make sure we have a valid creature in a combat state.
        var creature = bar._creature;
        if (creature?.Player == null || creature.CombatState == null)
        {
            _panel.SetVisible(false);
            return;
        }

        var damageInfo = CalculateIncomingDamage(creature);
        
        _panel.Update(damageInfo);
        _panel.SetVisible(true);
    }

    // -------------------------------------------------------------------------
    // Damage Calculation
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Sums all incoming attack damage for this turn from hittable enemies.
    ///
    ///     Key concepts:
    ///     - HittableEnemies: enemies that can currently be targeted (excludes
    ///       untargetable/stealth enemies).
    ///     - NextMove.Intents: a monster can have multiple intents in one turn
    ///       (e.g. attack + buff). We only care about attack-type intents.
    ///     - GetTotalDamage: calculates the *actual* damage after modifiers like
    ///       Vulnerable, Weak, or the player's current armor/block. This is more
    ///       useful than the raw base value.
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
    private static IncomingDamageInfo CalculateIncomingDamage(Creature creature)
    {
        if (creature.CombatState == null) return new IncomingDamageInfo();

        var player = LocalContext.GetMe(RunManager.Instance.State);
        int raw = 0, blocked = 0, blockRemaining = 0, total = 0;

        // Calculates total incoming damage from all enemies
        foreach (var hittableEnemy in creature.CombatState.HittableEnemies)
        {
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

        if (player == null) return new IncomingDamageInfo();
        var block = player.Creature._block;
        
        total = Math.Max(0, raw - block);
        blocked = Math.Min(block, raw);
        blockRemaining = Math.Max(0, block - raw);
        
        return new IncomingDamageInfo { Raw = raw, Unblocked = total, Blocked = blocked, RemainingBlock = blockRemaining };
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Returns true if this health bar belongs to the local player.
    ///     This check appears in every patch, so we centralize it here.
    /// </summary>
    private static bool IsPlayerBar(NHealthBar bar)
    {
        var player = LocalContext.GetMe(RunManager.Instance.State);
        return player != null && bar._creature?.Player == player;
    }
}