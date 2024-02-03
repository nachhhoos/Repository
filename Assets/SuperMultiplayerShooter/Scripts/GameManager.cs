using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon;

namespace Visyde
{
    /// <summary>
    /// Game Manager
    /// - Simply the game manager. The one that controls the game itself. Provides game settings and serves as the central component by
    /// connecting other components to communicate with each other.
    /// </summary>

    public class GameManager : PunBehaviour
    {
        public static GameManager instance;

        public string playerPrefab;                     // Name of player prefab. The prefab must be in a "Resources" folder.

        [Space]
        public GameMap[] maps;
        [HideInInspector] public int chosenMap = -1;

        [Space]
        [Header("Game Settings:")]
        public bool useMobileControls;              	// if set to true, joysticks and on-screen buttons will be enabled
        public int respawnTime = 5;             		// delay before respawning after death
        public float invulnerabilityDuration = 3;		// how long players stay invulnerable after spawn
        public int preparationTime = 3;                 // the starting countdown before the game starts
        public int gameLength = 120;                    // time in seconds
        public bool showEnemyHealth = false;
        public bool damagePopups = true;
        [System.Serializable]
        public class KillData
        {
            public bool notify = true;
            public string message;
            public int bonusScore;
        }
        public KillData[] multiKillMessages;
        public float multikillDuration = 3;             // multikill reset delay
        public bool allowHurtingSelf;                   // allow grenades and projectiles to hurt their owner
        public bool deadZone;                           // kill players when below the dead zone line
        public float deadZoneOffset;                    // Y position of the dead zone line
        public PowerUp[] powerUps;

        [Space]
        [Header("Others:")]
        public bool doCamShakesOnDamage;            	// allow cam shakes when taking damage
        public float camShakeAmount = 0.3f;
        public float camShakeDuration = 0.1f;

        [Space]
        [Header("References:")]
        public ItemSpawner itemSpawner;
        public ControlsManager controlsManager;
        public UIManager ui;
        public CameraController gameCam;

        // if the starting countdown has already begun:
        public bool countdownStarted
        {
            get { return startingCountdownStarted; }
        }
        // the progress of the starting countdown:
        public float countdown
        {
            get { return (float)(gameStartsIn - PhotonNetwork.time); }
        }
        // the time elapsed after the starting countdown:
        public float timeElapsed
        {
            get { return (float)elapsedTime; }
        }
        // the remaining time before the game ends:
        public int remainingGameTime
        {
            get { return (int)remainingTime; }
        }

        [HideInInspector] public bool gameStarted = false;                                                  // if the game has started already
        [HideInInspector] public string[] playerRankings = new string[0];       				            // used at the end of the game
        [HideInInspector] public PhotonPlayer[] bots = new PhotonPlayer[0];
        [HideInInspector] public bool isGameOver;                                                           // is the game over?
        [HideInInspector] public PlayerController ourPlayer;                                				// our player's player (the player object itself)
        [HideInInspector] public List<PlayerController> playerControllers = new List<PlayerController>();	// list of all player controllers currently in the scene
        [HideInInspector] public bool dead;
        [HideInInspector] public float curRespawnTime;
        public static bool isDraw;                                                          				// is game draw?
        bool hasBots;                                                                       				// does the game have bots?

        // Local copy of bot stats (so we don't periodically have to download them when not needed):
        string[] bn = new string[0];		// Bot names
        Vector3[] bs = new Vector3[0];		// Bot scores (x = kills, y = deaths, z = other scores)
        int[] bc = new int[0];				// Bot's chosen character's index

        // Used for time syncronization:
        [HideInInspector] public double startTime, elapsedTime, remainingTime, gameStartsIn;
        bool startRoundWhenTimeIsSynced, startingCountdownStarted, doneGameStart;

        // For respawning:
        double deathTime;

        void Awake()
        {
            instance = this;

            // Do we have bots in the game? Download bot stats if we have:
            hasBots = PhotonNetwork.room.CustomProperties.ContainsKey("botNames");
            if (hasBots)
            {
                // Download the stats:
                bn = (string[])PhotonNetwork.room.CustomProperties["botNames"];
                bs = (Vector3[])PhotonNetwork.room.CustomProperties["botScores"];
                bc = (int[])PhotonNetwork.room.CustomProperties["botCharacters"];
                // ...then generate the photon players:
                bots = GenerateBotPhotonPlayers();
            }

            // Don't allow the device to sleep while in game:
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        // Use this for initialization
        void Start()
        {
            // Setups:
            isDraw = false;
            gameCam.gm = this;

            // Determine the type of controls:
            controlsManager.mobileControls = useMobileControls;

            // Get the chosen map then enable it:
            chosenMap = (int)PhotonNetwork.room.CustomProperties["map"];
            for (int i = 0; i < maps.Length; i++)
            {
                maps[i].gameObject.SetActive(chosenMap == i);
            }

            // After loading the scene, we (the local player) are now ready for the game:
            Ready();

            // Start checking if all players are ready:
            InvokeRepeating("CheckIfAllPlayersReady", 1, 0.5f);

            // Also, start checking if there are still players in game:
            InvokeRepeating("CheckPlayersLeft", 2, 0.5f);

            // Start getting all player controllers in the scene:
            InvokeRepeating("ForceUpdatePlayerControllerList", preparationTime, 5);
        }

        void CheckIfAllPlayersReady()
        {
            if (!isGameOver)
            {
                // Check if players are ready:
                if (!startingCountdownStarted)
                {
                    bool allPlayersReady = true;
                    PhotonPlayer[] players = PhotonNetwork.playerList;
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (players[i].GetScore() == -1)
                        {
                            allPlayersReady = false;
                        }
                    }
                    // Start the preparation countdown when all players are done loading:
                    if (allPlayersReady && PhotonNetwork.isMasterClient)
                    {
                        StartGamePrepare();
                    }
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (!isGameOver)
            {
                // Start the game when preparation countdown is finished:
                if (startingCountdownStarted)
                {
                    if (elapsedTime >= (gameStartsIn - startTime) && !gameStarted && !doneGameStart)
                    {
                        // GAME HAS STARTED!
                        if (PhotonNetwork.isMasterClient)
                        {
                            doneGameStart = true;
                            ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
                            h["started"] = true;
                            PhotonNetwork.room.SetCustomProperties(h);
                            StartGameTimer();
                        }

                        CancelInvoke("CheckIfAllPlayersReady");
                    }
                }

                // Check game status:
                if (!gameStarted && playerRankings.Length == 0)
                {
                    gameStarted = (bool)PhotonNetwork.room.CustomProperties["started"];
                }

                // Game timer:
                if (startRoundWhenTimeIsSynced)
                {
                    StartGameTimer();
                }

                // Respawning:
                if (dead)
                {
                    if (deathTime == 0)
                    {
                        deathTime = PhotonNetwork.time + respawnTime;
                    }
                    curRespawnTime = (float)(deathTime - PhotonNetwork.time);
                    if (curRespawnTime <= 0)
                    {
                        dead = false;
                        deathTime = 0;
                        Spawn();
                    }
                }

                // Calculating the elapsed and remaining time:
                elapsedTime = (PhotonNetwork.time - startTime);
                remainingTime = gameLength - (elapsedTime % gameLength);

                // Finish game when elapsed time is greater than or equal to game length:
                if (elapsedTime + 1 >= gameLength && gameStarted && !isGameOver)
                {
                    // Post the player rankings:
                    if (PhotonNetwork.isMasterClient)
                    {
                        // Get player list by order based on scores and also set "draw" to true (the player sorter will set this to false if not draw):
                        isDraw = true;

                        // List of player names for the rankings:
                        PhotonPlayer[] ps = SortPlayersByScore();
                        string[] p = new string[ps.Length];
                        for (int i = 0; i < ps.Length; i++)
                        {
                            p[i] = ps[i].NickName;
                        }

                        isDraw = ps.Length > 1 && isDraw;

                        // Mark as game over:
                        ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
                        h.Add("rankings", p);
                        h.Add("draw", isDraw);
                        PhotonNetwork.room.SetCustomProperties(h);
                    }
                }

                // Check if game is over:
                if (playerRankings.Length > 0){
                    isGameOver = true;
                }
            }
        }

        // Called when we enter the game world:
        void Ready()
        {
            // Set our score to 0 on start (this is not the player's actual score, this is only used to determine if we're ready or not, 0 = ready, -1 = not):
            PhotonNetwork.player.SetScore(0);

            // Spawn our player:
            Spawn();

            // ... and the bots if we have some and if we are the master client:
            if (hasBots && PhotonNetwork.isMasterClient)
            {
                for (int i = 0; i < bots.Length; i++)
                {
                    SpawnBot(i);
                }
            }
        }

        /// <summary>
        /// Spawns the player.
        /// </summary>
        public void Spawn()
        {
            Transform spawnPoint = maps[chosenMap].playerSpawnPoints[UnityEngine.Random.Range(0, maps[chosenMap].playerSpawnPoints.Count)];
            ourPlayer = PhotonNetwork.Instantiate(playerPrefab, spawnPoint.position, Quaternion.identity, 0).GetComponent<PlayerController>();
        }

        /// <summary>
        /// Spawns a bot (only works on master client).
        /// </summary
        public void SpawnBot(int bot)
        {
            if (!PhotonNetwork.isMasterClient) return;

            Transform spawnPoint = maps[chosenMap].playerSpawnPoints[UnityEngine.Random.Range(0, maps[chosenMap].playerSpawnPoints.Count)];
            PlayerController botP = PhotonNetwork.InstantiateSceneObject(playerPrefab, spawnPoint.position, Quaternion.identity, 0, new object[] { bot }).GetComponent<PlayerController>();
        }

        [PunRPC]
        public void SomeoneDied(string dying, string killer)
        {
            ui.SomeoneKilledSomeone(dying, killer);
        }

        /// <summary>
        /// Returns the PlayerController of the player with the given name.
        /// </summary>
        public PlayerController GetPlayerControllerOfPlayer(string name)
        {
            for (int i = 0; i < playerControllers.Count; i++)
            {

                // Check if current item matches a player:
                if (string.CompareOrdinal(playerControllers[i].GetOwner().NickName, name) == 0)
                {
                    return playerControllers[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Checks how many players are still in the game. If there's only 1 left, the game will end.
        /// </summary>
        public void CheckPlayersLeft()
        {
            if (GetPlayerList().Length <= 1 && PhotonNetwork.room.MaxPlayers > 1)
            {
                ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
                double skip = 0;
                h["gameStartTime"] = skip;
                PhotonNetwork.room.SetCustomProperties(h);
            }
        }

        // Player leaderboard sorting:
        IComparer SortPlayers()
        {
            return (IComparer)new PlayerSorter();
        }
        public PhotonPlayer[] SortPlayersByScore()
        {

            // Get the full player list:
            PhotonPlayer[] players = GetPlayerList();

            // ...then sort them out based on scores:
            System.Array.Sort(players, SortPlayers());
            return players;
        }

        // Get player list (humans + bots):
        public PhotonPlayer[] GetPlayerList()
        {
            // Get the (human) player list:
            PhotonPlayer[] players = PhotonNetwork.playerList;

            // If we have bots, include them to the player list:
            if (hasBots)
            {
                // Merge the human list and bot list into one array:
                PhotonPlayer[] p = new PhotonPlayer[players.Length + bots.Length];
                players.CopyTo(p, 0);
                bots.CopyTo(p, players.Length);

                // ...then replace the human player list with the full player list array:
                players = p;
            }

            return players;
        }

        // Generates a PhotonPlayer array for bots: 
        PhotonPlayer[] GenerateBotPhotonPlayers()
        {
            PhotonPlayer[] p = bots;

            if (hasBots)
            {

                // If it's the first time generating, photon players should be created first: 
                if (bots.Length == 0)
                {
                    p = new PhotonPlayer[bn.Length];
                    for (int i = 0; i < p.Length; i++)
                    {
                        // Create this bot's photon player:
                        p[i] = new PhotonPlayer(false, 0, bn[i]);

                        // Set the scores:
                        ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
                        h.Add("kills", Mathf.RoundToInt(bs[i].x));
                        h.Add("deaths", Mathf.RoundToInt(bs[i].y));
                        h.Add("otherScore", Mathf.RoundToInt(bs[i].z));

                        // Add an empty key named "bot" just so we know it's a bot:
                        h.Add("bot", 0);

                        // Set the chosen character:
                        h.Add("character", bc[i]);

                        // Apply the hashtable to the photon player (we can just set it directly since we are only using this photon player locally):
                        p[i].CustomProperties = h;
                    }
                }
                // ...otherwise, we can just set the custom properties directly:
                else
                {
                    for (int i = 0; i < p.Length; i++)
                    {
                        p[i].CustomProperties["kills"] = Mathf.RoundToInt(bs[i].x);
                        p[i].CustomProperties["deaths"] = Mathf.RoundToInt(bs[i].y);
                        p[i].CustomProperties["otherScore"] = Mathf.RoundToInt(bs[i].z);
                    }
                }
            }
            return p;
        }

        // Forcibly updates the player controller list by finding all player controllers in the scene
        // (Do not call this inside the Update() since this can be very expensive):
        void ForceUpdatePlayerControllerList()
        {
            PlayerController[] pc = GetComponents<PlayerController>();
            if (pc.Length > 0)
            {
                playerControllers = new List<PlayerController>();
                for (int i = 0; i < pc.Length; i++)
                {
                    playerControllers.Add(pc[i]);
                }
            }
        }

        // Upload an updated bot score list to the room properties:
        public void UpdateBotStats()
        {
            ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();

            // Get each bot's scores and store them as a Vector3:
            bs = new Vector3[bots.Length];
            for (int i = 0; i < bots.Length; i++)
            {
                bs[i] = new Vector3((int)bots[i].CustomProperties["kills"], (int)bots[i].CustomProperties["deaths"], (int)bots[i].CustomProperties["otherScore"]);
            }

            h.Add("botScores", bs);
            PhotonNetwork.room.SetCustomProperties(h);
        }


        // Others:
        public void Explode(Vector2 position, float radius, int damage)
        {
            // Damaging:
            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, radius);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].CompareTag("Player"))
                {
                    PlayerController p = cols[i].GetComponent<PlayerController>();
                    if (((p.photonView.owner == photonView.owner && GameManager.instance.allowHurtingSelf) || p.photonView.owner != photonView.owner) && !p.invulnerable)
                    {
                        if (!p.isDead)
                        {
                            RaycastHit2D[] hits = Physics2D.RaycastAll(position, new Vector2(cols[i].transform.position.x, cols[i].transform.position.y) - position, radius);
                            RaycastHit2D hit = new RaycastHit2D();
                            for (int h = 0; h < hits.Length; h++)
                            {
                                if (hits[h].collider.gameObject == cols[i].gameObject)
                                {
                                    hit = hits[h];
                                    // Calculate the damage based on distance:
                                    int finalDamage = Mathf.RoundToInt(damage * (1 - ((transform.position - new Vector3(hit.point.x, hit.point.y)).magnitude / radius)));
                                    // Apply damage:
                                    p.photonView.RPC("Hurt", PhotonTargets.AllBuffered, PhotonNetwork.player.NickName, finalDamage, false);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        public void DoEmote(int id){
            if (ourPlayer && !ourPlayer.isDead){
                ourPlayer.photonView.RPC("Emote", PhotonTargets.All, id);
            }
        }

        // Calling this will make us disconnect from the current game/room:
        public void QuitMatch()
        {
            PhotonNetwork.LeaveRoom();
        }

#region Timer Sync (Do not touch!)
        void StartGameTimer()
        {
            if (PhotonNetwork.time < 0.0001f)
            {
                startRoundWhenTimeIsSynced = true;
                return;
            }
            startRoundWhenTimeIsSynced = false;

            ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
            h["gameStartTime"] = PhotonNetwork.time;
            PhotonNetwork.room.SetCustomProperties(h);
        }
        void StartGamePrepare()
        {
            ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
            h["gameStartsIn"] = PhotonNetwork.time + preparationTime;
            PhotonNetwork.room.SetCustomProperties(h);

        }
#endregion

#region Photon calls
        void OnLeftRoom()
        {
            DataCarrier.message = "";
            DataCarrier.LoadScene("MainMenu");
        }
        void OnPhotonCustomRoomPropertiesChanged(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            if (propertiesThatChanged.ContainsKey("gameStartsIn"))
            {
                gameStartsIn = (double)propertiesThatChanged["gameStartsIn"];
                startingCountdownStarted = true;
            }
            // Game timer:
            if (propertiesThatChanged.ContainsKey("gameStartTime"))
            {
                startTime = (double)propertiesThatChanged["gameStartTime"];
            }
            // Check if game is over:
            if (propertiesThatChanged.ContainsKey("rankings"))
            {
                playerRankings = (string[])propertiesThatChanged["rankings"];
                isDraw = (bool)propertiesThatChanged["draw"];
            }

            // Update our copy of bot stats if the online version changed:
            if (propertiesThatChanged.ContainsKey("botNames"))
            {
                bn = (string[])PhotonNetwork.room.CustomProperties["botNames"];
                bots = GenerateBotPhotonPlayers();
            }
            if (propertiesThatChanged.ContainsKey("botScores"))
            {
                bs = (Vector3[])PhotonNetwork.room.CustomProperties["botScores"];
                bots = GenerateBotPhotonPlayers();
            }
            if (propertiesThatChanged.ContainsKey("botCharacters"))
            {
                bc = (int[])PhotonNetwork.room.CustomProperties["botCharacters"];
                bots = GenerateBotPhotonPlayers();
            }
        }
        void OnMasterClientSwitched(PhotonPlayer newMasterClient)
        {
            // Game timer:
            if (!PhotonNetwork.room.CustomProperties.ContainsKey("gameStartTime"))
            {
                StartGameTimer();
            }
        }
        void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer)
        {
            // Display a message when someone disconnects/left the game/room:
            ui.DisplayMessage(otherPlayer.NickName + " left the match", UIManager.MessageType.LeftTheGame);

            // Refresh bot list (only if we have bots):
            if (hasBots)
            {
                bots = GenerateBotPhotonPlayers();
            }

            // Other refreshes:
            ui.UpdateBoards();
            CheckPlayersLeft();
        }
        void OnDisconnectedFromPhoton()
        {
            DataCarrier.message = "You've been disconnected from the game!";
            DataCarrier.LoadScene("MainMenu");
        }
#endregion

        void OnDrawGizmos()
        {
            // Dead zone:
            if (deadZone)
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Gizmos.DrawCube(new Vector3(0, deadZoneOffset - 50, 0), new Vector3(1000, 100, 0));
            }
        }
    }

    // Player sorter helper:
    public class PlayerSorter : IComparer
    {
        int IComparer.Compare(object a, object b)
        {
            int p1 = ((int)((PhotonPlayer)a).CustomProperties["kills"] - (int)((PhotonPlayer)a).CustomProperties["deaths"]) + (int)((PhotonPlayer)a).CustomProperties["otherScore"];
            int p2 = ((int)((PhotonPlayer)b).CustomProperties["kills"] - (int)((PhotonPlayer)b).CustomProperties["deaths"]) + (int)((PhotonPlayer)b).CustomProperties["otherScore"];
            //int p1 = (int)((PhotonPlayer)a).CustomProperties["kills"];
            //int p2 = (int)((PhotonPlayer)b).CustomProperties["kills"];
            if (p1 == p2)
            {
                return 0;
            }
            else
            {
                GameManager.isDraw = false;  // Game isn't draw:

                if (p1 > p2)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }
        }
    }
}