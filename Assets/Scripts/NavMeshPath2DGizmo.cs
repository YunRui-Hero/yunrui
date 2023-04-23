// �ڳ�������Gizmo��ʽ���ƴ����˵�·����
using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent2D))]
public class NavMeshPath2DGizmo : MonoBehaviour
{
    void OnDrawGizmos()
    {
        // ֻ����Ϸ����ʱ���ƣ�����NavMeshAgent2Dû��3D����
        if (Application.isPlaying)
        {
            // ���ܻ��������Ϊ���¼��ؽű���ʱ���������
            NavMeshAgent2D agent = GetComponent<NavMeshAgent2D>();
            // ��ȡ·��
            NavMeshPath2D path = agent.path;
            // ��ɫȡ����״̬
            Color color = Color.white;
            switch (path.status)
            {
                case NavMeshPathStatus.PathComplete: color = Color.white; break;
                case NavMeshPathStatus.PathInvalid: color = Color.red; break;
                case NavMeshPathStatus.PathPartial: color = Color.yellow; break;
            }
            // ����·��
            for (int i = 1; i < path.corners.Length; ++i)
                Debug.DrawLine(path.corners[i - 1], path.corners[i], color);
            // �����ٶ�
            Debug.DrawLine(transform.position, transform.position + (Vector3)agent.velocity, Color.blue, 0, false);
        }
    }
}