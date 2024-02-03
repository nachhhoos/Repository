using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Visyde
{
    /// <summary>
    /// Lobby Browser UI
    /// - The script for the sample lobby browser interface
    /// </summary>

    public class LobbyBrowserUI : MonoBehaviour
    {
        [Header("Browser:")]
        public float browserRefreshRate = 3f;   // how many times should the browser refresh itself
        public Transform roomItemHandler;		// this is where the room item prefabs will be spawned
        public RoomItem roomItemPrefab;         // the room item prefab (represents a game session in the lobby list)
        public Text listStatusText;             // displays the current status of the lobby browser (eg. "No games available", "Fetching game list...")

        [Header("Create Screen:")]
        public Text mapNameText;
        public SelectorUI mapSelector;
        public SelectorUI playerNumberSelector;
        public Toggle enableBotsOption;

        [Header("Joined Screen:")]
        public CustomGamePlayerItem playerItemPrefab;
        public Transform playerItemHandler;
        public ChatSystem chatSystem;
        public Text chosenMapText;
        public Text chosenPlayerNumberText;
        public Text enableBotsText;
        public Text currentNumberOfPlayersInRoomText;
        public Button startBTN;

        // Internals:
        string randomRoomName;
        RoomInfo[] rooms = new RoomInfo[0];
        RoomInfo[] lastRooms = new RoomInfo[0];

        void Start(){
            StartCoroutine("RefreshBrowser");
        }

        // Update is called once per frame
        void Update()
        {
            // ***BROWSE***
            // If UI list is not blank, don't show error text:
			if (roomItemHandler.childCount > 0){
                listStatusText.text = "";
            }
            else
            {
                // If blank but Photon room list has contents, populate the UI list:
                if (rooms.Length > 0)
                {
                    for (int i = 0; i < rooms.Length; i++)
                    {
                        if ((bool)rooms[i].CustomProperties["isInMatchmaking"] == false)
                        {
                            RoomItem r = Instantiate(roomItemPrefab, roomItemHandler);
                            r.Set(rooms[i], this);
                        }
                    }
                }
                // else, just show the error text:
                else
                {
                    listStatusText.text = "No games are currently available";
                }
            }
            
            // ***CREATE***
            // Display selected map name:
            mapNameText.text = Connector.instance.maps[mapSelector.curSelected];

            // ***JOINED***
            startBTN.interactable = PhotonNetwork.isMasterClient;
        }

        IEnumerator RefreshBrowser()
        {
            while (true)
            {
                // Fetch game list:
                rooms = PhotonNetwork.GetRoomList();
                
                // Clear UI list if room list changed:
                if (lastRooms != rooms){
                    foreach (Transform t in roomItemHandler)
                    {
                        Destroy(t.gameObject);
                    }
                    lastRooms = rooms;
                }

                // Wait for refresh rate before repeating:
                yield return new WaitForSecondsRealtime(1f / browserRefreshRate);
            }
        }
        public void Join(RoomInfo room){
            PhotonNetwork.JoinRoom(room.Name);
        }
        public void Create(){
            Connector.instance.CreateCustomGame(mapSelector.curSelected, playerNumberSelector.items[playerNumberSelector.curSelected].value, enableBotsOption.isOn);
        }

        // Custom Game:
        void OnPhotonPlayerConnected(PhotonPlayer player)
        {
            // When a player connects, update the player list:
            RefreshPlayerList();

            // Notify other players through chat:
            chatSystem.SendSystemChatMessage(player.NickName + " joined the game.");
        }
        void OnPhotonPlayerDisconnected(PhotonPlayer player)
        {
            // When a player disconnects, update the player list:
            RefreshPlayerList();

            // Notify other players through chat:
            chatSystem.SendSystemChatMessage(player.NickName + " left the game.");
        }
        void OnPhotonCreateRoomFailed(){
            // Display error:
            DataCarrier.message = "Custom game creation failed.";
        }
        void OnJoinedRoom()
        {
            // Update the player list when we join a room:
            RefreshPlayerList();

            chosenMapText.text = Connector.instance.maps[(int)PhotonNetwork.room.CustomProperties["map"]];
            chosenPlayerNumberText.text = PhotonNetwork.room.MaxPlayers.ToString();
            enableBotsText.text = (bool)PhotonNetwork.room.CustomProperties["customAllowBots"]? "Yes" : "No";
        }

        void RefreshPlayerList(){
            
            // Clear list first:
            foreach (Transform t in playerItemHandler){
                Destroy(t.gameObject);
            }

            // Repopulate:
            PhotonPlayer[] players = PhotonNetwork.playerList;
            for (int i = 0; i < players.Length; i++)
            {
                CustomGamePlayerItem cgp = Instantiate(playerItemPrefab, playerItemHandler, false);
                cgp.Set(players[i]);
            }

            // Player number in room text:
            currentNumberOfPlayersInRoomText.text = "Players (" + PhotonNetwork.room.PlayerCount + "/" + PhotonNetwork.room.MaxPlayers + ")";
        }
        public void LeaveRoom(){
            if (PhotonNetwork.inRoom){
                PhotonNetwork.LeaveRoom();
            }
        }
    }
}