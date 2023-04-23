// 人们应该能够轻松地看到和向开发者报告错误。
// Unity的开发者控制台仅在开发版本中起作用，仅显示错误。这个类提供了一个控制台，在所有版本中都起作用，并且在开发版本中也显示日志和警告。
// 注意：我们不包含堆栈跟踪，因为如果需要，它也可以从日志文件中获取。
// 注意：没有'隐藏'按钮，因为我们确实希望人们看到这些错误并将其报告给我们。
// 注意：在Debug/Development模式下构建后，可以显示正常的Debug.Log信息。
using UnityEngine;
using System.Collections.Generic;
// 定义一个名为Mirror的命名空间
namespace Mirror
{
    // 定义一个名为LogEntry的结构体
    struct LogEntry
    {
        // 定义message和type两个public字段
        public string message;
        public LogType type;
        // 定义一个带有message和type两个参数的构造函数
        public LogEntry(string message, LogType type)
        {
            this.message = message;
            this.type = type;
        }
    }
    // GUI控制台类，继承自MonoBehaviour类
    public class GUIConsole : MonoBehaviour
    {
        // 控制台高度
        public int height = 150;

        // 最多保留的日志数，超过后会删除旧的日志
        public int maxLogCount = 50;

        // 日志存储队列，以便轻松删除旧记录
        Queue<LogEntry> log = new Queue<LogEntry>();

        // 热键，用于在运行时显示/隐藏控制台，以便更轻松地调试
        // F12是一个不错的选择，其他游戏中几乎没有使用过
        public KeyCode hotKey = KeyCode.F12;

        // GUI相关变量
        bool visible;
        Vector2 scroll = Vector2.zero;

        void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        // OnLog 方法记录所有日志信息，即使是在发布版本中使用 Debug.Log 也会被记录
        // => 这使得很多事情更容易。例如，插件初始化记录日志等
        // => 拥有比没有更好
        void OnLog(string message, string stackTrace, LogType type)
        {
            // 是否为重要信息？
            bool isImportant = type == LogType.Error || type == LogType.Exception;

            // 仅在重要信息时使用堆栈跟踪
            // （否则用户将不得不查找和搜索日志文件。
            // 在控制台中直接看到它要容易得多。）
            // => 如果堆栈跟踪可用（仅在调试版本中），则添加 \n
            if (isImportant && !string.IsNullOrWhiteSpace(stackTrace))
                message += "\n" + stackTrace;

            // 添加到队列
            log.Enqueue(new LogEntry(message, type));

            // 保持日志数量不超过 maxLogCount
            if (log.Count > maxLogCount)
                log.Dequeue();

            // 如果是重要信息，则显示控制台
            // （对于普通日志，让用户自己决定是否显示控制台）
            if (isImportant)
                visible = true;

            // 自动滚动
            scroll.y = float.MaxValue;
        }

        void Update()
        {
            // 按下热键时显示/隐藏控制台
            if (Input.GetKeyDown(hotKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            // 开始绘制滚动区域
            scroll = GUILayout.BeginScrollView(scroll, "Box", GUILayout.Width(Screen.width), GUILayout.Height(height));
            foreach (LogEntry entry in log)
            {
                // 根据日志类型设置颜色
                if (entry.type == LogType.Error || entry.type == LogType.Exception)
                    GUI.color = Color.red;
                else if (entry.type == LogType.Warning)
                    GUI.color = Color.yellow;
                // 绘制日志信息
                GUILayout.Label(entry.message);
                GUI.color = Color.white;
            }
            // 结束绘制滚动区域
            GUILayout.EndScrollView();
        }
    }
}
