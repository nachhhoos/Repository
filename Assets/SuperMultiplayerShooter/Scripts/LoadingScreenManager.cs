using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Visyde{

    /// <summary>
    /// Loading Screen Manager
    /// - A simple loading screen manager that displays load progress.
    /// </summary>

    public class LoadingScreenManager : MonoBehaviour {

		[Header("References:")]
		public Slider loadingBar;

		void Start(){
			StartCoroutine (LoadGameWorld ());
		}

		IEnumerator LoadGameWorld(){
			AsyncOperation prog = PhotonNetwork.LoadLevelAsync (DataCarrier.sceneToLoad);
			while (!prog.isDone) {
				loadingBar.value = prog.progress / 0.9f;
				yield return new WaitForEndOfFrame();
			}
		}
	}
}