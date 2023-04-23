// 在Unity中实现像素完美渲染
// 参考来源：Unity 2014 2D最佳实践
// （由noobtuts.com | vis2k修改（添加了缩放因素））
using UnityEngine;

[ExecuteInEditMode]
public class PixelDensityCamera: MonoBehaviour
{
    // 所有精灵使用的像素值
    public float pixelsToUnits = 16;

    // 缩放因子
    public int zoom = 1;

    void Update()
    {
        // 设置相机正交大小
        GetComponent<Camera>().orthographicSize = Screen.height / pixelsToUnits / zoom / 2;
    }
}