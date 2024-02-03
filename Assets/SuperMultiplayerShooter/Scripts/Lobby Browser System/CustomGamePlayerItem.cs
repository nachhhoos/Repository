using UnityEngine;
using UnityEngine.UI;

namespace Visyde
{
    /// <summary>
    /// Custom Game Player Item
    /// - The script for the UI item that represents players in the custom game lobby
    /// </summary>

    public class CustomGamePlayerItem : MonoBehaviour
    {
        [Header("Settings:")]
        public Color ownerColor;

        [Header("References:")]
        public GameObject hostIndicator;
        public Text playerNameText;
        public Button kickBTN;

		public PhotonPlayer owner;

        public void Set(PhotonPlayer player)
        {
            owner = player;

            playerNameText.text = owner.NickName;
            if (owner.IsLocal)
            {
                playerNameText.color = ownerColor;
                kickBTN.gameObject.SetActive(false);
            }

            // Host indicator and the kick buttons:
            hostIndicator.SetActive(owner.IsMasterClient);
            kickBTN.gameObject.SetActive(PhotonNetwork.isMasterClient && !owner.IsLocal);
        }

        public void Kick()
        {
            PhotonNetwork.CloseConnection(owner);
        }
    }
}