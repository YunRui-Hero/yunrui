using UnityEngine;

namespace Mirror
{
    // <summary>玩家生成的起始位置，自动在NetworkManager中注册自己。</summary>
    [DisallowMultipleComponent]// 在组件菜单中添加这个组件的选项
    [AddComponentMenu("Network/NetworkStartPosition")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-start-position")]// 给出帮助文档的链接
    public class NetworkStartPosition : MonoBehaviour
    {
        public void Awake()
        {
            NetworkManager.RegisterStartPosition(transform);// 在NetworkManager中注册自己的Transform
        }

        public void OnDestroy()
        {
            NetworkManager.UnRegisterStartPosition(transform);// 在NetworkManager中取消注册自己的Transform
        }
    }
}
