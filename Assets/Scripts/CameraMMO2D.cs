// Simple MMO camera that always follows the player.
using UnityEngine;

public class CameraMMO2D : MonoBehaviour
{
    [Header("Snap to Pixel Grid")]
    public PixelDensityCamera pixelDensity;// �����ܶ����
    public bool snapToGrid = true;// �Ƿ���뵽���ظ���

    [Header("Target Follow")]
    public Transform target;
    // �����Ŀ��
    // �����Ŀ��λ�õ�ƫ����������������ھ۽�Ŀ���ͷ��
    public Vector2 offset = Vector2.zero;

    // ƽ������ƶ�������ֵ
    [Header("Dampening")]
    public float damp = 5;

    void LateUpdate()
    {
        if (!target) return;

        // ���������Ŀ��λ��
        Vector2 goal = (Vector2)target.position + offset;

        // ��ֵ�������λ��
        Vector2 position = Vector2.Lerp(transform.position, goal, Time.deltaTime * damp);

        // �����Ҫ���뵽���ظ��ӣ���������ظ��Ӷ���
        // �������Ա�֤���ص��������룬�����ƶ�����ʱ���ֶ�����Ч��
        if (snapToGrid)
        {
            float gridSize = pixelDensity.pixelsToUnits * pixelDensity.zoom;
            position.x = Mathf.Round(position.x * gridSize) / gridSize;
            position.y = Mathf.Round(position.y * gridSize) / gridSize;
        }

        // ��2Dλ��ת��Ϊ3Dλ�ã�������Z��λ�ñ�����2Dƽ��ǰ��
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }
}
