// 这个脚本可以添加到可移动窗口上，以使窗口只能在屏幕范围内移动。
using UnityEngine;

public class UIKeepInScreen : MonoBehaviour
{
    void Update()
    {
        // 获取当前矩形
        Rect rect = GetComponent<RectTransform>().rect;

        // 转换到世界坐标系
        Vector2 minworld = transform.TransformPoint(rect.min);
        Vector2 maxworld = transform.TransformPoint(rect.max);
        Vector2 sizeworld = maxworld - minworld;

        // 保持最小位置在屏幕范围内 - 尺寸
        maxworld = new Vector2(Screen.width, Screen.height) - sizeworld;

        // 保持位置在 (0,0) 和 maxworld 之间
        float x = Mathf.Clamp(minworld.x, 0, maxworld.x);
        float y = Mathf.Clamp(minworld.y, 0, maxworld.y);

        // 将新的位置设置为 xy（=本地坐标）+ 偏移量（=世界坐标）
        Vector2 offset = (Vector2)transform.position - minworld;
        transform.position = new Vector2(x, y) + offset;
    }
}