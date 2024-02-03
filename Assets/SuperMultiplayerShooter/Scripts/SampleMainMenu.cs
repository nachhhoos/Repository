using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Visyde
{
    /// <summary>
    /// Sample Main Menu
    /// - A sample script that handles the main menu UI.
    /// </summary>

    public class SampleMainMenu : MonoBehaviour
    {
        [Header("UI:")]
        public Text connectionStatusText;
        public Button findMatchBTN;
        public Button customMatchBTN;
        public GameObject findMatchCancelButtonObj;
        public GameObject findingMatchPanel;
        public GameObject customGameRoomPanel;
        public Text matchmakingPlayerCountText;
        public InputField playerNameInput;
        public GameObject messagePopupObj;
        public Text messagePopupText;
        public GameObject characterSelectionPanel;
        public Image characterIconPresenter;
        public GameObject loadingPanel;
        public Toggle frameRateSetting;

        void Awake(){
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        // Use this for initialization
        void Start()
        {
            // Load or create a username:
            if (PlayerPrefs.HasKey("name"))
            {
                playerNameInput.text = PlayerPrefs.GetString("name");
            }
            else
            {
                playerNameInput.text = "Player" + Random.Range(0, 9999);
            }
            SetPlayerName();

            // Others:
            frameRateSetting.isOn = Application.targetFrameRate == 60;
        }

        // Update is called once per frame
        void Update()
        {
            // Handling texts:
            connectionStatusText.text = PhotonNetwork.connectedAndReady ? "Connected! (" + PhotonNetwork.CloudRegion + ")" : PhotonNetwork.connecting ? "Connecting..." : "Finding network...";
            connectionStatusText.color = PhotonNetwork.connectedAndReady ? Color.green : Color.yellow;
            matchmakingPlayerCountText.text = PhotonNetwork.inRoom ? Connector.instance.totalPlayerCount + "/" + PhotonNetwork.room.MaxPlayers : "Matchmaking...";
            
            // Handling buttons:
            customMatchBTN.interactable = PhotonNetwork.connectedAndReady && !PhotonNetwork.inRoom;
            findMatchBTN.interactable = PhotonNetwork.connectedAndReady && !PhotonNetwork.inRoom;
            findMatchCancelButtonObj.SetActive(PhotonNetwork.inRoom);

            // Handling panels:
            customGameRoomPanel.SetActive(Connector.instance.isInCustomGame);
            loadingPanel.SetActive(PhotonNetwork.connectionStateDetailed == ClientState.ConnectingToGameserver || PhotonNetwork.connectionStateDetailed == ClientState.DisconnectingFromGameserver);

            // Messages popup system (used for checking if we we're kicked or we quit the match ourself from the last game etc):
            if (DataCarrier.message != "")
            {
                messagePopupObj.SetActive(true);
                messagePopupText.text = DataCarrier.message;
                DataCarrier.message = "";
            }
        }

        // Profile:
        public void SetPlayerName()
        {
            PlayerPrefs.SetString("name", playerNameInput.text);
            PhotonNetwork.playerName = playerNameInput.text;
        }

        // Main:
        public void FindMatch(){
            // Enable the "finding match" panel:
            findingMatchPanel.SetActive(true);
            // ...then finally, find a match:
            Connector.instance.FindMatch();
        }

        // Others:
        // *called by the toggle itself in the "On Value Changed" event:
        public void ToggleTargetFps(){
            Application.targetFrameRate = frameRateSetting.isOn? 60 : 30;

            // Display a notif message:
            if (frameRateSetting.isOn){
                DataCarrier.message = "Target frame rate has been set to 60.";
            }
        }
    }
}
