// 注意：这个脚本必须放在一个始终处于激活状态的UI父级对象上，这样我们才能从其他代码中始终找到它。（GameObject.Find不能找到未激活的对象）
using System.Linq; // 引用System.Linq命名空间
using UnityEngine; // 引用UnityEngine命名空间
using UnityEngine.UI; // 引用UnityEngine.UI命名空间
using Mirror; // 引用Mirror命名空间

public partial class UILogin : MonoBehaviour
{
    public UIPopup uiPopup; // 引用UIPopup组件
    public NetworkManagerMMO manager; // 引用NetworkManagerMMO组件，singleton=null在Start/Awake中
    public NetworkAuthenticatorMMO auth; // 引用NetworkAuthenticatorMMO组件
    public GameObject panel; // 引用GameObject对象
    public Text statusText; // 引用Text组件
    public InputField accountInput; // 引用InputField组件
    public InputField passwordInput; // 引用InputField组件
    public Dropdown serverDropdown; // 引用Dropdown组件
    public Button loginButton; // 引用Button组件
    public Button registerButton; // 引用Button组件
    [TextArea(1, 30)] public string registerMessage = "First time? Just log in and we will\ncreate an account automatically."; // 注册信息
    public Button hostButton; // 引用Button组件
    public Button dedicatedButton; // 引用Button组件
    public Button cancelButton; // 引用Button组件
    public Button quitButton; // 引用Button组件

    void Start()
    {
        // 在加载时通过名称加载上一个服务器，以防有一天顺序发生改变。
        if (PlayerPrefs.HasKey("LastServer"))
        {
            string last = PlayerPrefs.GetString("LastServer", ""); // 获取上一个服务器的名称
            serverDropdown.value = manager.serverList.FindIndex(s => s.name == last);// 找到对应名称的服务器并设置为下拉列表的默认选项
        }
    }

    void OnDestroy()
    {
        // 在销毁时通过名称保存上一个服务器，以防有一天顺序发生改变
        PlayerPrefs.SetString("LastServer", serverDropdown.captionText.text);// 将下拉列表中当前选项的文本设置为上一个服务器的名称并保存
    }

    void Update()
    {
        // 只在离线状态下显示，并且在握手期间显示，因为我们不想在尝试登录并等待服务器响应时什么都不显示。
        if (manager.state == NetworkState.Offline || manager.state == NetworkState.Handshake)
        {
            panel.SetActive(true);// 激活UI界面

            // 显示状态信息
            if (NetworkClient.isConnecting)
                statusText.text = "Connecting...";
            else if (manager.state == NetworkState.Handshake)
                statusText.text = "Handshake...";
            else
                statusText.text = "";

            //按钮。只有在网络处于非活动状态时可交互（使用IsConnecting会有轻微延迟，可能允许多次点击）
            registerButton.interactable = !manager.isNetworkActive;// 注册按钮只有在网络处于非活动状态时可交互
            registerButton.onClick.SetListener(() => { uiPopup.Show(registerMessage); });// 点击注册按钮时显示注册信息
            loginButton.interactable = !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);// 登录按钮只有在网络处于非活动状态且用户名符合要求时可交互
            loginButton.onClick.SetListener(() => { manager.StartClient(); });// 点击登录按钮时启动客户端
            hostButton.interactable = Application.platform != RuntimePlatform.WebGLPlayer && !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);// 主机按钮只有在网络处于非活动状态且用户名符合要求时可交互
            hostButton.onClick.SetListener(() => { manager.StartHost(); });// 点击主机按钮时启动主机
            cancelButton.gameObject.SetActive(NetworkClient.isConnecting);// 如果正在连接中，则显示取消按钮
            cancelButton.onClick.SetListener(() => { manager.StopClient(); });// 点击取消按钮时停止客户端
            dedicatedButton.interactable = Application.platform != RuntimePlatform.WebGLPlayer && !manager.isNetworkActive;// 独立服务器按钮只有在网络处于非活动状态时可交互
            dedicatedButton.onClick.SetListener(() => { manager.StartServer(); });// 点击独立服务器按钮时启动服务器
            quitButton.onClick.SetListener(() => { NetworkManagerMMO.Quit(); });// 点击退出按钮时退出游戏

            // 将玩家输入的用户名和密码赋值给Authenticator中的loginAccount和loginPassword
            auth.loginAccount = accountInput.text;
            auth.loginPassword = passwordInput.text;

            // 拷贝服务器列表到下拉列表中，并将选中的选项拷贝到NetworkManager的ip/port中
            serverDropdown.interactable = !manager.isNetworkActive;// 如果网络处于活动状态，则禁用下拉列表
            serverDropdown.options = manager.serverList.Select(
                sv => new Dropdown.OptionData(sv.name)
            ).ToList();// 将服务器列表中的名称添加到下拉列表中
            manager.networkAddress = manager.serverList[serverDropdown.value].ip;// 将选中的服务器的IP地址和端口号拷贝到NetworkManager中
        }
        else panel.SetActive(false);// 如果玩家已登录，则隐藏UI界面
    }
}
