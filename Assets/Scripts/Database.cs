﻿// 在SQLite数据库中保存角色数据。我们选择SQLite有以下几个原因：
// - SQLite是基于文件的，不需要设置数据库服务器
//   - 我们可以轻松通过SQL查询进行“删除所有…”或“修改所有…”
//   - 很多人要求使用SQL数据库而不是XML，因为更容易使用和理解
//   - 我们可以允许使用各种名称的角色，包括中文名称，而不会破坏文件系统。
// - 当使用多个服务器实例并进行升级时，我们将需要使用MYSQL或类似的数据库，但升级非常容易实现。
// - XML更容易，但是：
//   - 我们不能轻松地读取“只是角色的类”等，但我们通常需要用它进行角色选择等操作。
//   - 如果每个帐户都是包含玩家的文件夹，那么除非我们使用额外的account.xml文件，否则无法保存其他帐户信息，如密码、禁止等，这会使一切变得更加复杂。
//   - 总会有被禁止的文件名，比如“COM”，当人们试图使用该名称创建帐户或角色时，会引起问题。
//
//   关于物品商城货币：
//   支付提供方的回调函数应该将新订单添加到 character_orders 表中。在玩家游戏时，服务器将会处理这些订单。请不要尝试直接修改 character 表中的“coins”字段。
//
// 打开SQLite数据库文件的工具：
//   Windows/OSX程序：http://sqlitebrowser.org/
//   Firefox扩展：https://addons.mozilla.org/de/firefox/addon/sqlite-manager/
//   Web托管：Adminer/PhpLiteAdmin
//
// 关于性能：
// - 建议仅在使用SQLite连接时保持其打开状态。
//   MMO服务器始终使用它，因此我们一直保持它打开。 这也使我们可以轻松使用事务，并且它将使过渡到MYSQL更容易。
// - 事务肯定是必要的：
//   保存100个玩家而不使用事务需要3.6秒
//   保存100个玩家并使用事务只需要0.38秒
// - 使用tr = conn.BeginTransaction() + tr.Commit()并将其传递到所有函数中非常复杂。 我们使用BEGIN + END查询代替。
//
// 一些基准测试结果：
// 未优化情况下保存100个玩家：4秒
// 始终保持连接打开+事务的100个玩家：3.6秒
// 始终保持连接打开+事务+WAL的100个玩家：3.6秒
// 在1个`using tr = ...`事务中保存100个玩家：380毫秒
// 使用BEGIN / END样式事务保存100个玩家：380毫秒
// 使用XML保存100个玩家：369毫秒
// 使用mono-sqlite @2019-10-03保存1000个玩家：843毫秒
// 使用sqlite-net @2019-10-03保存1000个玩家：90毫秒（！）
//
// 构建说明：
// - 需要将Player设置为“.NET”而不是“.NET Subset”，否则会导致ArgumentException错误。
// - 需要为独立版（windows/mac/linux）准备 x86 和 x64 版本的 sqlite3.dll
//   => 在sqlite.org上找到
// - 需要为Android准备x86和armeabi-v7a版本的libsqlite3.so
//   => 使用android ndk r9b linux从sqlite.org合并源代码编译
using UnityEngine;
using Mirror;
using System;
using System.IO;
using System.Collections.Generic;
using SQLite;
using UnityEngine.Events;

// from https://github.com/praeclarum/sqlite-net

public partial class Database : MonoBehaviour
{
    // singleton for easier access
    public static Database singleton;

    // file name
    public string databaseFile = "Database.sqlite";

    // connection
    SQLiteConnection connection;

    // database layout via .NET classes:
    // https://github.com/praeclarum/sqlite-net/wiki/GettingStarted
    class accounts
    {
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        public string name { get; set; }
        public string password { get; set; }
        // created & lastlogin for statistics like CCU/MAU/registrations/...
        public DateTime created { get; set; }
        public DateTime lastlogin { get; set; }
        public bool banned { get; set; }
    }
    class characters
    {
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        [Collation("NOCASE")] // [COLLATE NOCASE for case insensitive compare. this way we can't both create 'Archer' and 'archer' as characters]
        public string name { get; set; }
        [Indexed] // add index on account to avoid full scans when loading characters
        public string account { get; set; }
        public string classname { get; set; } // 'class' isn't available in C#
        public float x { get; set; }
        public float y { get; set; }
        public int level { get; set; }
        public int health { get; set; }
        public int mana { get; set; }
        public int strength { get; set; }
        public int intelligence { get; set; }
        public long experience { get; set; } // TODO does long work?
        public long skillExperience { get; set; } // TODO does long work?
        public long gold { get; set; } // TODO does long work?
        public long coins { get; set; } // TODO does long work?
        // online status can be checked from external programs with either just
        // just 'online', or 'online && (DateTime.UtcNow - lastsaved) <= 1min)
        // which is robust to server crashes too.
        public bool online { get; set; }
        public DateTime lastsaved { get; set; }
        public bool deleted { get; set; }
    }
    class character_inventory
    {
        public string character { get; set; }
        public int slot { get; set; }
        public string name { get; set; }
        public int amount { get; set; }
        public int summonedHealth { get; set; }
        public int summonedLevel { get; set; }
        public long summonedExperience { get; set; } // TODO does long work?
        // PRIMARY KEY (character, slot) is created manually.
    }
    class character_equipment : character_inventory // same layout
    {
        // PRIMARY KEY (character, slot) is created manually.
    }
    class character_itemcooldowns
    {
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        public string character { get; set; }
        public string category { get; set; }
        public float cooldownEnd { get; set; }
    }
    class character_skills
    {
        public string character { get; set; }
        public string name { get; set; }
        public int level { get; set; }
        public float castTimeEnd { get; set; }
        public float cooldownEnd { get; set; }
        // PRIMARY KEY (character, name) is created manually.
    }
    class character_buffs
    {
        public string character { get; set; }
        public string name { get; set; }
        public int level { get; set; }
        public float buffTimeEnd { get; set; }
        // PRIMARY KEY (character, name) is created manually.
    }
    class character_quests
    {
        public string character { get; set; }
        public string name { get; set; }
        public int progress { get; set; }
        public bool completed { get; set; }
        // PRIMARY KEY (character, name) is created manually.
    }
    class character_orders
    {
        // INTEGER PRIMARY KEY is auto incremented by sqlite if the insert call
        // passes NULL for it.
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        public int orderid { get; set; }
        public string character { get; set; }
        public long coins { get; set; }
        public bool processed { get; set; }
    }
    class character_guild
    {
        // guild members are saved in a separate table because instead of in a
        // characters.guild field because:
        // * guilds need to be resaved independently, not just in CharacterSave
        // * kicked members' guilds are cleared automatically because we drop
        //   and then insert all members each time. otherwise we'd have to
        //   update the kicked member's guild field manually each time
        // * it's easier to remove / modify the guild feature if it's not hard-
        //   coded into the characters table
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        public string character { get; set; }
        // add index on guild to avoid full scans when loading guild members
        [Indexed]
        public string guild { get; set; }
        public int rank { get; set; }
    }
    class guild_info
    {
        // guild master is not in guild_info in case we need more than one later
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        public string name { get; set; }
        public string notice { get; set; }
    }

    [Header("Events")]
    // use onConnected to create an extra table for your addon
    public UnityEvent onConnected;
    public UnityEventPlayer onCharacterLoad;
    public UnityEventPlayer onCharacterSave;

    void Awake()
    {
        // initialize singleton
        if (singleton == null) singleton = this;
    }

    // connect /////////////////////////////////////////////////////////////////
    // only call this from the server, not from the client. otherwise the client
    // would create a database file / webgl would throw errors, etc.
    public void Connect()
    {
        // database path: Application.dataPath is always relative to the project,
        // but we don't want it inside the Assets folder in the Editor (git etc.),
        // instead we put it above that.
        // we also use Path.Combine for platform independent paths
        // and we need persistentDataPath on android
#if UNITY_EDITOR
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, databaseFile);
#elif UNITY_ANDROID
        string path = Path.Combine(Application.persistentDataPath, databaseFile);
#elif UNITY_IOS
        string path = Path.Combine(Application.persistentDataPath, databaseFile);
#else
        string path = Path.Combine(Application.dataPath, databaseFile);
#endif

        // open connection
        // note: automatically creates database file if not created yet
        connection = new SQLiteConnection(path);

        // create tables if they don't exist yet or were deleted
        connection.CreateTable<accounts>();
        connection.CreateTable<characters>();
        connection.CreateTable<character_inventory>();
        connection.CreateIndex(nameof(character_inventory), new []{"character", "slot"});
        connection.CreateTable<character_equipment>();
        connection.CreateIndex(nameof(character_equipment), new []{"character", "slot"});
        connection.CreateTable<character_itemcooldowns>();
        connection.CreateTable<character_skills>();
        connection.CreateIndex(nameof(character_skills), new []{"character", "name"});
        connection.CreateTable<character_buffs>();
        connection.CreateIndex(nameof(character_buffs), new []{"character", "name"});
        connection.CreateTable<character_quests>();
        connection.CreateIndex(nameof(character_quests), new []{"character", "name"});
        connection.CreateTable<character_orders>();
        connection.CreateTable<character_guild>();
        connection.CreateTable<guild_info>();

        // addon system hooks
        onConnected.Invoke();

        //Debug.Log("connected to database");
    }

    // close connection when Unity closes to prevent locking
    void OnApplicationQuit()
    {
        connection?.Close();
    }

    // account data ////////////////////////////////////////////////////////////
    // try to log in with an account.
    // -> not called 'CheckAccount' or 'IsValidAccount' because it both checks
    //    if the account is valid AND sets the lastlogin field
    public bool TryLogin(string account, string password)
    {
        // this function can be used to verify account credentials in a database
        // or a content management system.
        //
        // for example, we could setup a content management system with a forum,
        // news, shop etc. and then use a simple HTTP-GET to check the account
        // info, for example:
        //
        //   var request = new WWW("example.com/verify.php?id="+id+"&amp;pw="+pw);
        //   while (!request.isDone)
        //       print("loading...");
        //   return request.error == null && request.text == "ok";
        //
        // where verify.php is a script like this one:
        //   <?php
        //   // id and pw set with HTTP-GET?
        //   if (isset($_GET['id']) && isset($_GET['pw'])) {
        //       // validate id and pw by using the CMS, for example in Drupal:
        //       if (user_authenticate($_GET['id'], $_GET['pw']))
        //           echo "ok";
        //       else
        //           echo "invalid id or pw";
        //   }
        //   ?>
        //
        // or we could check in a MYSQL database:
        //   var dbConn = new MySql.Data.MySqlClient.MySqlConnection("Persist Security Info=False;server=localhost;database=notas;uid=root;password=" + dbpwd);
        //   var cmd = dbConn.CreateCommand();
        //   cmd.CommandText = "SELECT id FROM accounts WHERE id='" + account + "' AND pw='" + password + "'";
        //   dbConn.Open();
        //   var reader = cmd.ExecuteReader();
        //   if (reader.Read())
        //       return reader.ToString() == account;
        //   return false;
        //
        // as usual, we will use the simplest solution possible:
        // create account if not exists, compare password otherwise.
        // no CMS communication necessary and good enough for an Indie MMORPG.

        // not empty?
        if (!string.IsNullOrWhiteSpace(account) && !string.IsNullOrWhiteSpace(password))
        {
            // demo feature: create account if it doesn't exist yet.
            // note: sqlite-net has no InsertOrIgnore so we do it in two steps
            if (connection.FindWithQuery<accounts>("SELECT * FROM accounts WHERE name=?", account) == null)
                connection.Insert(new accounts{ name=account, password=password, created=DateTime.UtcNow, lastlogin=DateTime.Now, banned=false});

            // check account name, password, banned status
            if (connection.FindWithQuery<accounts>("SELECT * FROM accounts WHERE name=? AND password=? and banned=0", account, password) != null)
            {
                // save last login time and return true
                connection.Execute("UPDATE accounts SET lastlogin=? WHERE name=?", DateTime.UtcNow, account);
                return true;
            }
        }
        return false;
    }

    // character data //////////////////////////////////////////////////////////
    public bool CharacterExists(string characterName)
    {
        // checks deleted ones too so we don't end up with duplicates if we un-
        // delete one
        return connection.FindWithQuery<characters>("SELECT * FROM characters WHERE name=?", characterName) != null;
    }

    public void CharacterDelete(string characterName)
    {
        // soft delete the character so it can always be restored later
        connection.Execute("UPDATE characters SET deleted=1 WHERE name=?", characterName);
    }

    // returns the list of character names for that account
    // => all the other values can be read with CharacterLoad!
    public List<string> CharactersForAccount(string account)
    {
        List<string> result = new List<string>();
        foreach (characters character in connection.Query<characters>("SELECT * FROM characters WHERE account=? AND deleted=0", account))
            result.Add(character.name);
        return result;
    }

    void LoadInventory(PlayerInventory inventory)
    {
        // fill all slots first
        for (int i = 0; i < inventory.size; ++i)
            inventory.slots.Add(new ItemSlot());

        // then load valid items and put into their slots
        // (one big query is A LOT faster than querying each slot separately)
        foreach (character_inventory row in connection.Query<character_inventory>("SELECT * FROM character_inventory WHERE character=?", inventory.name))
        {
            if (row.slot < inventory.size)
            {
                if (ScriptableItem.All.TryGetValue(row.name.GetStableHashCode(), out ScriptableItem itemData))
                {
                    Item item = new Item(itemData);
                    //item.durability = Mathf.Min(row.durability, item.maxDurability);
                    item.summonedHealth = row.summonedHealth;
                    item.summonedLevel = row.summonedLevel;
                    item.summonedExperience = row.summonedExperience;
                    inventory.slots[row.slot] = new ItemSlot(item, row.amount);
                }
                else Debug.LogWarning("LoadInventory: skipped item " + row.name + " for " + inventory.name + " because it doesn't exist anymore. If it wasn't removed intentionally then make sure it's in the Resources folder.");
            }
            else Debug.LogWarning("LoadInventory: skipped slot " + row.slot + " for " + inventory.name + " because it's bigger than size " + inventory.size);
        }
    }

    void LoadEquipment(PlayerEquipment equipment)
    {
        // fill all slots first
        for (int i = 0; i < equipment.slotInfo.Length; ++i)
            equipment.slots.Add(new ItemSlot());

        // then load valid equipment and put into their slots
        // (one big query is A LOT faster than querying each slot separately)
        foreach (character_equipment row in connection.Query<character_equipment>("SELECT * FROM character_equipment WHERE character=?", equipment.name))
        {
            if (row.slot < equipment.slotInfo.Length)
            {
                if (ScriptableItem.All.TryGetValue(row.name.GetStableHashCode(), out ScriptableItem itemData))
                {
                    Item item = new Item(itemData);
                    //item.durability = Mathf.Min(row.durability, item.maxDurability);
                    item.summonedHealth = row.summonedHealth;
                    item.summonedLevel = row.summonedLevel;
                    item.summonedExperience = row.summonedExperience;
                    equipment.slots[row.slot] = new ItemSlot(item, row.amount);
                }
                else Debug.LogWarning("LoadEquipment: skipped item " + row.name + " for " + equipment.name + " because it doesn't exist anymore. If it wasn't removed intentionally then make sure it's in the Resources folder.");
            }
            else Debug.LogWarning("LoadEquipment: skipped slot " + row.slot + " for " + equipment.name + " because it's bigger than size " + equipment.slotInfo.Length);
        }
    }

    void LoadItemCooldowns(Player player)
    {
        // then load cooldowns
        // (one big query is A LOT faster than querying each slot separately)
        foreach (character_itemcooldowns row in connection.Query<character_itemcooldowns>("SELECT * FROM character_itemcooldowns WHERE character=?", player.name))
        {
            // cooldownEnd is based on NetworkTime.time which will be different
            // when restarting a server, hence why we saved it as just the
            // remaining time. so let's convert it back again.
            player.itemCooldowns.Add(row.category, row.cooldownEnd + NetworkTime.time);
        }
    }

    void LoadSkills(PlayerSkills skills)
    {
        // load skills based on skill templates (the others don't matter)
        // -> this way any skill changes in a prefab will be applied
        //    to all existing players every time (unlike item templates
        //    which are only for newly created characters)

        // fill all slots first
        foreach (ScriptableSkill skillData in skills.skillTemplates)
            skills.skills.Add(new Skill(skillData));

        // then load learned skills and put into their slots
        // (one big query is A LOT faster than querying each slot separately)
        foreach (character_skills row in connection.Query<character_skills>("SELECT * FROM character_skills WHERE character=?", skills.name))
        {
            int index = skills.GetSkillIndexByName(row.name);
            if (index != -1)
            {
                Skill skill = skills.skills[index];
                // make sure that 1 <= level <= maxlevel (in case we removed a skill
                // level etc)
                skill.level = Mathf.Clamp(row.level, 1, skill.maxLevel);
                // make sure that 1 <= level <= maxlevel (in case we removed a skill
                // level etc)
                // castTimeEnd and cooldownEnd are based on NetworkTime.time
                // which will be different when restarting a server, hence why
                // we saved them as just the remaining times. so let's convert
                // them back again.
                skill.castTimeEnd = row.castTimeEnd + NetworkTime.time;
                skill.cooldownEnd = row.cooldownEnd + NetworkTime.time;

                skills.skills[index] = skill;
            }
        }
    }

    void LoadBuffs(PlayerSkills skills)
    {
        // load buffs
        // note: no check if we have learned the skill for that buff
        //       since buffs may come from other people too
        foreach (character_buffs row in connection.Query<character_buffs>("SELECT * FROM character_buffs WHERE character=?", skills.name))
        {
            if (ScriptableSkill.All.TryGetValue(row.name.GetStableHashCode(), out ScriptableSkill skillData))
            {
                // make sure that 1 <= level <= maxlevel (in case we removed a skill
                // level etc)
                int level = Mathf.Clamp(row.level, 1, skillData.maxLevel);
                Buff buff = new Buff((BuffSkill)skillData, level);
                // buffTimeEnd is based on NetworkTime.time, which will be
                // different when restarting a server, hence why we saved
                // them as just the remaining times. so let's convert them
                // back again.
                buff.buffTimeEnd = row.buffTimeEnd + NetworkTime.time;
                skills.buffs.Add(buff);
            }
            else Debug.LogWarning("LoadBuffs: skipped buff " + row.name + " for " + skills.name + " because it doesn't exist anymore. If it wasn't removed intentionally then make sure it's in the Resources folder.");
        }
    }

    void LoadQuests(PlayerQuests quests)
    {
        // load quests
        foreach (character_quests row in connection.Query<character_quests>("SELECT * FROM character_quests WHERE character=?", quests.name))
        {
            ScriptableQuest questData;
            if (ScriptableQuest.All.TryGetValue(row.name.GetStableHashCode(), out questData))
            {
                Quest quest = new Quest(questData);
                quest.progress = row.progress;
                quest.completed = row.completed;
                quests.quests.Add(quest);
            }
            else Debug.LogWarning("LoadQuests: skipped quest " + row.name + " for " + quests.name + " because it doesn't exist anymore. If it wasn't removed intentionally then make sure it's in the Resources folder.");
        }
    }

    // only load guild when their first player logs in
    // => using NetworkManager.Awake to load all guilds.Where would work,
    //    but we would require lots of memory and it might take a long time.
    // => hooking into player loading to load guilds is a really smart solution,
    //    because we don't ever have to load guilds that aren't needed
    void LoadGuildOnDemand(PlayerGuild playerGuild)
    {
        string guildName = connection.ExecuteScalar<string>("SELECT guild FROM character_guild WHERE character=?", playerGuild.name);
        if (guildName != null)
        {
            // load guild on demand when the first player of that guild logs in
            // (= if it's not in GuildSystem.guilds yet)
            if (!GuildSystem.guilds.ContainsKey(guildName))
            {
                Guild guild = LoadGuild(guildName);
                GuildSystem.guilds[guild.name] = guild;
                playerGuild.guild = guild;
            }
            // assign from already loaded guild
            else playerGuild.guild = GuildSystem.guilds[guildName];
        }
    }

    public GameObject CharacterLoad(string characterName, List<Player> prefabs, bool isPreview)
    {
        characters row = connection.FindWithQuery<characters>("SELECT * FROM characters WHERE name=? AND deleted=0", characterName);
        if (row != null)
        {
            // instantiate based on the class name
            Player prefab = prefabs.Find(p => p.name == row.classname);
            if (prefab != null)
            {
                GameObject go = Instantiate(prefab.gameObject);
                Player player = go.GetComponent<Player>();

                player.name               = row.name;
                player.account            = row.account;
                player.className          = row.classname;
                Vector2 position          = new Vector2(row.x, row.y);
                player.level.current      = Mathf.Min(row.level, player.level.max); // limit to max level in case we changed it
                player.strength.value     = row.strength;
                player.intelligence.value = row.intelligence;
                player.experience.current = row.experience;
                player.skillExperience    = row.skillExperience;
                player.gold               = row.gold;
                player.itemMall.coins     = row.coins;

                // can the player's movement type spawn on the saved position?
                // it might not be if we changed the terrain, or if the player
                // logged out in an instanced dungeon that doesn't exist anymore
                //   * NavMesh movement need to check if on NavMesh
                //   * CharacterController movement need to check if on a Mesh
                if (player.movement.IsValidSpawnPoint(position))
                {
                    // agent.warp is recommended over transform.position and
                    // avoids all kinds of weird bugs
                    player.movement.Warp(position);
                }
                // otherwise warp to start position
                else
                {
                    Transform start = NetworkManagerMMO.GetNearestStartPosition(position);
                    player.movement.Warp(start.position);
                    // no need to show the message all the time. it would spam
                    // the server logs too much.
                    //Debug.Log(player.name + " spawn position reset because it's not on a NavMesh anymore. This can happen if the player previously logged out in an instance or if the Terrain was changed.");
                }

                LoadInventory(player.inventory);
                LoadEquipment((PlayerEquipment)player.equipment);
                LoadItemCooldowns(player);
                LoadSkills((PlayerSkills)player.skills);
                LoadBuffs((PlayerSkills)player.skills);
                LoadQuests(player.quests);
                LoadGuildOnDemand(player.guild);

                // assign health / mana after max values were fully loaded
                // (they depend on equipment, buffs, etc.)
                player.health.current = row.health;
                player.mana.current = row.mana;

                // set 'online' directly. otherwise it would only be set during
                // the next CharacterSave() call, which might take 5-10 minutes.
                // => don't set it when loading previews though. only when
                //    really joining the world (hence setOnline flag)
                if (!isPreview)
                    connection.Execute("UPDATE characters SET online=1, lastsaved=? WHERE name=?", DateTime.UtcNow, characterName);

                // addon system hooks
                onCharacterLoad.Invoke(player);

                return go;
            }
            else Debug.LogError("no prefab found for class: " + row.classname);
        }
        return null;
    }

    void SaveInventory(PlayerInventory inventory)
    {
        // inventory: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM character_inventory WHERE character=?", inventory.name);
        for (int i = 0; i < inventory.slots.Count; ++i)
        {
            ItemSlot slot = inventory.slots[i];
            if (slot.amount > 0) // only relevant items to save queries/storage/time
            {
                // note: .Insert causes a 'Constraint' exception. use Replace.
                connection.InsertOrReplace(new character_inventory{
                    character = inventory.name,
                    slot = i,
                    name = slot.item.name,
                    amount = slot.amount,
                    //durability = slot.item.durability,
                    summonedHealth = slot.item.summonedHealth,
                    summonedLevel = slot.item.summonedLevel,
                    summonedExperience = slot.item.summonedExperience
                });
            }
        }
    }

    void SaveEquipment(PlayerEquipment equipment)
    {
        // equipment: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM character_equipment WHERE character=?", equipment.name);
        for (int i = 0; i < equipment.slots.Count; ++i)
        {
            ItemSlot slot = equipment.slots[i];
            if (slot.amount > 0) // only relevant equip to save queries/storage/time
            {
                connection.InsertOrReplace(new character_equipment{
                    character = equipment.name,
                    slot = i,
                    name = slot.item.name,
                    amount = slot.amount,
                    //durability = slot.item.durability,
                    summonedHealth = slot.item.summonedHealth,
                    summonedLevel = slot.item.summonedLevel,
                    summonedExperience = slot.item.summonedExperience
                });
            }
        }
    }

    void SaveItemCooldowns(Player player)
    {
        // equipment: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM character_itemcooldowns WHERE character=?", player.name);
        foreach (KeyValuePair<string, double> kvp in player.itemCooldowns)
        {
            // cooldownEnd is based on NetworkTime.time, which will be different
            // when restarting the server, so let's convert it to the remaining
            // time for easier save & load
            // note: this does NOT work when trying to save character data
            //       shortly before closing the editor or game because
            //       NetworkTime.time is 0 then.
            float cooldown = player.GetItemCooldown(kvp.Key);
            if (cooldown > 0)
            {
                connection.InsertOrReplace(new character_itemcooldowns{
                    character = player.name,
                    category = kvp.Key,
                    cooldownEnd = cooldown
                });
            }
        }
    }

    void SaveSkills(PlayerSkills skills)
    {
        // skills: remove old entries first, then add all new ones
        connection.Execute("DELETE FROM character_skills WHERE character=?", skills.name);
        foreach (Skill skill in skills.skills)
            if (skill.level > 0) // only learned skills to save queries/storage/time
            {
                // castTimeEnd and cooldownEnd are based on NetworkTime.time,
                // which will be different when restarting the server, so let's
                // convert them to the remaining time for easier save & load
                // note: this does NOT work when trying to save character data
                //       shortly before closing the editor or game because
                //       NetworkTime.time is 0 then.
                connection.InsertOrReplace(new character_skills{
                    character = skills.name,
                    name = skill.name,
                    level = skill.level,
                    castTimeEnd = skill.CastTimeRemaining(),
                    cooldownEnd = skill.CooldownRemaining()
                });
            }
    }

    void SaveBuffs(PlayerSkills skills)
    {
        // buffs: remove old entries first, then add all new ones
        connection.Execute("DELETE FROM character_buffs WHERE character=?", skills.name);
        foreach (Buff buff in skills.buffs)
        {
            // buffTimeEnd is based on NetworkTime.time, which will be different
            // when restarting the server, so let's convert them to the
            // remaining time for easier save & load
            // note: this does NOT work when trying to save character data
            //       shortly before closing the editor or game because
            //       NetworkTime.time is 0 then.
            connection.InsertOrReplace(new character_buffs{
                character = skills.name,
                name = buff.name,
                level = buff.level,
                buffTimeEnd = buff.BuffTimeRemaining()
            });
        }
    }

    void SaveQuests(PlayerQuests quests)
    {
        // quests: remove old entries first, then add all new ones
        connection.Execute("DELETE FROM character_quests WHERE character=?", quests.name);
        foreach (Quest quest in quests.quests)
        {
            connection.InsertOrReplace(new character_quests{
                character = quests.name,
                name = quest.name,
                progress = quest.progress,
                completed = quest.completed
            });
        }
    }

    // adds or overwrites character data in the database
    public void CharacterSave(Player player, bool online, bool useTransaction = true)
    {
        // only use a transaction if not called within SaveMany transaction
        if (useTransaction) connection.BeginTransaction();

        connection.InsertOrReplace(new characters{
            name = player.name,
            account = player.account,
            classname = player.className,
            x = player.transform.position.x,
            y = player.transform.position.y,
            level = player.level.current,
            health = player.health.current,
            mana = player.mana.current,
            strength = player.strength.value,
            intelligence = player.intelligence.value,
            experience = player.experience.current,
            skillExperience = player.skillExperience,
            gold = player.gold,
            coins = player.itemMall.coins,
            online = online,
            lastsaved = DateTime.UtcNow
        });

        SaveInventory(player.inventory);
        SaveEquipment((PlayerEquipment)player.equipment);
        SaveItemCooldowns(player);
        SaveSkills((PlayerSkills)player.skills);
        SaveBuffs((PlayerSkills)player.skills);
        SaveQuests(player.quests);
        if (player.guild.InGuild())
            SaveGuild(player.guild.guild, false); // TODO only if needs saving? but would be complicated

        // addon system hooks
        onCharacterSave.Invoke(player);

        if (useTransaction) connection.Commit();
    }

    // save multiple characters at once (useful for ultra fast transactions)
    public void CharacterSaveMany(IEnumerable<Player> players, bool online = true)
    {
        connection.BeginTransaction(); // transaction for performance
        foreach (Player player in players)
            CharacterSave(player, online, false);
        connection.Commit(); // end transaction
    }

    // guilds //////////////////////////////////////////////////////////////////
    public bool GuildExists(string guild)
    {
        return connection.FindWithQuery<guild_info>("SELECT * FROM guild_info WHERE name=?", guild) != null;
    }

    Guild LoadGuild(string guildName)
    {
        Guild guild = new Guild();

        // set name
        guild.name = guildName;

        // load guild info
        guild_info info = connection.FindWithQuery<guild_info>("SELECT * FROM guild_info WHERE name=?", guildName);
        if (info != null)
        {
            guild.notice = info.notice;
        }

        // load members list
        List<character_guild> rows = connection.Query<character_guild>("SELECT * FROM character_guild WHERE guild=?", guildName);
        GuildMember[] members = new GuildMember[rows.Count]; // avoid .ToList(). use array directly.
        for (int i = 0; i < rows.Count; ++i)
        {
            character_guild row = rows[i];

            GuildMember member = new GuildMember();
            member.name = row.character;
            member.rank = (GuildRank)row.rank;

            // is this player online right now? then use runtime data
            if (Player.onlinePlayers.TryGetValue(member.name, out Player player))
            {
                member.online = true;
                member.level = player.level.current;
            }
            else
            {
                member.online = false;
                // note: FindWithQuery<characters> is easier than ExecuteScalar<int> because we need the null check
                characters character = connection.FindWithQuery<characters>("SELECT * FROM characters WHERE name=?", member.name);
                member.level = character != null ? character.level : 1;
            }

            members[i] = member;
        }
        guild.members = members;
        return guild;
    }

    public void SaveGuild(Guild guild, bool useTransaction = true)
    {
        if (useTransaction) connection.BeginTransaction(); // transaction for performance

        // guild info
        connection.InsertOrReplace(new guild_info{
            name = guild.name,
            notice = guild.notice
        });

        // members list
        connection.Execute("DELETE FROM character_guild WHERE guild=?", guild.name);
        foreach (GuildMember member in guild.members)
        {
            connection.InsertOrReplace(new character_guild{
                character = member.name,
                guild = guild.name,
                rank = (int)member.rank
            });
        }

        if (useTransaction) connection.Commit(); // end transaction
    }

    public void RemoveGuild(string guild)
    {
        connection.BeginTransaction(); // transaction for performance
        connection.Execute("DELETE FROM guild_info WHERE name=?", guild);
        connection.Execute("DELETE FROM character_guild WHERE guild=?", guild);
        connection.Commit(); // end transaction
    }

    // item mall ///////////////////////////////////////////////////////////////
    public List<long> GrabCharacterOrders(string characterName)
    {
        // grab new orders from the database and delete them immediately
        //
        // note: this requires an orderid if we want someone else to write to
        // the database too. otherwise deleting would delete all the new ones or
        // updating would update all the new ones. especially in sqlite.
        //
        // note: we could just delete processed orders, but keeping them in the
        // database is easier for debugging / support.
        List<long> result = new List<long>();
        List<character_orders> rows = connection.Query<character_orders>("SELECT * FROM character_orders WHERE character=? AND processed=0", characterName);
        foreach (character_orders row in rows)
        {
            result.Add(row.coins);
            connection.Execute("UPDATE character_orders SET processed=1 WHERE orderid=?", row.orderid);
        }
        return result;
    }
}