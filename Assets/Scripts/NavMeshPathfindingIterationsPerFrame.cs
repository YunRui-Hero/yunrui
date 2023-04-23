// Unity每帧只计算'n'次导航网格寻路迭代。默认值100适用于小型项目，但具有大量代理的MMO将需要更多的迭代次数才能避免移动延迟。

// 现在我们只是在Awake中增加该数字一次。

// 在未来，我们将能够为每个代理设置迭代次数：
//   https://forum.unity3d.com/threads/pathfindingiterationsperframe-for-bigger-games-should-be-per-agent.482699/

// 注意：我们已经可以使用Update将迭代设置为players.count*multiplier，但这并不会好太多，因为一个玩家仍然可能会延迟所有其他玩家的路径计算。
// 引入Unity引擎和导航系统命名空间
using UnityEngine;
using UnityEngine.AI;
// 定义一个名为NavMeshPathfindingIterationsPerFrame的类，并继承自MonoBehaviour类
public class NavMeshPathfindingIterationsPerFrame : MonoBehaviour
{
    public int iterations = 100; // 默认值为100，即每帧路径计算次数为100次
    // Awake方法会在对象实例化时被调用
    void Awake()
    {
        // 打印一条消息，显示当前每帧路径计算次数，并将其设置为我们定义的iterations值
        // NavMesh.pathfindingIterationsPerFrame表示每帧路径计算的最大次数
        print("Setting NavMesh Pathfinding Iterations Per Frame from " + NavMesh.pathfindingIterationsPerFrame + " to " + iterations);
        NavMesh.pathfindingIterationsPerFrame = iterations;
    }
}
