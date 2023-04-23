// 我们使用自定义 NetworkManager 来处理登录、角色选择、角色创建等。
// 我们不使用 playerPrefab，而是将所有可用的玩家类拖放到可生成对象属性中。
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Mirror;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 我们需要一个明确定义的网络状态，以便知道我们是离线/在世界中/在大厅中
// 否则，UICharacterSelection等不知道他们是否应该100％可见。
public enum NetworkState { Offline, Handshake, Lobby, World } // 定义不同的网络状态，包括离线、握手、大厅和世界。

[Serializable] public class UnityEventCharactersAvailableMsg : UnityEvent<CharactersAvailableMsg> {}// 定义可序列化的Unity事件，用于携带角色可用信息。
[Serializable] public class UnityEventCharacterCreateMsgPlayer : UnityEvent<CharacterCreateMsg, Player> {}// 定义可序列化的Unity事件，用于创建角色和玩家相关信息。
[Serializable] public class UnityEventStringGameObjectNetworkConnectionCharacterSelectMsg : UnityEvent<string, GameObject, NetworkConnection, CharacterSelectMsg> {}// 定义可序列化的Unity事件，用于在字符串、游戏对象、网络连接和角色选择信息之间传递
[Serializable] public class UnityEventCharacterDeleteMsg : UnityEvent<CharacterDeleteMsg> {}// 定义可序列化的Unity事件，用于删除角色信息。

[RequireComponent(typeof(Database))]// 必须挂载Database组件才能使用该组件。
[DisallowMultipleComponent]// 不允许挂载多个该组件。
public partial class NetworkManagerMMO : NetworkManager
{
    // client端当前网络管理器的状态
    public NetworkState state = NetworkState.Offline;//初始状态为离线

    // 大厅中<连接，帐户>字典
    // （仍在创建或选择角色的人）
    public Dictionary<NetworkConnection, string> lobby = new Dictionary<NetworkConnection, string>();

    // UI组件，避免使用FindObjectOfType
    [Header("UI")]
    public UIPopup uiPopup; // 弹出框UI组件

    // 如果第一个游戏服务器过于拥挤，我们可能会想要添加另一个游戏服务器。
    // 服务器列表允许玩家选择一个服务器。
    //
    // 注意：我们使用一个端口为所有服务器提供服务，以便无头服务器知道要绑定到哪个端口。
    // 否则，它将不得不知道从列表中选择哪一个，这太过复杂。一个端口为所有服务器提供服务就足够了。
    // 对于独立的MMORPG来说这样做就可以了。
    [Serializable]
    public class ServerInfo
    {
        public string name;//服务器名称
        public string ip;//服务器IP地址
    }
    public List<ServerInfo> serverList = new List<ServerInfo>() {
        new ServerInfo{name="Local", ip="localhost"}//默认使用本地localhost作为服务器
    };

    [Header("Logout")]//注释：注销
    [Tooltip("Players shouldn't be able to log out instantly to flee combat. There should be a delay.")]//注释：玩家不能立即注销以逃避战斗。应该有一个延迟。
    public float combatLogoutDelay = 5;

    [Header("Character Selection")]
    public int selection = -1;//注释：角色选择
    public Transform[] selectionLocations;//位置数组
    public Transform selectionCameraLocation;//相机位置
    [HideInInspector] public List<Player> playerClasses = new List<Player>(); //在Awake中缓存

    [Header("Database")]//注释：数据库
    public int characterLimit = 4;//角色数量上限
    public int characterNameMaxLength = 16;//角色名字最长长度
    public float saveInterval = 60f; //保存的时间间隔（单位：秒）

    // 我们仍然需要为NetworkManager提供OnStartClient/Server/etc.事件，因为这些不是所有组件都会获得的常规NetworkBehaviour事件。
    //以下是所有预定义事件：
    //开始客户端事件。
    public UnityEvent onStartClient;
    //停止客户端事件。
    public UnityEvent onStopClient;
    //开始服务器事件。
    public UnityEvent onStartServer;
    //停止服务器事件。
    public UnityEvent onStopServer;
    //客户端连接事件。
    public UnityEventNetworkConnection onClientConnect;
    //服务器连接事件。
    public UnityEventNetworkConnection onServerConnect;
    //客户端可用角色事件。
    public UnityEventCharactersAvailableMsg onClientCharactersAvailable;
    //服务器角色创建事件。
    public UnityEventCharacterCreateMsgPlayer onServerCharacterCreate;
    //服务器角色选择事件。
    public UnityEventStringGameObjectNetworkConnectionCharacterSelectMsg onServerCharacterSelect;
    //服务器角色删除事件。
    public UnityEventCharacterDeleteMsg onServerCharacterDelete;
    //客户端断开连接事件。
    public UnityEventNetworkConnection onClientDisconnect;
    //服务器断开连接事件。
    public UnityEventNetworkConnection onServerDisconnect;

    // 在客户端上存储可用角色信息，以便UI可以访问它
    [HideInInspector] public CharactersAvailableMsg charactersAvailableMsg;

    // 名称检查
    public bool IsAllowedCharacterName(string characterName)
    {
        // 长度不要太长？
        // 只包含字母、数字和下划线，且不为空（+）？
        //（对于数据库安全等很重要。）
        return characterName.Length <= characterNameMaxLength &&
               Regex.IsMatch(characterName, @"^[a-zA-Z0-9_]+$");
    }

    // 获取最近的出生点
    public static Transform GetNearestStartPosition(Vector2 from) =>
        Utils.GetNearestTransform(startPositions, from);

    // 玩家类别
    public List<Player> FindPlayerClasses()
    {
        // 从spawnPrefabs中筛选出所有的Player预制件
        // （出于性能和垃圾回收的考虑，避免使用Linq。玩家被大量生成。这点很重要。）
        List<Player> classes = new List<Player>();
        foreach (GameObject prefab in spawnPrefabs)
        {
            Player player = prefab.GetComponent<Player>();
            if (player != null)
                classes.Add(player);
        }
        return classes;
    }

    // 事件
    public override void Awake()
    {
        base.Awake();

        // 缓存玩家角色的预制体列表。
        // => 我们假设这在运行时不会改变（为什么会改变呢？）
        // => 这比每次循环所有预制件在角色
        //    选择/创建/删除时更好！
        playerClasses = FindPlayerClasses();
    }

    void Update()
    {
        // 是否存在有效的本地玩家？有则将状态设置为世界网络状态。
        if (NetworkClient.localPlayer != null)
            state = NetworkState.World;
    }

    // 错误信息函数 
    public void ServerSendError(NetworkConnection conn, string error, bool disconnect)
    {
        conn.Send(new ErrorMsg{text=error, causesDisconnect=disconnect});// 向网络连接发送错误信息
    }

    void OnClientError(ErrorMsg message)// 当客户端出现错误时执行此函数
    {
        Debug.Log("OnClientError: " + message.text);// 记录错误信息到Debug.Log

        // 弹出一个窗口显示错误信息
        uiPopup.Show(message.text);

        // 如果这是一个重要的网络错误
        //（需要这样做的原因是登录失败的消息并不会立即断开客户端，而只有在超时后才会这样做）
        if (message.causesDisconnect)
        {
            NetworkClient.connection.Disconnect();// 断开连接

            // 如果当前是 host，也停止 host
            //（host 不应该启动服务器，而应该断开客户端的无效登录，这是毫无意义的）
            if (NetworkServer.active) StopHost();
        }
    }

    // 开始和停止
    public override void OnStartClient()
    {
        // 设置处理程序
        NetworkClient.RegisterHandler<ErrorMsg>(OnClientError, false); // 允许在认证前使用！
        NetworkClient.RegisterHandler<CharactersAvailableMsg>(OnClientCharactersAvailable);

        // 插件系统钩子
        onStartClient.Invoke();
    }

    public override void OnStartServer()
    {
        // 在服务器启动时执行以下操作：
        // 连接数据库
        Database.singleton.Connect();

        // 注册握手包处理程序（在服务器启动时注册，以便重新连接工作）
        NetworkServer.RegisterHandler<CharacterCreateMsg>(OnServerCharacterCreate);
        NetworkServer.RegisterHandler<CharacterSelectMsg>(OnServerCharacterSelect);
        NetworkServer.RegisterHandler<CharacterDeleteMsg>(OnServerCharacterDelete);

        // 定时保存玩家数据
        InvokeRepeating(nameof(SavePlayers), saveInterval, saveInterval);

        // 插件系统的钩子函数
        onStartServer.Invoke();
    }

    public override void OnStopClient()
    {
        //在客户端停止时执行以下操作：
        //插件系统的钩子函数
        onStopClient.Invoke();
    }
        //在服务器停止时执行以下操作
    public override void OnStopServer()
    {
        CancelInvoke(nameof(SavePlayers));//取消保存玩家数据定时器

        // 插件系统的钩子函数
        onStopServer.Invoke();
    }

    // 握手：登录
    // 如果客户端成功通过身份验证后连接，调用此函数。
    public override void OnClientConnect(NetworkConnection conn)
    {
        // 插件系统的钩子函数
        onClientConnect.Invoke(conn);

        // 不要调用原始函数，否则客户端会被设置为“准备就绪”状态。
        // 只有在选择角色后才应该设置为“准备就绪”状态。
        // 否则，它可能会收到来自怪物等的全局消息。
        // 原始函数：
        // base.OnClientConnect(conn);
    }

    // called on the server if a client connects after successful auth
    public override void OnServerConnect(NetworkConnection conn)
    {
        // grab the account from the lobby
        string account = lobby[conn];

        // send necessary data to client
        conn.Send(MakeCharactersAvailableMessage(account));

        // addon system hooks
        onServerConnect.Invoke(conn);
    }

    // the default OnClientSceneChanged sets the client as ready automatically,
    // which makes no sense for MMORPG situations. this was more for situations
    // where the server tells all clients to load a new scene.
    // -> setting client as ready will cause 'already set as ready' errors if
    //    we call StartClient before loading a new scene (e.g. for zones)
    // -> it's best to just overwrite this with an empty function
    public override void OnClientSceneChanged(NetworkConnection conn) {}

    // helper function to make a CharactersAvailableMsg from all characters in
    // an account
    CharactersAvailableMsg MakeCharactersAvailableMessage(string account)
    {
        // load from database
        // (avoid Linq for performance/gc. characters are loaded frequently!)
        List<Player> characters = new List<Player>();
        foreach (string characterName in Database.singleton.CharactersForAccount(account))
        {
            GameObject player = Database.singleton.CharacterLoad(characterName, playerClasses, true);
            characters.Add(player.GetComponent<Player>());
        }

        // construct the message
        CharactersAvailableMsg message = new CharactersAvailableMsg();
        message.Load(characters);

        // destroy the temporary players again and return the result
        characters.ForEach(player => Destroy(player.gameObject));
        return message;
    }

    // handshake: character selection //////////////////////////////////////////
    void LoadPreview(GameObject prefab, Transform location, int selectionIndex, CharactersAvailableMsg.CharacterPreview character)
    {
        // instantiate the prefab
        GameObject preview = Instantiate(prefab.gameObject, location.position, location.rotation);
        preview.transform.parent = location;
        Player player = preview.GetComponent<Player>();

        // assign basic preview values like name and equipment
        player.name = character.name;
        //player.isGameMaster = character.isGameMaster;
        for (int i = 0; i < character.equipment.Length; ++i)
        {
            ItemSlot slot = character.equipment[i];
            player.equipment.slots.Add(slot);
            if (slot.amount > 0)
            {
                // OnEquipmentChanged won't be called unless spawned, we
                // need to refresh manually
                ((PlayerEquipment)player.equipment).RefreshLocation(i);
            }
        }

        // add selection script
        preview.AddComponent<SelectableCharacter>();
        preview.GetComponent<SelectableCharacter>().index = selectionIndex;
    }

    public void ClearPreviews()
    {
        selection = -1;
        foreach (Transform location in selectionLocations)
            if (location.childCount > 0)
                Destroy(location.GetChild(0).gameObject);
    }

    void OnClientCharactersAvailable(CharactersAvailableMsg message)
    {
        charactersAvailableMsg = message;
        Debug.Log("characters available:" + charactersAvailableMsg.characters.Length);

        // set state
        state = NetworkState.Lobby;

        // clear previous previews in any case
        ClearPreviews();

        // load previews for 3D character selection
        for (int i = 0; i < charactersAvailableMsg.characters.Length; ++i)
        {
            CharactersAvailableMsg.CharacterPreview character = charactersAvailableMsg.characters[i];

            // find the prefab for that class
            Player prefab = playerClasses.Find(p => p.name == character.className);
            if (prefab != null)
                LoadPreview(prefab.gameObject, selectionLocations[i], i, character);
            else
                Debug.LogWarning("Character Selection: no prefab found for class " + character.className);
        }

        // setup camera
        Camera.main.transform.position = selectionCameraLocation.position;
        Camera.main.transform.rotation = selectionCameraLocation.rotation;

        // addon system hooks
        onClientCharactersAvailable.Invoke(charactersAvailableMsg);
    }

    // handshake: character creation ///////////////////////////////////////////
    // find a NetworkStartPosition for this class, or a normal one otherwise
    // (ignore the ones with playerPrefab == null)
    public Transform GetStartPositionFor(string className)
    {
        // avoid Linq for performance/GC. players spawn frequently!
        foreach (Transform startPosition in startPositions)
        {
            NetworkStartPositionForClass spawn = startPosition.GetComponent<NetworkStartPositionForClass>();
            if (spawn != null &&
                spawn.playerPrefab != null &&
                spawn.playerPrefab.name == className)
                return spawn.transform;
        }
        // return any start position otherwise
        return GetStartPosition();
    }

    Player CreateCharacter(GameObject classPrefab, string characterName, string account)//, bool gameMaster)
    {
        // create new character based on the prefab.
        // -> we also assign default items and equipment for new characters
        // -> skills are handled in Database.CharacterLoad every time. if we
        //    add new ones to a prefab, all existing players should get them
        // (instantiate temporary player)
        //Debug.Log("creating character: " + message.name + " " + message.classIndex);
        Player player = Instantiate(classPrefab).GetComponent<Player>();
        player.name = characterName;
        player.account = account;
        player.className = classPrefab.name;
        player.transform.position = GetStartPositionFor(player.className).position;
        for (int i = 0; i < player.inventory.size; ++i)
        {
            // add empty slot or default item if any
            player.inventory.slots.Add(i < player.inventory.defaultItems.Length ? new ItemSlot(new Item(player.inventory.defaultItems[i].item), player.inventory.defaultItems[i].amount) : new ItemSlot());
        }
        for (int i = 0; i < ((PlayerEquipment)player.equipment).slotInfo.Length; ++i)
        {
            // add empty slot or default item if any
            EquipmentInfo info = ((PlayerEquipment)player.equipment).slotInfo[i];
            player.equipment.slots.Add(info.defaultItem.item != null ? new ItemSlot(new Item(info.defaultItem.item), info.defaultItem.amount) : new ItemSlot());
        }
        player.health.current = player.health.max; // after equipment in case of boni
        player.mana.current = player.mana.max; // after equipment in case of boni
        //player.isGameMaster = gameMaster;

        return player;
    }

    void OnServerCharacterCreate(NetworkConnection conn, CharacterCreateMsg message)
    {
        //Debug.Log("OnServerCharacterCreate " + conn);

        // only while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(conn))
        {
            // allowed character name?
            if (IsAllowedCharacterName(message.name))
            {
                // not existent yet?
                string account = lobby[conn];
                if (!Database.singleton.CharacterExists(message.name))
                {
                    // not too may characters created yet?
                    if (Database.singleton.CharactersForAccount(account).Count < characterLimit)
                    {
                        // valid class index?
                        if (0 <= message.classIndex && message.classIndex < playerClasses.Count)
                        {
                            // game master can only be requested by the host.
                            // DO NOT allow regular connections to create GMs!
                            //if (message.gameMaster == false ||
                            //    conn == NetworkServer.localConnection)
                            //{
                                // create new character based on the prefab.
                                Player player = CreateCharacter(playerClasses[message.classIndex].gameObject, message.name, account); //, message.gameMaster);

                                // addon system hooks
                                onServerCharacterCreate.Invoke(message, player);

                                // save the player
                                Database.singleton.CharacterSave(player, false);
                                Destroy(player.gameObject);

                                // send available characters list again, causing
                                // the client to switch to the character
                                // selection scene again
                                conn.Send(MakeCharactersAvailableMessage(account));
                            //}
                            //else
                            //{
                            //    //Debug.Log("character insufficient permissions for GM request: " + conn);  <- don't show on live server
                            //    ServerSendError(conn, "insufficient permissions", false);
                            //}
                        }
                        else
                        {
                            //Debug.Log("character invalid class: " + message.classIndex);  <- don't show on live server
                            ServerSendError(conn, "character invalid class", false);
                        }
                    }
                    else
                    {
                        //Debug.Log("character limit reached: " + message.name); <- don't show on live server
                        ServerSendError(conn, "character limit reached", false);
                    }
                }
                else
                {
                    //Debug.Log("character name already exists: " + message.name); <- don't show on live server
                    ServerSendError(conn, "name already exists", false);
                }
            }
            else
            {
                //Debug.Log("character name not allowed: " + message.name); <- don't show on live server
                ServerSendError(conn, "character name not allowed", false);
            }
        }
        else
        {
            //Debug.Log("CharacterCreate: not in lobby"); <- don't show on live server
            ServerSendError(conn, "CharacterCreate: not in lobby", true);
        }
    }

    // overwrite the original OnServerAddPlayer function so nothing happens if
    // someone sends that message.
    public override void OnServerAddPlayer(NetworkConnection conn) { Debug.LogWarning("Use the CharacterSelectMsg instead"); }

    void OnServerCharacterSelect(NetworkConnection conn, CharacterSelectMsg message)
    {
        //Debug.Log("OnServerCharacterSelect");
        // only while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(conn))
        {
            // read the index and find the n-th character
            // (only if we know that he is not ingame, otherwise lobby has
            //  no netMsg.conn key)
            string account = lobby[conn];
            List<string> characters = Database.singleton.CharactersForAccount(account);

            // validate index
            if (0 <= message.index && message.index < characters.Count)
            {
                //Debug.Log(account + " selected player " + characters[index]);

                // load character data
                GameObject go = Database.singleton.CharacterLoad(characters[message.index], playerClasses, false);

                // add to client
                NetworkServer.AddPlayerForConnection(conn, go);

                // addon system hooks
                onServerCharacterSelect.Invoke(account, go, conn, message);

                // remove from lobby
                lobby.Remove(conn);
            }
            else
            {
                Debug.Log("invalid character index: " + account + " " + message.index);
                ServerSendError(conn, "invalid character index", false);
            }
        }
        else
        {
            Debug.Log("CharacterSelect: not in lobby" + conn);
            ServerSendError(conn, "CharacterSelect: not in lobby", true);
        }
    }

    void OnServerCharacterDelete(NetworkConnection conn, CharacterDeleteMsg message)
    {
        //Debug.Log("OnServerCharacterDelete " + conn);

        // only while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(conn))
        {
            string account = lobby[conn];
            List<string> characters = Database.singleton.CharactersForAccount(account);

            // validate index
            if (0 <= message.index && message.index < characters.Count)
            {
                // delete the character
                Debug.Log("delete character: " + characters[message.index]);
                Database.singleton.CharacterDelete(characters[message.index]);

                // addon system hooks
                onServerCharacterDelete.Invoke(message);

                // send the new character list to client
                conn.Send(MakeCharactersAvailableMessage(account));
            }
            else
            {
                Debug.Log("invalid character index: " + account + " " + message.index);
                ServerSendError(conn, "invalid character index", false);
            }
        }
        else
        {
            Debug.Log("CharacterDelete: not in lobby: " + conn);
            ServerSendError(conn, "CharacterDelete: not in lobby", true);
        }
    }

    // player saving ///////////////////////////////////////////////////////////
    // we have to save all players at once to make sure that item trading is
    // perfectly save. if we would invoke a save function every few minutes on
    // each player seperately then it could happen that two players trade items
    // and only one of them is saved before a server crash - hence causing item
    // duplicates.
    void SavePlayers()
    {
        Database.singleton.CharacterSaveMany(Player.onlinePlayers.Values);
        if (Player.onlinePlayers.Count > 0)
            Debug.Log("saved " + Player.onlinePlayers.Count + " player(s)");
    }

    // stop/disconnect /////////////////////////////////////////////////////////
    // called on the server when a client disconnects
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        //Debug.Log("OnServerDisconnect " + conn);

        // players shouldn't be able to log out instantly to flee combat.
        // there should be a delay.
        float delay = 0;
        if (conn.identity != null)
        {
            Player player = conn.identity.GetComponent<Player>();
            delay = (float)player.remainingLogoutTime;
        }

        StartCoroutine(DoServerDisconnect(conn, delay));
    }

    IEnumerator<WaitForSeconds> DoServerDisconnect(NetworkConnection conn, float delay)
    {
        yield return new WaitForSeconds(delay);

        //Debug.Log("DoServerDisconnect " + conn);

        // save player (if any. nothing to save if disconnecting while in lobby.)
        if (conn.identity != null)
        {
            Database.singleton.CharacterSave(conn.identity.GetComponent<Player>(), false);
            Debug.Log("saved:" + conn.identity.name);
        }

        // addon system hooks
        onServerDisconnect.Invoke(conn);

        // remove logged in account after everything else was done
        lobby.Remove(conn); // just returns false if not found

        // do base function logic (removes the player for the connection)
        base.OnServerDisconnect(conn);
    }

    // called on the client if he disconnects
    public override void OnClientDisconnect(NetworkConnection conn)
    {
        Debug.Log("OnClientDisconnect");

        // take the camera out of the local player so it doesn't get destroyed
        // -> this is necessary for character controller movement where a camera
        //    gets parented to a player.
        Camera mainCamera = Camera.main;
        if (mainCamera.transform.parent != null)
            mainCamera.transform.SetParent(null);

        // show a popup so that users know what happened
        uiPopup.Show("Disconnected.");

        // call base function to guarantee proper functionality
        base.OnClientDisconnect(conn);

        // set state
        state = NetworkState.Offline;

        // addon system hooks
        onClientDisconnect.Invoke(conn);
    }

    // universal quit function for editor & build
    public static void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public override void OnValidate()
    {
        base.OnValidate();

        // ip has to be changed in the server list. make it obvious to users.
        if (!Application.isPlaying && networkAddress != "")
            networkAddress = "Use the Server List below!";

        // need enough character selection locations for character limit
        if (selectionLocations.Length != characterLimit)
        {
            // create new array with proper size
            Transform[] newArray = new Transform[characterLimit];

            // copy old values
            for (int i = 0; i < Mathf.Min(characterLimit, selectionLocations.Length); ++i)
                newArray[i] = selectionLocations[i];

            // use new array
            selectionLocations = newArray;
        }
    }
}
