using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Animation Event
/// - A very simple event caller
/// </summary>

public class AnimationEvent : MonoBehaviour {

    public UnityEvent doEvent;

	public void DoEvent(){
        doEvent.Invoke();
    }
}
