// Simple MMO camera that always follows the player.
using UnityEngine;

public class CameraMMO2D : MonoBehaviour
{
    [Header("Snap to Pixel Grid")]
    public PixelDensityCamera pixelDensity;// 像素密度相机
    public bool snapToGrid = true;// 是否对齐到像素格子

    [Header("Target Follow")]
    public Transform target;
    // 跟随的目标
    // 相对于目标位置的偏移量，例如可以用于聚焦目标的头部
    public Vector2 offset = Vector2.zero;

    // 平滑相机移动的阻尼值
    [Header("Dampening")]
    public float damp = 5;

    void LateUpdate()
    {
        if (!target) return;

        // 计算相机的目标位置
        Vector2 goal = (Vector2)target.position + offset;

        // 插值计算相机位置
        Vector2 position = Vector2.Lerp(transform.position, goal, Time.deltaTime * damp);

        // 如果需要对齐到像素格子，则进行像素格子对齐
        // 这样可以保证像素的完美对齐，避免移动物体时出现抖动等效果
        if (snapToGrid)
        {
            float gridSize = pixelDensity.pixelsToUnits * pixelDensity.zoom;
            position.x = Mathf.Round(position.x * gridSize) / gridSize;
            position.y = Mathf.Round(position.y * gridSize) / gridSize;
        }

        // 将2D位置转换为3D位置，但保持Z轴位置保持在2D平面前方
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }
}
