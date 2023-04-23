// 一个普通的传送门，可以将玩家从A点传送到B点。
using UnityEngine;

[RequireComponent(typeof(Collider2D))]// 确保此脚本附着的物体拥有Collider2D组件
public class Portal : MonoBehaviour
{
    public int requiredLevel = 1;// 需要的等级
    public Transform destination;// 目标传送点的Transform组件

    void OnPortal(Player player)// 当玩家进入传送门时执行的函数
    {
        if (destination != null)
            player.movement.Warp(destination.position);// 如果有目标传送点，则将玩家传送到目标位置
    }

    void OnTriggerEnter2D(Collider2D co)
    {
        // 碰撞器可能在玩家的骨骼结构中，需要查找其父对象。
        Player player = co.GetComponentInParent<Player>();
        if (player != null)
        {
            // 是否需要特定等级？
            if (player.level.current >= requiredLevel)
            {
                // 服务器？则进入传送门。
                if (player.isServer)
                    OnPortal(player);
            }
            else
            {
                // 客户端？则直接显示信息消息，无需通过TargetRpc从服务器发送到客户端。
                if (player.isClient)
                    player.chat.AddMsgInfo("Portal requires level " + requiredLevel);
            }
        }
    }
}
