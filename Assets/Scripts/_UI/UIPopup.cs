// 引入Unity引擎和UI命名空间
using UnityEngine;
using UnityEngine.UI;

// UIPopup类
public class UIPopup : MonoBehaviour
{
    // 静态单例
    public static UIPopup singleton;
    // 弹窗面板
    public GameObject panel;
    // 弹窗文本组件
    public Text messageText;

    // 构造函数
    public UIPopup()
    {
        // 只分配一次静态单例（用于在使用区域/切换场景时与DontDestroyOnLoad一起使用）
        if (singleton == null) singleton = this;
    }

    // 显示弹窗方法
    public void Show(string message)
    {
        // 如果弹窗面板已显示，则附加错误信息，否则设置错误信息。最后显示弹窗面板。
        if (panel.activeSelf) messageText.text += ";\n" + message;
        else messageText.text = message;
        panel.SetActive(true);
    }
}