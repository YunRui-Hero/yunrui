// 在场景中以Gizmo形式绘制代理人的路径。
using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent2D))]
public class NavMeshPath2DGizmo : MonoBehaviour
{
    void OnDrawGizmos()
    {
        // 只在游戏运行时绘制，否则NavMeshAgent2D没有3D代理。
        if (Application.isPlaying)
        {
            // 不能缓存代理，因为重新加载脚本有时会清除缓存
            NavMeshAgent2D agent = GetComponent<NavMeshAgent2D>();
            // 获取路径
            NavMeshPath2D path = agent.path;
            // 颜色取决于状态
            Color color = Color.white;
            switch (path.status)
            {
                case NavMeshPathStatus.PathComplete: color = Color.white; break;
                case NavMeshPathStatus.PathInvalid: color = Color.red; break;
                case NavMeshPathStatus.PathPartial: color = Color.yellow; break;
            }
            // 绘制路径
            for (int i = 1; i < path.corners.Length; ++i)
                Debug.DrawLine(path.corners[i - 1], path.corners[i], color);
            // 绘制速度
            Debug.DrawLine(transform.position, transform.position + (Vector3)agent.velocity, Color.blue, 0, false);
        }
    }
}