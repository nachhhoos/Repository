using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon;

namespace Visyde
{
    /// <summary>
    /// Player Controller
    /// - The player controller itself! Requires a 2D character controller (like the MovementController.cs) to work 
    /// </summary>

    public class PlayerController : PunBehaviour
    {
        [System.Serializable]
        public class Character
        {
            public CharacterData data;
            public Animator animator;
        }
        public Character[] characters;                          // list of characters for the spawnable characters (modifying this will not change the main menu character selection screen
                                                                // NOTE: please read the manual to learn how to add and remove characters from the character selection screen)

        [Space]
        [Header("Settings:")]
        public string grenadePrefab;
        public float grenadeThrowForce = 20;

        [Header("References:")]
        public AIController ai;									// the AI controller for this player (only gets enabled if this is a bot, disabled when not)
        public AudioSource aus;                                 // the AudioSource that will play the player sounds
        public MovementController movementController;           // the one that controls the rigidbody movement
        public Transform weaponHandler;                         // the transform that holds weapon prefabs
        public Transform grenadePoint;                          // where grenades spawn
        public MeleeWeaponController meleeWeapon;               // the melee weapon controller
        public GameObject invulnerabilityIndicator;				// shown when player is invulnerable
        public AudioClip[] hurtSFX;                             // audios that are played randomly when getting damaged
        public AudioClip throwGrenadeSFX;               		// audio that's played when throwing grenades
        public GameObject spawnVFX;								// audio that's played on spawn
        public EmotePopup emotePopupPrefab;

        // Network:
        double curTime, curPosTime, lastPosTime, timeToReachPos;
        Vector3 positionAtLastPacket;
        string lastDamageDealer;                                // the last player to damage us (used to track who killed us and etc.)

        // Internals:
        [HideInInspector] public int curCharacter;              // determines which character is used for this player
        [HideInInspector] public int health;                    // the current health of this player
        [HideInInspector] public int lastWeaponId;              // used when sending damage across the network so everyone knows what weapon were used (negative value means character id)
        [HideInInspector] public bool isDead;
        [HideInInspector] public Vector3 mousePos;              // the mouse position we're working on locally
        [HideInInspector] public Weapon curWeapon;              // the current "physical" weapon the player is holding
        [HideInInspector] public Weapon originalWeapon;         // the current weapon's prefab reference
        [HideInInspector] public EmotePopup curEmote;           // this player's own emote popup
        [HideInInspector] public Vector2 moveTo;
        [HideInInspector] public int curGrenadeCount;
        [HideInInspector] public int curMultikill;              // current multi kills
        [HideInInspector] public ObjectPooler pooler;
        [HideInInspector] public bool isOnJumpPad;				// when true, jumping is disabled to not interfere with the jump pad
        [HideInInspector] public GameManager gm;                // GameManager reference
        [HideInInspector] public CameraController cam;          // The main camera in the game scene (used for the controls)
        [HideInInspector] public Vector3 nMousePos;             // mouse position from network. We're gonna smoothly interpolate the mousePos' value to this one to prevent the jittering effect.
        [HideInInspector] public bool shooting;                 // are we shooting?
        [HideInInspector] public float xInput;				    // the X input for the movement controls (sent to other clients for animation speed control)
        float jumpProgress;                                     // longer press means higher jump
        float curInvulnerability;
        float curMeleeAttackRate;
        float curMultikillDelay = 1;
        bool moving;                                            // are we moving on ground?
        bool isFalling;                                         // are we falling? (can be used for something like a falling animation)
        bool lastFrameGrounded;                                 // used for spawning landing vfx
        bool doneDeadZone;										// makes sure that DeadZoned() doesn't called repeatedly
        float lastGroundedTime;
        Vector3 lastAimPos;                             		// used for mobile controls

        // Returns true if invulnerable:
        public bool invulnerable
        {
            get
            {
                return curInvulnerability < gm.invulnerabilityDuration;
            }
        }
        // Returns true if this is a bot:
        public bool isBot
        {
            get
            {
                return photonView.isSceneView;
            }
        }

        void Start()
        {
            // Spawn VFX:
            Instantiate(spawnVFX, transform);

            // Find essential references:
            gm = GameManager.instance;
            pooler = FindObjectOfType<ObjectPooler>();
            cam = FindObjectOfType<CameraController>();

            // Spawn our own emote popup:
            curEmote = Instantiate(emotePopupPrefab, Vector3.zero, Quaternion.identity);
            curEmote.owner = this;

            // Add this to the player controllers list of the Game Manager:
            gm.playerControllers.Add(this);

            // If this is a bot, we need to initialize it and get its bot index from its instantiation data:
            if (isBot)
            {
                ai.InitializeBot((int)photonView.instantiationData[0]);
                ai.enabled = true;
            }
            else
            {
                ai.enabled = false;
            }

            // Reset player stats and stuff:
            RestartPlayer();

            // Load things up:
            gm.ui.SpawnFloatingBar(this);
            movementController.moveSpeed = characters[curCharacter].data.moveSpeed;
            movementController.jumpForce = characters[curCharacter].data.jumpForce;
            curGrenadeCount = characters[curCharacter].data.grenades;

            // Find the camera and let it know we're the local player:
            if (photonView.isMine && !isBot)
            {
                cam.target = this;
            }

            // Disable gravity and other things when we're not the owner since we're gonna do the movement over the network anyway:
            movementController.isMine = photonView.isMine;

            // Equip the starting weapon (if our current character has one):
            EquipStartingWeapon();

            if (photonView.isMine)
            {
                // Setting up send rates:
                PhotonNetwork.sendRate = 24;
                PhotonNetwork.sendRateOnSerialize = 24;
            }
        }

        void Update()
        {
            if (!isDead)
            {
                // Manage invulnerability:
                // *When invulnerable:
                if (curInvulnerability < gm.invulnerabilityDuration)
                {
                    if (gm.gameStarted) curInvulnerability += Time.deltaTime;

                    // Show the invulnerability indicator:
                    invulnerabilityIndicator.SetActive(true);
                }
                // *When not:
                else
                {
                    // Hide invulnerability indicator when finally vulnerable:
                    if (invulnerabilityIndicator.activeSelf) invulnerabilityIndicator.SetActive(false);
                }


                // Check if we're currently falling:
                isFalling = movementController.rg.velocity.y < 0;

                // Dead zone interaction:
                if (gm.deadZone)
                {
                    if (transform.position.y < gm.deadZoneOffset && !doneDeadZone)
                    {
                        DeadZoned(); // Dead zone'd!!!
                        doneDeadZone = true;
                    }
                }

                // If owned by us (including bots):
                if (photonView.isMine)
                {
                    // *For our player:
                    if (IsPlayerOurs())
                    {
                        // Example emote keys (this is just a hard-coded example of displaying an emote using alphanumeric keys):
                        if (Input.GetKeyDown(KeyCode.Alpha1)){
                            photonView.RPC("Emote", PhotonTargets.All, 0);
                        }
                        if (Input.GetKeyDown(KeyCode.Alpha2))
                        {
                            photonView.RPC("Emote", PhotonTargets.All, 1);
                        }
                        if (Input.GetKeyDown(KeyCode.Alpha3))
                        {
                            photonView.RPC("Emote", PhotonTargets.All, 2);
                        }

                        // Is moving on ground?:
                        moving = movementController.rg.velocity.x != 0 && movementController.isGrounded && xInput != 0;

                        // Only allow controls if the menu is not shown (the menu when you press 'ESC' on PC):
                        if (!gm.ui.isMenuShown)
                        {
                            // ...and also if the game has started:
                            if (gm.gameStarted && !gm.isGameOver)
                            {
                                // Mouse position on screen or Joystick value if mobile (will be sent across the network):
                                if (gm.useMobileControls)
                                {
                                    // Mobile joystick:
                                    lastAimPos = new Vector3(gm.controlsManager.aimX, gm.controlsManager.aimY, 0).normalized;
                                    mousePos = lastAimPos + new Vector3(transform.position.x, weaponHandler.position.y, 0);
                                }
                                else
                                {
                                    // PC mouse:
                                    mousePos = cam.theCamera.ScreenToWorldPoint(Input.mousePosition);
                                }

                                // MOVEMENT INPUT:
                                xInput = gm.useMobileControls ? gm.controlsManager.horizontal : gm.controlsManager.horizontalRaw;

                                // Jumping:
                                if (gm.controlsManager.jump)
                                {
                                    Jump();
                                }

                                // Shooting:
                                shooting = gm.controlsManager.shoot;

                                // Melee:
                                if (!gm.useMobileControls && Input.GetButtonDown("Fire2"))
                                {
                                    OwnerMeleeAttack();
                                }

                                // Grenade throw:
                                if (!gm.useMobileControls && Input.GetButtonDown("Fire3"))
                                {
                                    OwnerThrowGrenade();
                                }
                            }
                            else
                            {
                                // Reset movement inputs when game is over:
                                xInput = 0;
                            }
                        }
                        else
                        {
                            xInput = 0;
                        }
                    }
                    // *For the bots:
                    else
                    {
                        if (!gm.isGameOver)
                        {
                            // Smooth mouse aim sync for the bot:
                            mousePos = nMousePos;
                        }
                        else
                        {

                        }
                    }

                    // Melee attack rate:
                    if (curMeleeAttackRate < 1)
                    {
                        curMeleeAttackRate += Time.deltaTime * meleeWeapon.attackSpeed;
                    }

                    // Multikill timer:
                    if (curMultikillDelay > 0)
                    {
                        curMultikillDelay -= Time.deltaTime;
                    }
                    else
                    {
                        curMultikill = 0;
                    }
                }
                else
                {
                    // Smooth mouse aim sync:
                    mousePos = Vector3.MoveTowards(mousePos, nMousePos, Time.deltaTime * (mousePos - nMousePos).magnitude * 15);
                }

                // Apply movement input to the movement controller:
                movementController.InputMovement(xInput);

                // Landing VFX:
                if (movementController.isGrounded)
                {
                    if (!lastFrameGrounded && (Time.time - lastGroundedTime) > 0.1f)
                    {
                        Land();
                    }
                    lastFrameGrounded = movementController.isGrounded;
                    lastGroundedTime = Time.time;
                }
                else
                {
                    lastFrameGrounded = movementController.isGrounded;
                }

                // Hide gun if attacking with melee weapon:
                weaponHandler.gameObject.SetActive(!meleeWeapon.isAttacking);

                // Flipping:
                transform.localScale = new Vector3(mousePos.x > transform.position.x ? 1 : mousePos.x < transform.position.x ? -1 : transform.localScale.x, 1, 1);

                // Since we're syncing everyone's mouse position across the network, we can just do the aiming locally:
                Vector3 diff = mousePos - weaponHandler.position;
                diff.Normalize();
                float rot_z = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
                weaponHandler.rotation = Quaternion.Euler(0f, 0f, rot_z + (transform.localScale.x == -1 ? 180 : 0));
            }
            // If dead, prevent any collision and disable physics etc:
            else
            {
                Collider2D[] cols = GetComponentsInChildren<Collider2D>();
                for (int i = 0; i < cols.Length; i++)
                {
                    cols[i].enabled = false;
                }
                movementController.rg.isKinematic = true;
                movementController.movement = Vector2.zero;
                movementController.rg.velocity = Vector2.zero;

                invulnerabilityIndicator.SetActive(false);
            }

            // Lag compensation (movement interpolation):
            if (!photonView.isMine && moveTo != Vector2.zero)
            {
                timeToReachPos = curPosTime - lastPosTime;
                curTime += Time.deltaTime;
                transform.position = Vector3.Lerp(positionAtLastPacket, moveTo, (float)(curTime / timeToReachPos));
            }

            // Handling death:
            if (health <= 0 && !isDead)
            {
                if (!gm.isGameOver)
                {
                    isDead = true;

                    // Remove any weapons:
                    DisarmItem();
                }

                // Update the others about our status:
                if (photonView.isMine)
                {
                    photonView.RPC("UpdateOthers", PhotonTargets.All, health);
                    if (!gm.isGameOver && IsPlayerOurs()) gm.dead = true;
                }
                Die(lastDamageDealer);
            }

            // Animations:
            if (characters[curCharacter].animator)
            {
                characters[curCharacter].animator.SetBool("Moving", moving);
                characters[curCharacter].animator.SetBool("Dead", isDead);
                characters[curCharacter].animator.SetBool("Falling", isFalling);

                // Set the animator speed based on the current movement speed (only applies to grounded moving animations such as running):
                characters[curCharacter].animator.speed = moving && movementController.isGrounded ? Mathf.Abs(xInput) : 1;
            }
        }

        public void EquipStartingWeapon()
        {

            if (characters[curCharacter].data.startingWeapon)
            {
                // A negative value as a weapon id is invalid, but we can use it to tell everyone that it's a starting weapon since starting weapons don't need id's because 
                // there is only one starting weapon for each character anyway.
                // Starting weapons might not be set as spawnable in a map so refer to the current character's data instead:
                lastWeaponId = -(curCharacter + 1); // deacreased by 1 because an index of 0 will not do the trick (will be resolve later)

                // Spawn the starting weapon:
                originalWeapon = characters[curCharacter].data.startingWeapon;
                curWeapon = Instantiate(originalWeapon, weaponHandler);
                curWeapon.owner = this;
                curWeapon.gm = gm;
            }
        }

        // Returns this player's PhotonPlayer:
        public PhotonPlayer GetOwner()
        {
            PhotonPlayer p = null;
            // If a bot player:
            if (isBot)
            {
                p = gm.bots[ai.botID];
            }
            // If a human player:
            else
            {
                p = photonView.owner;
            }

            return p;
        }

        // Check if this player is ours and not owned by a bot or another player:
        public bool IsPlayerOurs()
        {
            bool result = false;
            PhotonPlayer o = GetOwner();
            if (o != null)
            {
                result = GetOwner() == PhotonNetwork.player;
            }
            return result;
        }

        public void RestartPlayer()
        {
            // Get the chosen character of this player (we only need the index of the chosen character in DataCarrier's characters array):
            int chosenCharacter = (int)GetOwner().CustomProperties["character"];
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i].data == DataCarrier.characters[chosenCharacter])
                {
                    curCharacter = i;
                }
            }

            // Enable only the chosen character's graphics:
            for (int i = 0; i < characters.Length; i++)
            {
                if (i == curCharacter)
                {
                    characters[i].animator.gameObject.SetActive(true);
                }
                else
                {
                    characters[i].animator.gameObject.SetActive(false);
                }
            }

            // Get the stat infos from the character data:
            health = characters[curCharacter].data.maxHealth;
            

            // Remove any weapon:
            DisarmItem();

        }

        public void Jump()
        {
            if (!isOnJumpPad && movementController.isGrounded && movementController.allowJump)
            {
                // Call jump in character controller:
                movementController.Jump();

                if (characters[curCharacter].data.jumpSFX.Length > 0)
                {
                    aus.PlayOneShot(characters[curCharacter].data.jumpSFX[Random.Range(0, characters[curCharacter].data.jumpSFX.Length)]);
                }
            }
        }

        public void Land()
        {
            pooler.Spawn("LandDust", transform.position, Quaternion.identity);
            // Sound:
            if (characters[curCharacter].data.landingsSFX.Length > 0) aus.PlayOneShot(characters[curCharacter].data.landingsSFX[Random.Range(0, characters[curCharacter].data.landingsSFX.Length)]);
        }

        // Called by the owner from mobile or pc input:
        public void OwnerMeleeAttack()
        {
            if (curMeleeAttackRate >= 1)
            {
                photonView.RPC("MeleeAttack", PhotonTargets.All);
                curMeleeAttackRate = 0;
            }
        }
        public void OwnerThrowGrenade()
        {
            if (curGrenadeCount > 0)
            {
                curGrenadeCount -= 1;
                photonView.RPC("ThrowGrenade", PhotonTargets.All);
            }
        }

        void Die(string killer)
        {
            if (!gm.isGameOver)
            {
                // Multikill (if we are the killer and we are not the one dying):
                PlayerController killerPc = gm.GetPlayerControllerOfPlayer(killer);

                // Check if killer isn't dead:
                if (killerPc)
                {
                    // If the killer is ours (bots are also ours if we're the master client):
                    if (killerPc == gm.ourPlayer && !IsPlayerOurs() || (killerPc.isBot && PhotonNetwork.isMasterClient))
                    {
                        killerPc.curMultikill += 1;
                        killerPc.curMultikillDelay = gm.multikillDuration;

                        // Add a bonus score to killer for doing a multi kill:
                        if (killerPc.curMultikill > 1)
                        {
                            int scoreToAdd = gm.multiKillMessages[Mathf.Clamp(killerPc.curMultikill - 1, 0, gm.multiKillMessages.Length - 1)].bonusScore;
                            killerPc.AddScore(killerPc.GetOwner().NickName, false, false, scoreToAdd);
                        }
                    }
                }
            }

            // Officially die:
            if (photonView.isMine)
            {
                if (!gm.isGameOver)
                {
                    // Kill score to killer:
                    AddScore(killer, killer != GetOwner().NickName ? true : false, false, 0);

                    // Add death to us:
                    AddScore(GetOwner().NickName, false, true, 0);

                    // Display "killed" message:
                    gm.photonView.RPC("SomeoneDied", PhotonTargets.All, GetOwner().NickName, killer);

                    // and then destroy (give a time for the death animation):
                    Invoke("PhotonDestroy", 1f);
                }
            }
        }

        void PhotonDestroy()
        {
            PhotonNetwork.Destroy(photonView);
        }

        /// <summary>
        /// Deal damage to player.
        /// </summary>
        /// <param name="fromPlayer">Damage dealer player name.</param>
        /// <param name="value">Can be either a weapon id (if a gun was used) or a damage value (if melee attack or grenade).</param>
        /// <param name="gun">If set to <c>true</c>, "value" will be used as weapon id.</param>
        [PunRPC]
        void Hurt(string fromPlayer, int value, bool gun)
        {
            if (!gm.isGameOver)
            {
                // Only damage if vulnerable:
                if (!invulnerable)
                {
                    int finalDamage = 0; // the damage value

                    // If damage is from a gun:
                    if (gun)
                    {
                        // Get the weapon used using the "value" parameter as weapon id (or if it's a negative value, then it's a character id):
                        Weapon weaponUsed = value >= 0 ? gm.maps[gm.chosenMap].spawnableWeapons[value] : characters[value * -1 - 1].data.startingWeapon;

                        // ...then get the weapon's damage value:
                        finalDamage = weaponUsed.damage;
                    }
                    else
                    {
                        // If not a gun then it could be from a grenade or a melee attack, either way, just assume that the "value" parameter is the damage value:
                        finalDamage = value;
                    }

                    // Now do the damage application:
                    health -= finalDamage;

                    // Damage popup:
                    if (gm.damagePopups)
                    {
                        pooler.Spawn("DamagePopup", weaponHandler.position, Quaternion.identity).GetComponent<DamagePopup>().Set(finalDamage);
                    }

                    // Sound:
                    aus.PlayOneShot(hurtSFX[Random.Range(0, hurtSFX.Length)]);

                    // Do the "hurt screen" effect:
                    if (IsPlayerOurs())
                    {
                        gm.ui.Hurt();
                    }
                    lastDamageDealer = fromPlayer;
                }
            }
        }

        // Instant death from dead zone:
        public void DeadZoned()
        {
            lastDamageDealer = GetOwner().NickName;
            health = 0;

            // VFX:
            if (gm.maps[gm.chosenMap].deadZoneVFX)
            {
                Instantiate(gm.maps[gm.chosenMap].deadZoneVFX, new Vector3(transform.position.x, gm.deadZoneOffset, 0), Quaternion.identity);
            }
        }

        // Called by the weapon our character's currently holding:
        [PunRPC]
        public void Shoot(Vector3 curMousePos, Vector2 curPlayerPos)
        {
            // We're gonna update others about our player and mouse position first so everything's synced up:
            mousePos = curMousePos;
            nMousePos = curMousePos;
            movementController.rg.position = curPlayerPos;
            moveTo = curPlayerPos;
            // ...then the shooting itself:
            curWeapon.Shoot();
        }
        [PunRPC]
        public void ThrowGrenade()
        {

            // Sound:
            aus.PlayOneShot(throwGrenadeSFX);

            // Grenade spawning:
            if (photonView.isMine)
            {
                Vector2 p1 = new Vector2(grenadePoint.position.x, grenadePoint.position.y);
                Vector2 p2 = new Vector2(weaponHandler.position.x, weaponHandler.position.y);
                object[] data = new object[] { (p1 - p2) * grenadeThrowForce, GetOwner().NickName }; // the instantiation data of a grenade includes the direction of the throw and the owner's name 

                PhotonNetwork.Instantiate(grenadePrefab, grenadePoint.position, Quaternion.identity, 0, data);
            }
        }
        [PunRPC]
        public void MeleeAttack()
        {
            meleeWeapon.Attack(photonView.isMine, this);
        }

        [PunRPC]
        public void GrabWeapon(int id, int getFromSpawnPoint)
        {

            // Find the weapon in spawnable weapons of the current map:
            Weapon theWeapon = getFromSpawnPoint != -1 ? gm.maps[gm.chosenMap].weaponSpawnPoints[getFromSpawnPoint].onlySpawnThisHere : gm.maps[gm.chosenMap].spawnableWeapons[id];

            // Disarm current item first (if we have one):
            DisarmItem();

            originalWeapon = theWeapon;
            // ...then instantiate one based on the new item:
            curWeapon = Instantiate(theWeapon, weaponHandler);
            curWeapon.owner = this;
            curWeapon.gm = gm;
            // Also, let's save the weapon ID:
            lastWeaponId = getFromSpawnPoint != -1 ? System.Array.IndexOf(gm.maps[gm.chosenMap].spawnableWeapons, gm.maps[gm.chosenMap].weaponSpawnPoints[getFromSpawnPoint].onlySpawnThisHere) : id;
        }
        [PunRPC]
        public void ReceivePowerUp(int id, int getFromSpawnPoint)
        {

            // Find the weapon in spawnable weapons of the current map:
            PowerUp thePowerUp = getFromSpawnPoint != -1 ? gm.maps[gm.chosenMap].powerUpSpawnPoints[getFromSpawnPoint].onlySpawnThisHere : gm.maps[gm.chosenMap].spawnablePowerUps[id];

            // ...then do the effect:
            // HEAL:
            health += thePowerUp.addedHealth;
            health = Mathf.Clamp(health, 0, characters[curCharacter].data.maxHealth);
            // GRENADE REFILL:
            curGrenadeCount += thePowerUp.addedGrenade;
            // AMMO REFILL:
            if (curWeapon && thePowerUp.fullRefillAmmo)
            {
                curWeapon.curAmmo = curWeapon.ammo;
            }

            // Update others about our current health:
            photonView.RPC("UpdateOthers", PhotonTargets.Others, health);
        }

        [PunRPC]
        public void Emote(int emote){
            if (curEmote && curEmote.isReady){
                curEmote.Show(emote);
            }
        }
        // *************************************************

        [PunRPC]
        public void UpdateOthers(int curHealth)
        {
            health = curHealth;
        }

        void DisarmItem()
        {
            if (curWeapon)
            {
                //Pickup p = Instantiate (pickupPrefab, transform.up * 2, Quaternion.identity);
                //p.itemHandled = originalItem;
                Destroy(curWeapon.gameObject);
            }
        }

        void OnDestroy()
        {
            gm.playerControllers.Remove(this);
        }
        // *****************************************************

        public void AddScore(string player, bool kill, bool death, int others)
        {
            ExitGames.Client.Photon.Hashtable h = new ExitGames.Client.Photon.Hashtable();

            // Get the player list:
            PhotonPlayer[] players = gm.GetPlayerList();

            // The matching player:
            PhotonPlayer thePlayer = null;

            // Find the player with the given name from the player list:
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].NickName == player)
                {
                    thePlayer = players[i];
                    break;
                }
            }

            // If there's a player matching the name, add the scores:
            if (thePlayer != null)
            {
                if (kill) h.Add("kills", (int)thePlayer.CustomProperties["kills"] + 1);
                if (death) h.Add("deaths", (int)thePlayer.CustomProperties["deaths"] + 1);
                if (others > 0) h.Add("otherScore", (int)thePlayer.CustomProperties["otherScore"] + others);
                thePlayer.SetCustomProperties(h);

                // Also, if the player is a "bot", we need to upload the new bot scores as well:
                if (thePlayer.CustomProperties.ContainsKey("bot"))
                {
                    gm.UpdateBotStats();
                }
            }
        }

        public override void OnPhotonPlayerPropertiesChanged(object[] playerAndUpdatedProps)
        {
            if (gm) gm.ui.UpdateBoards();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.isWriting && photonView.isMine)
            {
                // Send controls over network
                stream.SendNext(movementController.rg.position);
                stream.SendNext(mousePos);
                stream.SendNext(moving);
                stream.SendNext(isFalling);
                stream.SendNext(xInput);
            }
            else if (stream.isReading)
            {
                // Receive controls
                moveTo = (Vector2)(stream.ReceiveNext());
                nMousePos = (Vector3)(stream.ReceiveNext());
                moving = (bool)(stream.ReceiveNext());
                isFalling = (bool)(stream.ReceiveNext());
                xInput = (float)(stream.ReceiveNext());

                // Lag compensation:
                curTime = 0.0;
                positionAtLastPacket = transform.position;
                lastPosTime = curPosTime;
                curPosTime = info.timestamp;
            }
        }
    }
}