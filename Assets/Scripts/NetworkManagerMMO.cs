// ����ʹ���Զ��� NetworkManager �������¼����ɫѡ�񡢽�ɫ�����ȡ�
// ���ǲ�ʹ�� playerPrefab�����ǽ����п��õ�������Ϸŵ������ɶ��������С�
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Mirror;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ������Ҫһ����ȷ���������״̬���Ա�֪������������/��������/�ڴ�����
// ����UICharacterSelection�Ȳ�֪�������Ƿ�Ӧ��100���ɼ���
public enum NetworkState { Offline, Handshake, Lobby, World } // ���岻ͬ������״̬���������ߡ����֡����������硣

[Serializable] public class UnityEventCharactersAvailableMsg : UnityEvent<CharactersAvailableMsg> {}// ��������л���Unity�¼�������Я����ɫ������Ϣ��
[Serializable] public class UnityEventCharacterCreateMsgPlayer : UnityEvent<CharacterCreateMsg, Player> {}// ��������л���Unity�¼������ڴ�����ɫ����������Ϣ��
[Serializable] public class UnityEventStringGameObjectNetworkConnectionCharacterSelectMsg : UnityEvent<string, GameObject, NetworkConnection, CharacterSelectMsg> {}// ��������л���Unity�¼����������ַ�������Ϸ�����������Ӻͽ�ɫѡ����Ϣ֮�䴫��
[Serializable] public class UnityEventCharacterDeleteMsg : UnityEvent<CharacterDeleteMsg> {}// ��������л���Unity�¼�������ɾ����ɫ��Ϣ��

[RequireComponent(typeof(Database))]// �������Database�������ʹ�ø������
[DisallowMultipleComponent]// ��������ض���������
public partial class NetworkManagerMMO : NetworkManager
{
    // client�˵�ǰ�����������״̬
    public NetworkState state = NetworkState.Offline;//��ʼ״̬Ϊ����

    // ������<���ӣ��ʻ�>�ֵ�
    // �����ڴ�����ѡ���ɫ���ˣ�
    public Dictionary<NetworkConnection, string> lobby = new Dictionary<NetworkConnection, string>();

    // UI���������ʹ��FindObjectOfType
    [Header("UI")]
    public UIPopup uiPopup; // ������UI���

    // �����һ����Ϸ����������ӵ�������ǿ��ܻ���Ҫ�����һ����Ϸ��������
    // �������б��������ѡ��һ����������
    //
    // ע�⣺����ʹ��һ���˿�Ϊ���з������ṩ�����Ա���ͷ������֪��Ҫ�󶨵��ĸ��˿ڡ�
    // �����������ò�֪�����б���ѡ����һ������̫�����ӡ�һ���˿�Ϊ���з������ṩ������㹻�ˡ�
    // ���ڶ�����MMORPG��˵�������Ϳ����ˡ�
    [Serializable]
    public class ServerInfo
    {
        public string name;//����������
        public string ip;//������IP��ַ
    }
    public List<ServerInfo> serverList = new List<ServerInfo>() {
        new ServerInfo{name="Local", ip="localhost"}//Ĭ��ʹ�ñ���localhost��Ϊ������
    };

    [Header("Logout")]//ע�ͣ�ע��
    [Tooltip("Players shouldn't be able to log out instantly to flee combat. There should be a delay.")]//ע�ͣ���Ҳ�������ע�����ӱ�ս����Ӧ����һ���ӳ١�
    public float combatLogoutDelay = 5;

    [Header("Character Selection")]
    public int selection = -1;//ע�ͣ���ɫѡ��
    public Transform[] selectionLocations;//λ������
    public Transform selectionCameraLocation;//���λ��
    [HideInInspector] public List<Player> playerClasses = new List<Player>(); //��Awake�л���

    [Header("Database")]//ע�ͣ����ݿ�
    public int characterLimit = 4;//��ɫ��������
    public int characterNameMaxLength = 16;//��ɫ���������
    public float saveInterval = 60f; //�����ʱ��������λ���룩

    // ������Ȼ��ҪΪNetworkManager�ṩOnStartClient/Server/etc.�¼�����Ϊ��Щ����������������õĳ���NetworkBehaviour�¼���
    //����������Ԥ�����¼���
    //��ʼ�ͻ����¼���
    public UnityEvent onStartClient;
    //ֹͣ�ͻ����¼���
    public UnityEvent onStopClient;
    //��ʼ�������¼���
    public UnityEvent onStartServer;
    //ֹͣ�������¼���
    public UnityEvent onStopServer;
    //�ͻ��������¼���
    public UnityEventNetworkConnection onClientConnect;
    //�����������¼���
    public UnityEventNetworkConnection onServerConnect;
    //�ͻ��˿��ý�ɫ�¼���
    public UnityEventCharactersAvailableMsg onClientCharactersAvailable;
    //��������ɫ�����¼���
    public UnityEventCharacterCreateMsgPlayer onServerCharacterCreate;
    //��������ɫѡ���¼���
    public UnityEventStringGameObjectNetworkConnectionCharacterSelectMsg onServerCharacterSelect;
    //��������ɫɾ���¼���
    public UnityEventCharacterDeleteMsg onServerCharacterDelete;
    //�ͻ��˶Ͽ������¼���
    public UnityEventNetworkConnection onClientDisconnect;
    //�������Ͽ������¼���
    public UnityEventNetworkConnection onServerDisconnect;

    // �ڿͻ����ϴ洢���ý�ɫ��Ϣ���Ա�UI���Է�����
    [HideInInspector] public CharactersAvailableMsg charactersAvailableMsg;

    // ���Ƽ��
    public bool IsAllowedCharacterName(string characterName)
    {
        // ���Ȳ�Ҫ̫����
        // ֻ������ĸ�����ֺ��»��ߣ��Ҳ�Ϊ�գ�+����
        //���������ݿⰲȫ�Ⱥ���Ҫ����
        return characterName.Length <= characterNameMaxLength &&
               Regex.IsMatch(characterName, @"^[a-zA-Z0-9_]+$");
    }

    // ��ȡ����ĳ�����
    public static Transform GetNearestStartPosition(Vector2 from) =>
        Utils.GetNearestTransform(startPositions, from);

    // ������
    public List<Player> FindPlayerClasses()
    {
        // ��spawnPrefabs��ɸѡ�����е�PlayerԤ�Ƽ�
        // ���������ܺ��������յĿ��ǣ�����ʹ��Linq����ұ��������ɡ�������Ҫ����
        List<Player> classes = new List<Player>();
        foreach (GameObject prefab in spawnPrefabs)
        {
            Player player = prefab.GetComponent<Player>();
            if (player != null)
                classes.Add(player);
        }
        return classes;
    }

    // �¼�
    public override void Awake()
    {
        base.Awake();

        // ������ҽ�ɫ��Ԥ�����б�
        // => ���Ǽ�����������ʱ����ı䣨Ϊʲô��ı��أ���
        // => ���ÿ��ѭ������Ԥ�Ƽ��ڽ�ɫ
        //    ѡ��/����/ɾ��ʱ���ã�
        playerClasses = FindPlayerClasses();
    }

    void Update()
    {
        // �Ƿ������Ч�ı�����ң�����״̬����Ϊ��������״̬��
        if (NetworkClient.localPlayer != null)
            state = NetworkState.World;
    }

    // ������Ϣ���� 
    public void ServerSendError(NetworkConnection conn, string error, bool disconnect)
    {
        conn.Send(new ErrorMsg{text=error, causesDisconnect=disconnect});// ���������ӷ��ʹ�����Ϣ
    }

    void OnClientError(ErrorMsg message)// ���ͻ��˳��ִ���ʱִ�д˺���
    {
        Debug.Log("OnClientError: " + message.text);// ��¼������Ϣ��Debug.Log

        // ����һ��������ʾ������Ϣ
        uiPopup.Show(message.text);

        // �������һ����Ҫ���������
        //����Ҫ��������ԭ���ǵ�¼ʧ�ܵ���Ϣ�����������Ͽ��ͻ��ˣ���ֻ���ڳ�ʱ��Ż���������
        if (message.causesDisconnect)
        {
            NetworkClient.connection.Disconnect();// �Ͽ�����

            // �����ǰ�� host��Ҳֹͣ host
            //��host ��Ӧ����������������Ӧ�öϿ��ͻ��˵���Ч��¼�����Ǻ�������ģ�
            if (NetworkServer.active) StopHost();
        }
    }

    // ��ʼ��ֹͣ
    public override void OnStartClient()
    {
        // ���ô������
        NetworkClient.RegisterHandler<ErrorMsg>(OnClientError, false); // ��������֤ǰʹ�ã�
        NetworkClient.RegisterHandler<CharactersAvailableMsg>(OnClientCharactersAvailable);

        // ���ϵͳ����
        onStartClient.Invoke();
    }

    public override void OnStartServer()
    {
        // �ڷ���������ʱִ�����²�����
        // �������ݿ�
        Database.singleton.Connect();

        // ע�����ְ���������ڷ���������ʱע�ᣬ�Ա��������ӹ�����
        NetworkServer.RegisterHandler<CharacterCreateMsg>(OnServerCharacterCreate);
        NetworkServer.RegisterHandler<CharacterSelectMsg>(OnServerCharacterSelect);
        NetworkServer.RegisterHandler<CharacterDeleteMsg>(OnServerCharacterDelete);

        // ��ʱ�����������
        InvokeRepeating(nameof(SavePlayers), saveInterval, saveInterval);

        // ���ϵͳ�Ĺ��Ӻ���
        onStartServer.Invoke();
    }

    public override void OnStopClient()
    {
        //�ڿͻ���ֹͣʱִ�����²�����
        //���ϵͳ�Ĺ��Ӻ���
        onStopClient.Invoke();
    }
        //�ڷ�����ֹͣʱִ�����²���
    public override void OnStopServer()
    {
        CancelInvoke(nameof(SavePlayers));//ȡ������������ݶ�ʱ��

        // ���ϵͳ�Ĺ��Ӻ���
        onStopServer.Invoke();
    }

    // ���֣���¼
    // ����ͻ��˳ɹ�ͨ�������֤�����ӣ����ô˺�����
    public override void OnClientConnect(NetworkConnection conn)
    {
        // ���ϵͳ�Ĺ��Ӻ���
        onClientConnect.Invoke(conn);

        // ��Ҫ����ԭʼ����������ͻ��˻ᱻ����Ϊ��׼��������״̬��
        // ֻ����ѡ���ɫ���Ӧ������Ϊ��׼��������״̬��
        // ���������ܻ��յ����Թ���ȵ�ȫ����Ϣ��
        // ԭʼ������
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
