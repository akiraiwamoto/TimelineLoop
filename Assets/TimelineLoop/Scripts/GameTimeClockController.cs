using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class GameTimeClockController : MonoBehaviour
{
	private static GameTimeClockController instance;
	public static GameTimeClockController Instance { get { return instance; } }
	
	[SerializeField]
	private PlayableDirector director;

	private GameTimeClockClip clockClip;

	private void Awake()
	{
		if (instance == null) { instance = this; }
		else if (instance != this) { Destroy(this); }

		var timelineAsset = director.playableAsset as TimelineAsset;
		if (timelineAsset == null) { return; }
		var tracks = timelineAsset.GetOutputTracks();
		if (tracks == null) { return; }
		foreach (var track in tracks)
		{
			var clips = track.GetClips();
			if (clips == null) { continue; }
			foreach (var clip in clips)
			{
				var cc = clip.asset as GameTimeClockClip;
				if (cc == null) { continue; }
				clockClip = cc;
				return;
			}
		}

		if (clockClip == null) { Destroy(this); }
	}

	/// <summary>
	/// コールバックの設定
	/// </summary>
	/// <param name="callBack">コールバック</param>
	/// <returns>CBのID</returns>
	public int SetCB(System.Action<double> callBack)
	{
		return clockClip.SetCB(callBack);
	}

	/// <summary>
	/// コールバックの削除
	/// </summary>
	/// <param name="id">削除したいCBのID</param>
	public void RemoveCB(int id)
	{
		clockClip.RemoveCB(id);
	}

	/// <summary>
	/// CBの全削除
	/// </summary>
	public void ClearCB()
	{
		clockClip.ClearCB();
	}
}