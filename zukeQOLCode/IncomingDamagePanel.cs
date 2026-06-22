using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace zukeQOL.zukeQOLCode;

public class IncomingDamagePanel
{
    private readonly PanelContainer _panel;

    private readonly Label _raw;
    private readonly Label _blocked;
    private readonly Label _unblocked;
    private readonly Label _remaining;

    public Control Root => _panel;

    public IncomingDamagePanel()
    {
        _panel = new PanelContainer();

        var margin = new MarginContainer();
        var vbox = new VBoxContainer();

        _raw = new Label();
        _unblocked = new Label();
        _blocked = new Label();
        _remaining = new Label();

        vbox.AddChild(_raw);
        vbox.AddChild(_unblocked);
        vbox.AddChild(_blocked);
        vbox.AddChild(_remaining);

        margin.AddChild(vbox);
        _panel.AddChild(margin);
    }

    public void SetVisible(bool visible)
    {
        _panel.Visible = visible;
    }
    
    public void Reposition(NCreature creatureNode)
    {
        var hitbox = creatureNode.Hitbox;
        
        var panelSize = _panel.GetMinimumSize();

        _panel.Position = new Vector2(
            hitbox.Position.X - panelSize.X - 32f,
            hitbox.Position.Y + (hitbox.Size.Y - panelSize.Y) / 2f
        );
    }

    public void Update(IncomingDamageInfo damage)
    {
        _raw.Text       = $"Raw:         {damage.Raw}";
        _unblocked.Text  = $"Unblocked:    {damage.Unblocked}";
        _blocked.Text   = $"Blocked:     {damage.Blocked}";
        _remaining.Text = $"Remaining Block:  {damage.RemainingBlock}";
    }
}