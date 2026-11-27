namespace Rolan.Core;

// 消耗标记只能由一次新的按下事件重置，拖放返回或界面重建都不能重新启用它。
public sealed class ItemDragGesture
{
    private string pressedItemId = "";
    public bool Consumed { get; private set; }
    public void Arm(string itemId)
    {
        pressedItemId = itemId;
        Consumed = false;
    }
    public bool TryBegin(string itemId, bool eventPressed, bool physicallyPressed)
    {
        if (!physicallyPressed)
        {
            pressedItemId = "";
            return false;
        }
        if (Consumed || !eventPressed || pressedItemId.Length == 0 || itemId != pressedItemId)
        {
            return false;
        }
        Consumed = true;
        return true;
    }
}
