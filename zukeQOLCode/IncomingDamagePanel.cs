using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace zukeQOL.zukeQOLCode;

public class IncomingDamagePanel
{
    // -------------------------------------------------------------------------
    // Colors
    // -------------------------------------------------------------------------

    private static readonly Color BgColor         = new(0.05f, 0.04f, 0.09f, 0.93f);
    private static readonly Color BorderColor     = new(0.66f, 0.52f, 0.10f, 1.00f); // STS gold
    private static readonly Color TextMuted       = new(0.48f, 0.44f, 0.56f, 1.00f);
    private static readonly Color SeparatorColor  = new(0.23f, 0.19f, 0.31f, 0.80f);
    private static readonly Color ColorRaw        = new(0.80f, 0.79f, 0.85f, 1.00f); // near-white
    private static readonly Color ColorBlocked    = new(0.29f, 0.66f, 0.91f, 1.00f); // ice blue
    private static readonly Color ColorUnblocked  = new(0.91f, 0.26f, 0.16f, 1.00f); // danger red
    private static readonly Color ColorRemaining  = new(0.26f, 0.79f, 0.34f, 1.00f); // safe green
    private static readonly Color FlashColor      = new(1.00f, 1.00f, 1.00f, 1.00f);

    private const float FlashFadeSeconds = 0.4f;

    // -------------------------------------------------------------------------
    // Nodes
    // -------------------------------------------------------------------------

    private readonly PanelContainer _panel;
    private readonly Label          _rawValue;
    private readonly Label          _blockedValue;
    private readonly Label          _unblockedValue;
    private readonly Label          _remainingValue;

    // -------------------------------------------------------------------------
    // Animation state
    // -------------------------------------------------------------------------

    private int _prevRaw       = -1;
    private int _prevBlocked   = -1;
    private int _prevUnblocked = -1;
    private int _prevRemaining = -1;

    public Control Root => _panel;

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    public IncomingDamagePanel()
    {
        _panel = new PanelContainer();
        ApplyPanelStyle();

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top",    10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.AddThemeConstantOverride("margin_left",   14);
        margin.AddThemeConstantOverride("margin_right",  14);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);

        // ---- Title ----
        var title = new Label { Text = "INCOMING" };
        title.AddThemeFontSizeOverride("font_size", 10);
        title.AddThemeColorOverride("font_color", TextMuted);
        vbox.AddChild(title);

        AddColorRect(vbox, BorderColor with { A = 0.40f }, height: 1); // gold rule

        // ---- Primary row: damage taken (largest, most important) ----
        _unblockedValue = AddRow(vbox, "TAKING", ColorUnblocked, valueSize: 22);

        AddColorRect(vbox, SeparatorColor, height: 1); // subdued divider

        // ---- Supporting rows ----
        _rawValue       = AddRow(vbox, "ATTACK",     ColorRaw,       valueSize: 15);
        _blockedValue   = AddRow(vbox, "BLOCKED",    ColorBlocked,   valueSize: 15);
        _remainingValue = AddRow(vbox, "BLOCK LEFT", ColorRemaining, valueSize: 15);

        margin.AddChild(vbox);
        _panel.AddChild(margin);
    }

    // -------------------------------------------------------------------------
    // Construction helpers
    // -------------------------------------------------------------------------

    private void ApplyPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor                 = BgColor,
            BorderColor             = BorderColor,
            BorderWidthTop          = 2,
            BorderWidthBottom       = 2,
            BorderWidthLeft         = 2,
            BorderWidthRight        = 2,
            CornerRadiusTopLeft     = 4,
            CornerRadiusTopRight    = 4,
            CornerRadiusBottomLeft  = 4,
            CornerRadiusBottomRight = 4,
        };
        _panel.AddThemeStyleboxOverride("panel", style);
    }

    /// <summary>
    ///     Adds a horizontal row with a muted category label on the left and
    ///     a colored value label on the right. Returns the value label.
    /// </summary>
    private static Label AddRow(VBoxContainer parent, string categoryText, Color valueColor, int valueSize)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);

        var category = new Label { Text = categoryText };
        category.AddThemeFontSizeOverride("font_size", 11);
        category.AddThemeColorOverride("font_color", TextMuted);
        // Vertical-center the small label against the potentially taller value label
        category.VerticalAlignment = VerticalAlignment.Center;
        category.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        var value = new Label { Text = "—" };
        value.AddThemeFontSizeOverride("font_size", valueSize);
        value.AddThemeColorOverride("font_color", valueColor);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        value.HorizontalAlignment = HorizontalAlignment.Right;

        hbox.AddChild(category);
        hbox.AddChild(value);
        parent.AddChild(hbox);

        return value;
    }

    /// <summary>
    ///     Adds a thin horizontal color rule as a visual separator.
    /// </summary>
    private static void AddColorRect(VBoxContainer parent, Color color, int height)
    {
        var rect = new ColorRect
        {
            Color             = color,
            CustomMinimumSize = new Vector2(0, height),
        };
        parent.AddChild(rect);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public bool IsValid() => GodotObject.IsInstanceValid(_panel);

    public void SetVisible(bool visible) => _panel.Visible = visible;

    /// <summary>
    ///     Repositions the panel just left of the player's hitbox.
    /// </summary>
    public void Reposition(NCreature creatureNode)
    {
        var hitbox    = creatureNode.Hitbox;
        var panelSize = _panel.GetMinimumSize();

        _panel.Position = new Vector2(
            hitbox.Position.X - panelSize.X - 32f,
            hitbox.Position.Y + (hitbox.Size.Y - panelSize.Y) / 2f
        );
    }

    /// <summary>
    ///     Updates the displayed values, triggering a flash animation on any
    ///     value that has changed since the last update.
    /// </summary>
    public void Update(IncomingDamageInfo damage)
    {
        SetAndAnimate(_unblockedValue, damage.Unblocked,      ColorUnblocked, ref _prevUnblocked);
        SetAndAnimate(_rawValue,       damage.Raw,            ColorRaw,       ref _prevRaw);
        SetAndAnimate(_blockedValue,   damage.Blocked,        ColorBlocked,   ref _prevBlocked);
        SetAndAnimate(_remainingValue, damage.RemainingBlock, ColorRemaining, ref _prevRemaining);
    }

    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Updates a label's text and, if the value has changed, plays a
    ///     white flash that eases back to the label's assigned color.
    /// </summary>
    private static void SetAndAnimate(Label label, int newValue, Color baseColor, ref int stored)
    {
        var changed = newValue != stored;
        label.Text  = newValue.ToString();
        stored      = newValue;

        if (!changed) return;

        label.AddThemeColorOverride("font_color", FlashColor);

        var tween = label.CreateTween();
        tween.TweenProperty(label, "theme_override_colors/font_color", baseColor, FlashFadeSeconds)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);
    }
}