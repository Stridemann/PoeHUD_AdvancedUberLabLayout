using System.Collections.Generic;
using System.Windows.Forms;
using PoeHUD.Hud.Settings;
using PoeHUD.Plugins;

public class Settings : SettingsBase
{
    public Settings()
    {
        Enable = true;
        LabType = new ListNode();
        LabType.Value = "normal";
        AutoLabDetection = false;

        var screenBottomRight = BasePlugin.API.GameController.Window.GetWindowRectangle().BottomRight;
        X = new RangeNode<float>(500, 0, screenBottomRight.X);
        Y = new RangeNode<float>(0, 0, screenBottomRight.Y);

        Transparency = new RangeNode<float>(255, 0, 255);
        Size = new RangeNode<float>(100, 10, 300);

        ToggleDraw = new HotkeyNode(Keys.Delete);
        Reload = new HotkeyNode(Keys.Insert);
    }

    public int CurrentImageDateDay = -1;

    [Menu("Lab Type")]
    public ListNode LabType { get; set; }

    [Menu("Auto Lab Detection")]
    public ToggleNode AutoLabDetection { get; set; }

    [Menu("Position X")]
    public RangeNode<float> X { get; set; }

    [Menu("Position Y")]
    public RangeNode<float> Y { get; set; }

    [Menu("Opacity")]
    public RangeNode<float> Transparency { get; set; }

    [Menu("Size")]
    public RangeNode<float> Size { get; set; }

    [Menu("Toggle Display")]
    public HotkeyNode ToggleDraw { get; set; }

    [Menu("Force Reload")]
    public HotkeyNode Reload { get; set; }

    public bool ShowImage = true;
}