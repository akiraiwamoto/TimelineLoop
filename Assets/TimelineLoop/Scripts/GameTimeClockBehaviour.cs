using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class GameTimeClockBehaviour : PlayableBehaviour
{
	public GameTimeClockClip clip;

	private float pastTime;

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		pastTime = Time.time;
	}

	public override void ProcessFrame(Playable playable, FrameData info, object playerData)
	{
		#if UNITY_EDITOR
		if (!Application.isPlaying) { return; }
		#endif
		clip.DoAllCB(Time.time - pastTime);
		pastTime = Time.time;
	}
}