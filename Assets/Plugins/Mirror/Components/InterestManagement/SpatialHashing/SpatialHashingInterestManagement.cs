// ���� uMMORPG GridChecker �ļ���Ŀռ��ϣ��Ȥ����
// => �ڳ�ʼ�����п��� 30 ��
// => ����չ�Ը���
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class SpatialHashingInterestManagement : InterestManagement
    {
        [Tooltip("The maximum range that objects will be visible at.")]
        public int visRange = 30;

        // ������ǿ��� 8 ���ھӣ��� 1 ����Ŀ�� visRange/3
        public int resolution => visRange / 3;

        [Tooltip("Rebuild all every 'rebuildInterval' seconds.")]
        public float rebuildInterval = 1;
        double lastRebuildTime;

        public enum CheckMethod
        {
            XZ_FOR_3D,// 3D ��Ϸʹ�� XZ
            XY_FOR_2D// 2D ��Ϸʹ�� XY
        }
        [Tooltip("Spatial Hashing supports 3D (XZ) and 2D (XY) games.")]
        public CheckMethod checkMethod = CheckMethod.XZ_FOR_3D;

        // ����
        public bool showSlider;

        // ����
        Grid2D<NetworkConnection> grid = new Grid2D<NetworkConnection>();

        // �� 3D ����λ��ͶӰ������λ��
        Vector2Int ProjectToGrid(Vector3 position) =>
            checkMethod == CheckMethod.XZ_FOR_3D
            ? Vector2Int.RoundToInt(new Vector2(position.x, position.z) / resolution)
            : Vector2Int.RoundToInt(new Vector2(position.x, position.y) / resolution);

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            // ����ͶӰλ��
            Vector2Int projected = ProjectToGrid(identity.transform.position);
            Vector2Int observerProjected = ProjectToGrid(newObserver.identity.transform.position);

            // ��������� 8 ���ھ�������һ������
            //   ֱ���ھӵľ���Ϊ 1
            //   �Խ��ھӵľ���Ϊ 1.41 (= sqrt(2))
            // => ʹ�� sqrMagnitude �� '2' ������㡣��ͬ�Ľ����
            return (projected - observerProjected).sqrMagnitude <= 2;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers, bool initialize)
        {
            // ����������ӵ� 9 ���ھӸ�����
            // -> ֱ�ӽ��۲��ߴ��ݸ� GetWithNeighbours���������Ͱ���� .UnionWith ���㡣
            Vector2Int current = ProjectToGrid(identity.transform.position);
            grid.GetWithNeighbours(current, newObservers);
        }

        //����ÿ�����������е�λ��
        //���ڲ����Ա����ǿ��Ը��ݲ��Խ��и��£�
        internal void Update()
        {
            // ���ڷ�������
            if (!NetworkServer.active) return;

            // ��Ҫ��ʾ��ÿ�����¶�Ҫˢ������
            // => �´�����ʵ��ͨ�� OnCheckObservers ��ù۲��߷��䡣�������ʱ���������ǲ�ϣ�����Ǳ��㲥���ɵģ����ƶ������٣����ӡ�
            // => ���һֱ���ƶ�������ϣ������ʼ�մ�����ȷ������λ�á�
            // => ע�⣬ʵ�ʵġ��ؽ����С�������Ҫһֱ������
            // ע�⣺Ҳ����ÿ���������ֻˢ��һ�����񡣵����ڲ���Ҫ���ȶ��Ժ���ȷ�Ժ���Ҫ��
            // �ڸ���ÿ���˵�λ��֮ǰ����ɵ���������
            // (�������Ǿ��Զ������������ٵ�����)
            //
            // ע�⣺�ڲ���������� HashSets��
            //       ÿ֡�������䶼���Թ�����û�з��䡣
            grid.ClearNonAlloc();

            // ��ÿ�����ӷ���������ҵ�λ�õ�������
            // ע�⣺���������Χ�İ뾶�ڿ�����������Χ���ῴ����
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                // �����֤��������һ��ӵ����ҵ����磿
                if (connection.isAuthenticated && connection.identity != null)
                {
                    // ���㵱ǰ����λ��
                    Vector2Int position = ProjectToGrid(connection.identity.transform.position);

                    // ����������
                    grid.Add(position, connection);
                }
            }

            // ÿ����������ؽ�����������ʵ��Ĺ۲���
            // �⽫���� OnRebuildObservers��Ȼ�󷵻�ÿ��ʵ���� grid[position] ���Ĺ۲��ߡ�
            if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
            {
                RebuildAll();
                lastRebuildTime = NetworkTime.localTime;
            }
        }

        // ���� DotsNet �Ļ��������ڻ�׼��ʾ����ˣ�ܺá�
        void OnGUI()
        {
            if (!showSlider) return;

            // ֻ�ڷ���������ʱ��ʾ�����ڿͻ��˵�����ʾ��
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
