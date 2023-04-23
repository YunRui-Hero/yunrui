// 基于 uMMORPG GridChecker 的极快的空间哈希兴趣管理
// => 在初始测试中快了 30 倍
// => 可扩展性更高
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class SpatialHashingInterestManagement : InterestManagement
    {
        [Tooltip("The maximum range that objects will be visible at.")]
        public int visRange = 30;

        // 如果我们看到 8 个邻居，则 1 个条目是 visRange/3
        public int resolution => visRange / 3;

        [Tooltip("Rebuild all every 'rebuildInterval' seconds.")]
        public float rebuildInterval = 1;
        double lastRebuildTime;

        public enum CheckMethod
        {
            XZ_FOR_3D,// 3D 游戏使用 XZ
            XY_FOR_2D// 2D 游戏使用 XY
        }
        [Tooltip("Spatial Hashing supports 3D (XZ) and 2D (XY) games.")]
        public CheckMethod checkMethod = CheckMethod.XZ_FOR_3D;

        // 调试
        public bool showSlider;

        // 网格
        Grid2D<NetworkConnection> grid = new Grid2D<NetworkConnection>();

        // 将 3D 世界位置投影到网格位置
        Vector2Int ProjectToGrid(Vector3 position) =>
            checkMethod == CheckMethod.XZ_FOR_3D
            ? Vector2Int.RoundToInt(new Vector2(position.x, position.z) / resolution)
            : Vector2Int.RoundToInt(new Vector2(position.x, position.y) / resolution);

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            // 计算投影位置
            Vector2Int projected = ProjectToGrid(identity.transform.position);
            Vector2Int observerProjected = ProjectToGrid(newObserver.identity.transform.position);

            // 距离必须在 8 个邻居中最大的一个，即
            //   直接邻居的距离为 1
            //   对角邻居的距离为 1.41 (= sqrt(2))
            // => 使用 sqrMagnitude 和 '2' 避免计算。相同的结果。
            return (projected - observerProjected).sqrMagnitude <= 2;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers, bool initialize)
        {
            // 将所有人添加到 9 个邻居格子中
            // -> 直接将观察者传递给 GetWithNeighbours，避免分配和昂贵的 .UnionWith 计算。
            Vector2Int current = ProjectToGrid(identity.transform.position);
            grid.GetWithNeighbours(current, newObservers);
        }

        //更新每个人在网格中的位置
        //（内部，以便我们可以根据测试进行更新）
        internal void Update()
        {
            // 仅在服务器上
            if (!NetworkServer.active) return;

            // 重要提示：每个更新都要刷新网格！
            // => 新创建的实体通过 OnCheckObservers 获得观察者分配。这可能随时发生，我们不希望它们被广播给旧的（已移动或销毁）连接。
            // => 玩家一直在移动。我们希望它们始终处于正确的网格位置。
            // => 注意，实际的“重建所有”并不需要一直发生。
            // 注意：也考虑每个“间隔”只刷新一次网格。但现在不需要。稳定性和正确性很重要。
            // 在更新每个人的位置之前清除旧的网格结果。
            // (这样我们就自动摆脱了已销毁的连接)
            //
            // 注意：内部保留分配的 HashSets。
            //       每帧清除和填充都可以工作，没有分配。
            grid.ClearNonAlloc();

            // 将每个连接放入其主玩家的位置的网格中
            // 注意：玩家在他周围的半径内看到。宠物周围不会看到。
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                // 身份验证并加入了一个拥有玩家的世界？
                if (connection.isAuthenticated && connection.identity != null)
                {
                    // 计算当前网格位置
                    Vector2Int position = ProjectToGrid(connection.identity.transform.position);

                    // 放入网格中
                    grid.Add(position, connection);
                }
            }

            // 每个“间隔”重建所有已生成实体的观察者
            // 这将调用 OnRebuildObservers，然后返回每个实体在 grid[position] 处的观察者。
            if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
            {
                RebuildAll();
                lastRebuildTime = NetworkTime.localTime;
            }
        }

        // 来自 DotsNet 的滑动条。在基准演示中玩耍很好。
        void OnGUI()
        {
            if (!showSlider) return;

            // 只在服务器运行时显示。不在客户端等上显示。
            if (!NetworkServer.active) return;

            int height = 30;
            int width = 250;
            GUILayout.BeginArea(new Rect(Screen.width / 2 - width / 2, Screen.height - height, width, height));
            GUILayout.BeginHorizontal("Box");
            GUILayout.Label("Radius:");
            visRange = Mathf.RoundToInt(GUILayout.HorizontalSlider(visRange, 0, 200, GUILayout.Width(150)));
            GUILayout.Label(visRange.ToString());
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
