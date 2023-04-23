// 为UI面板添加窗口行为，使用户能够移动和关闭。
using UnityEngine;
using UnityEngine.EventSystems;

public enum CloseOption // 关闭选项
{
    DoNothing,// 什么也不做
    DeactivateWindow,// 关闭窗口
    DestroyWindow// 销毁窗口
}
// 继承自IBeginDragHandler、IDragHandler、IEndDragHandler接口
public class UIWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // 关闭选项，默认为关闭窗口
    public CloseOption onClose = CloseOption.DeactivateWindow;

    // 缓存窗口对象
    Transform window;

    // 在Awake()函数中
    void Awake()
    {
        // 缓存父窗口
        window = transform.parent;
    }

    // 处理拖拽事件
    public void HandleDrag(PointerEventData d)
    {
        // 如果父窗口需要知道拖拽事件，则发送消息
        window.SendMessage("OnWindowDrag", d, SendMessageOptions.DontRequireReceiver);

        // 移动父窗口
        window.Translate(d.delta);
    }

    // 当开始拖拽时调用
    public void OnBeginDrag(PointerEventData d)
    {
        // 处理拖拽事件
        HandleDrag(d);
    }

    // 拖拽过程中持续调用
    public void OnDrag(PointerEventData d)
    {
        // 处理拖拽事件
        HandleDrag(d);
    }

    // 当结束拖拽时调用
    public void OnEndDrag(PointerEventData d)
    {
        // 处理拖拽事件
        HandleDrag(d);
    }

    // OnClose是通过Inspector Callbacks被关闭按钮调用的函数
    public void OnClose()
    {
        // 发送消息，以防需要的情况下
        // 注意：为避免死锁，不要将其命名为与此函数相同的名称
        window.SendMessage("OnWindowClose", SendMessageOptions.DontRequireReceiver);

        // 隐藏窗口
        if (onClose == CloseOption.DeactivateWindow)
            window.gameObject.SetActive(false);

        // 如果需要，则销毁窗口
        if (onClose == CloseOption.DestroyWindow)
            Destroy(window.gameObject);
    }
}
