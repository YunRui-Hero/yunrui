// ��Unity��ʵ������������Ⱦ
// �ο���Դ��Unity 2014 2D���ʵ��
// ����noobtuts.com | vis2k�޸ģ�������������أ���
using UnityEngine;

[ExecuteInEditMode]
public class PixelDensityCamera: MonoBehaviour
{
    // ���о���ʹ�õ�����ֵ
    public float pixelsToUnits = 16;

    // ��������
    public int zoom = 1;

    void Update()
    {
        // �������������С
        GetComponent<Camera>().orthographicSize = Screen.height / pixelsToUnits / zoom / 2;
    }
}