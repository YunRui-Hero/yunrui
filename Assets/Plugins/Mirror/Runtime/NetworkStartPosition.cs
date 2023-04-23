using UnityEngine;

namespace Mirror
{
    // <summary>������ɵ���ʼλ�ã��Զ���NetworkManager��ע���Լ���</summary>
    [DisallowMultipleComponent]// ������˵��������������ѡ��
    [AddComponentMenu("Network/NetworkStartPosition")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-start-position")]// ���������ĵ�������
    public class NetworkStartPosition : MonoBehaviour
    {
        public void Awake()
        {
            NetworkManager.RegisterStartPosition(transform);// ��NetworkManager��ע���Լ���Transform
        }

        public void OnDestroy()
        {
            NetworkManager.UnRegisterStartPosition(transform);// ��NetworkManager��ȡ��ע���Լ���Transform
        }
    }
}
