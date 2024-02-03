using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Visyde
{
    /// <summary>
    /// Connector
    /// - manages the initial connection and matchmaking
    /// </summary>

    public class Connector : MonoBehaviour
    {
        public static Connector instance;

        [Header("Settings:")]
        public string gameVersion = "0.1";
        public string gameSceneName = "";
        public int requiredPlayers;
        public string[] maps;

        [Header("Bot Players:")]
        [Tooltip("This is only for match making.")] public bool createBots;
        public float startCreatingBotsAfter;		// (Only if `createBots` is enabled) after this delay, the game will start on creating bots to fill up the room.
        public float minBotCreationTime;			// minimum bot join/creation delay
        public float maxBotCreationTime;			// maximum bot join/creation delay
        public string[] botPrefixes;                // names for bots

        [Header("Other References:")]
        public CharacterSelector characterSelector;

        bool inCustom;
        public bool isInCustomGame{
            get{
                return inCustom && PhotonNetwork.inRoom;
            }
        }

        // Internal variables:
        [HideInInspector] public int selectedMap;
        [HideInInspector] public int totalPlayerCount;
        bool doneConnect;
        float curConnectDelay;
        Bot[] curBots;
        int bnp;

        class Bot
        {
            public string name;				// bot name
            public Vector3 scores; 			// x = kills, y = deaths, z = other scores
            public int characterUsing;		// the chosen character of the bot (index only)
        }
        bool loadNow;                       // if true, the game scene will be loaded. Matchmaking will set this to true instantly when enough 
                                            // players are present, custom games on the other hand will require the host to press the "Start" button first.

        void Start()
        {
            instance = this;
            PhotonNetwork.automaticallySyncScene = true;
            loadNow = false;
        }

        // Update is called once per frame
        void Update()
        {
            // Connecting to server;
            if (!PhotonNetwork.connected && !PhotonNetwork.connecting)
            {
                if (doneConnect)
                {
                    doneConnect = false;
                    curConnectDelay = 0;
                }
            }

            // Reconnect:
            if (!doneConnect && !PhotonNetwork.connected)
            {
                if (curConnectDelay < 2)
                {
                    curConnectDelay += Time.deltaTime;
                }
                else
                {
                    doneConnect = true;
                    PhotonNetwork.ConnectUsingSettings(gameVersion);
                }
            }

            // Room managing:
            if (PhotonNetwork.inRoom)
            {
                // Set the variable "loadNow" to true if the room is already full (matchmaking):
                if (totalPlayerCount >= PhotonNetwork.room.MaxPlayers && !isInCustomGame)
                {
                    loadNow = true;
                }

                // Go to the game scene if the variable "loadNow" is true:
                if (loadNow){
                    if (PhotonNetwork.isMasterClient)
                    {
                        PhotonNetwork.room.IsOpen = false;
                        PhotonNetwork.LoadLevel(gameSceneName);
                    }
                }
            }
        }

        // Matchmaking:
        public void FindMatch()
        {
            ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
            h.Add("isInMatchmaking", true);
            PhotonNetwork.JoinRandomRoom(h, 0);
        }
        public void CancelMatchmaking()
        {
            PhotonNetwork.LeaveRoom();
            // Clear bots list:
            curBots = new Bot[0];
        }

        // Custom Game:
        public void CreateCustomGame(int selectedMap, int maxPlayers, bool allowBots)
        {
            ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
            h.Add("started", false);
            h.Add("map", selectedMap);
            h.Add("customAllowBots", allowBots);
            h.Add("isInMatchmaking", false);
            PhotonNetwork.CreateRoom(PhotonNetwork.playerName, new RoomOptions() { MaxPlayers = (byte)maxPlayers, IsVisible = true,
                        CleanupCacheOnLeave = false, CustomRoomProperties = h, CustomRoomPropertiesForLobby = new string[] {"started", "map", "customAllowBots", "isInMatchmaking"} }, null );
        }
        public void StartGame(){
            // Start if in custom game:
            if (inCustom)
            {
                // Create the bots if allowed:
                if ((bool)PhotonNetwork.room.CustomProperties["customAllowBots"])
                {
                    // Clear the bots array first:
                    curBots = new Bot[0];
                    // Generate a number to be attached to the bot names:
                    bnp = Random.Range(0, 9999);
                    int numCreatedBots = 0;
                    int max = PhotonNetwork.room.MaxPlayers - totalPlayerCount;
                    while (numCreatedBots < max)
                    {
                        CreateABot();
                        numCreatedBots++;
                    }

                    loadNow = true;
                }
                else
                {
                    loadNow = true;
                }
            }
        }

        // Bot Creation:
        void StartCreatingBots()
        {
            // Generate a number to be attached to the bot names:
            bnp = Random.Range(0, 9999);
            Invoke("CreateABot", Random.Range(minBotCreationTime, maxBotCreationTime));
        }
        void CreateABot()
        {
            if (PhotonNetwork.inRoom)
            {
                // Add a new bot to the bots array:
                Bot[] b = new Bot[curBots.Length + 1];
                for (int i = 0; i < curBots.Length; i++)
                {
                    b[i] = curBots[i];
                }
                b[b.Length - 1] = new Bot();

                // Setup the new bot (set the name and the character chosen):
                b[b.Length - 1].name = botPrefixes[Random.Range(0, botPrefixes.Length)] + bnp;
                bnp += 1;   // make next bot name unique
                b[b.Length - 1].characterUsing = Random.Range(0, characterSelector.characters.Length);

                // Now replace the old bot array with the new one:
                curBots = b;

                // ... and upload the new bot array to the room properties:
                ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();

                string[] bn = new string[b.Length];
                Vector3[] bs = new Vector3[b.Length];
                int[] bc = new int[b.Length];
                for (int i = 0; i < b.Length; i++)
                {
                    bn[i] = b[i].name;
                    bs[i] = b[i].scores;
                    bc[i] = b[i].characterUsing;
                }
                bn[bn.Length - 1] = b[b.Length - 1].name;
                bs[bs.Length - 1] = b[b.Length - 1].scores;
                bc[bc.Length - 1] = b[b.Length - 1].characterUsing;

                h.Add("botNames", bn);
                h.Add("botScores", bs);
                h.Add("botCharacters", bc);
                PhotonNetwork.room.SetCustomProperties(h);

                // Continue adding another bot after a random delay (to give human players enough time to join, and also to simulate realism):
                Invoke("CreateABot", Random.Range(minBotCreationTime, maxBotCreationTime));

                print("New bot created!");
            }
        }

        Bot[] GetBotList()
        {
            Bot[] list = new Bot[0];

            // Download the bots list if we already have one:
            if (PhotonNetwork.room.CustomProperties.ContainsKey("botNames"))
            {
                string[] bn = (string[])PhotonNetwork.room.CustomProperties["botNames"];
                Vector3[] bs = (Vector3[])PhotonNetwork.room.CustomProperties["botScores"];
                int[] bc = (int[])PhotonNetwork.room.CustomProperties["botCharacters"];

                list = new Bot[bn.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    list[i] = new Bot();
                    list[i].name = bn[i];
                    list[i].scores = bs[i];
                    list[i].characterUsing = bc[i];
                }
            }
            return list;
        }


        void UpdatePlayerCount()
        {
            // Get the "Real" player count:
            int players = PhotonNetwork.room.PlayerCount;

            // ...then check if there are bots in the room:
            if (PhotonNetwork.room.CustomProperties.ContainsKey("botNames"))
            {
                // If there are, set the bots list from the server:
                if (!PhotonNetwork.isMasterClient) curBots = GetBotList();

                // ... and get the number of bots and add it to the total player count:
                players += curBots.Length;
            }

            // Set the total player count:
            totalPlayerCount = players;
        }

        // PHOTON:
        void OnConnectedToMaster()
        {
            PhotonNetwork.JoinLobby();
        }
        void OnFailedToConnectToPhoton(DisconnectCause cause)
        {

        }

        void OnPhotonPlayerConnected(PhotonPlayer player)
        {
            // When a player connects, update the player count:
            UpdatePlayerCount();
        }
        void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer)
        {
            // When a player disconnects, update the player count:
            UpdatePlayerCount();

            // Also, if a player disconnects while matchmaking and they happen to be the master client, a new master client will be assigned.
            // We could be the new master client so check if we are. If we are, continue adding bots (if bots are allowed):
            if (PhotonNetwork.isMasterClient && createBots)
            {
                // Get the existing bot list made by the last master client, or make a new one if none:
                curBots = GetBotList();
                // Start creating bots after a delay:
                Invoke("StartCreatingBots", curBots.Length > 0 ? Random.Range(minBotCreationTime, maxBotCreationTime) : startCreatingBotsAfter);
            }
        }

        void OnPhotonCustomRoomPropertiesChanged(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            // A bot might have joined, so update the total player count:
            UpdatePlayerCount();
        }
        void OnPhotonRandomJoinFailed()
        {
            if (PhotonNetwork.connectedAndReady)
            {
                // Prepare the room properties:
                ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();
                h.Add("started", false);
                h.Add("map", Random.Range(0, maps.Length));
                h.Add("isInMatchmaking", true);

                // Then create the room, with the prepared room properties in the RoomOptions argument:
                PhotonNetwork.CreateRoom(null , new RoomOptions() { MaxPlayers = (byte)requiredPlayers, CleanupCacheOnLeave = false, 
                                    IsVisible = true, CustomRoomProperties = h, CustomRoomPropertiesForLobby = new string[] { "isInMatchmaking" }}, null);
            }
        }
        void OnJoinRoomFailed()
        {
            PhotonNetwork.JoinRandomRoom();
        }
        void OnJoinedRoom()
        {
            // Know if the room we joined in is a custom game or not:
            inCustom = !(bool)PhotonNetwork.room.CustomProperties["isInMatchmaking"];

            // This is only used to check if we've loaded the game and ready. This sets to 0 after loading the game scene:
            PhotonNetwork.player.SetScore(-1); // -1 = not ready, 0 = ready

            // Setup scores (these are the actual player scores):
            ExitGames.Client.Photon.Hashtable p = new ExitGames.Client.Photon.Hashtable();
            p.Add("kills", 0);
            p.Add("deaths", 0);
            p.Add("otherScore", 0);
            // ... and also the chosen character:
            p.Add("character", DataCarrier.chosenCharacter);
            PhotonNetwork.player.SetCustomProperties(p);

            // Let's update the total player count (for local reference):
            UpdatePlayerCount();

            // Start creating bots (if bots are allowed and if we are NOT in a CUSTOM game):
            if (PhotonNetwork.isMasterClient && createBots && !isInCustomGame)
            {
                // Clear the bots array first:
                curBots = new Bot[0];
                // then start creating new ones:
                Invoke("StartCreatingBots", startCreatingBotsAfter);
            }
        }
    }
}
