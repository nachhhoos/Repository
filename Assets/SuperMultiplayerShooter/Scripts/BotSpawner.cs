using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Visyde
{
    /// <summary>
    /// Bot Spawner
    /// - Every bot owns an instance of this. This manages the bot's spawning, just like what GameManager does to our player. 
    /// </summary>

    public class BotSpawner : MonoBehaviour
    {
        public int botId;
        public GameManager gm;
        public PlayerController theBot;

        // Privates:
        bool dead;
        double lastDeathTime;

        // NOTE: Always call this when instantiating:
        public void Initialize(int botNo, PlayerController botPlayer)
        {
            botId = botNo;
            theBot = botPlayer;
            dead = false;
        }

        // Update is called once per frame
        void Update()
        {
            // Check our bot while it's alive:
            if (!dead)
            {

                // "A non existing bot is a dead bot":
                if (theBot == null)
                {
                    Died();
                }
            }
            // Do respawn if it's dead:
            else
            {

                // If we are the master client, we have the authority to respawn bots:
                if (PhotonNetwork.isMasterClient)
                {

                    // Respawn!
                    if (PhotonNetwork.time - lastDeathTime >= gm.respawnTime)
                    {
                        dead = false;
                        Respawn();
                    }
                }
            }
        }

        public void Respawn()
        {
            Transform spawnPoint = gm.maps[gm.chosenMap].playerSpawnPoints[UnityEngine.Random.Range(0, gm.maps[gm.chosenMap].playerSpawnPoints.Count)];
            theBot = PhotonNetwork.InstantiateSceneObject(gm.playerPrefab, spawnPoint.position, Quaternion.identity, 0, new object[] { botId }).GetComponent<PlayerController>();
        }

        public void Died()
        {
            lastDeathTime = PhotonNetwork.time;
            dead = true;
        }
    }
}