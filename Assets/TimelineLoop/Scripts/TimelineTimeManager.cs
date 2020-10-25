using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TimelineTimeManager : MonoBehaviour
{
	public class ClipData
	{
		public double startTime;
		public double endTime;
		public double offsetTime;
		public bool isPlayed;
		public ETimelineControlType controlType;

		public ClipData(double startTime, double endTime, double offsetTime, bool isPlayed, ETimelineControlType controlType)
		{
			this.startTime = startTime;
			this.endTime = endTime;
			this.offsetTime = offsetTime;
			this.isPlayed = isPlayed;
			this.controlType = controlType;
		}

		/// <summary>
		/// 何かしら処理を行いたい時間を取得
		/// </summary>
		public double GetActionPointTime
		{
			get
			{
				switch (controlType)
				{
					case ETimelineControlType.Loop:
						return endTime - offsetTime;
					case ETimelineControlType.Pause:
					case ETimelineControlType.AutoSkip:
						return startTime;
					case ETimelineControlType.Skip:
					default:
						return endTime;
				}
			}
		}
	}

	[SerializeField]
	private PlayableDirector director;
	[SerializeField]
	private bool playOnAwake;
	[SerializeField]
	private double speed = 1.0;

	private bool isPlaying;
	private double currentTime = 0.0;
	private List<ClipData> loopClipDatas = new List<ClipData>();
	public int GetLoopClipCount { get { return loopClipDatas.Count; } }
	private int callBackID;

	#region mono
#if UNITY_EDITOR
	private void Update()
	{
		if (!isPlaying) { return; }
		if (GameTimeClockController.Instance != null) { return; }
		currentTime += Time.deltaTime * speed;
		UpdateTimeline();
	}
#endif

	private void OnEnable()
	{
		if (director == null)
		{
			director = GetComponent<PlayableDirector>();
			if (director == null) { return; }
			playOnAwake = playOnAwake || director.playOnAwake;
		}
		if (playOnAwake) { Play(); }
	}
	private void OnDestroy()
	{
		Stop();
	}
	#endregion

	#region privateMethod
	/// <summary>
	/// タイムライン更新処理
	/// </summary>
	private void UpdateTimeline()
	{
		if (loopClipDatas != null)
		{
			int dataIndex = loopClipDatas.FindIndex(data =>
			{
				if (data.isPlayed) { return false; }
				return data.GetActionPointTime < currentTime;
			});
			if (dataIndex >= 0)
			{
				if (loopClipDatas[dataIndex].controlType == ETimelineControlType.Loop ||
					loopClipDatas[dataIndex].controlType == ETimelineControlType.Pause)
				{
					currentTime = loopClipDatas[dataIndex].startTime;
				}
				else if (loopClipDatas[dataIndex].controlType == ETimelineControlType.AutoSkip)
				{
					currentTime = loopClipDatas[dataIndex].endTime;
					loopClipDatas[dataIndex].isPlayed = true;
				}
				else if (loopClipDatas[dataIndex].controlType == ETimelineControlType.Skip)
				{
					loopClipDatas[dataIndex].isPlayed = true;
				}
			}
		}

		if (currentTime > director.duration)
		{
			if (director.extrapolationMode == DirectorWrapMode.None)
			{
				Stop();
				return;
			}
			else if (director.extrapolationMode == DirectorWrapMode.Loop)
			{
				currentTime = 0.0;
			}
			else if (director.extrapolationMode == DirectorWrapMode.Hold)
			{
				currentTime = director.duration;
			}
		}
		director.time = currentTime;
		director.Evaluate();
	}

	/// <summary>
	/// 指定した時間よりも前のClipは再生済みにする
	/// </summary>
	/// <param name="startTime"></param>
	private void SetClipPlayedByTime(double startTime)
	{
		if (loopClipDatas == null || loopClipDatas.Count <= 0) { return; }
		foreach (var data in loopClipDatas)
		{
			if (startTime + 0.01 < data.endTime) { data.isPlayed = false; }
			else { data.isPlayed = true; }
		}
	}
	#endregion

	#region publicMethod
	/// <summary>
	/// 再生開始
	/// </summary>
	public void Play(bool isAutoSetting = true)
	{
		if (isPlaying) { return; }
		if (isAutoSetting) { SetLoopClipDatas(); }
		isPlaying = true;
		director.Play();
		//UpdateTimeline();

#if UNITY_EDITOR
		if (GameTimeClockController.Instance == null) { return; }
#endif

		callBackID = GameTimeClockController.Instance.SetCB(deltaTime =>
		{
			currentTime += deltaTime * speed;
			UpdateTimeline();
		});
	}

	/// <summary>
	/// 指定した時間から再生開始
	/// </summary>
	/// <param name="startTime">開始時間</param>
	public void Play(double startTime, bool isAutoSetting = true)
	{
		if (isPlaying) { return; }
		if (director.duration < startTime) { return; }
		if (isAutoSetting) { SetLoopClipDatas(); }
		currentTime = startTime;
		SetClipPlayedByTime(startTime);
		Play(false);
	}

	/// <summary>
	/// 再生停止
	/// </summary>
	public void Stop()
	{
		if (!isPlaying) { return; }
#if UNITY_EDITOR
		if (GameTimeClockController.Instance != null) { GameTimeClockController.Instance.RemoveCB(callBackID); }
#else
		GameTimeClockController.Instance.RemoveCB(callBackID);
#endif
		isPlaying = false;
		currentTime = 0.0;
		director.Stop();
	}

	/// <summary>
	/// LoopClipからループなどしたい時間を取得する
	/// </summary>
	public void SetLoopClipDatas()
	{
		loopClipDatas.Clear();
		if (director == null)
		{
			director = GetComponent<PlayableDirector>();
			if (director == null) { return; }
		}
		director.timeUpdateMode = DirectorUpdateMode.Manual;

		var timelineAsset = director.playableAsset as TimelineAsset;
		if (timelineAsset == null) { return; }

		var tracks = timelineAsset.GetOutputTracks();
		if (tracks == null) { return; }

		foreach (var track in tracks)
		{
			if (track.muted) { continue; }
			var clips = track.GetClips();
			if (clips == null) { continue; }

			foreach (var clip in clips)
			{
				var loopClip = clip.asset as LoopClip;
				if (loopClip == null) { continue; }

				loopClipDatas.Add(new ClipData(clip.start, clip.end, loopClip.GetOffsetSecond, false, loopClip.GetControlType));
			}
		}

		// LoopClipの動作したい箇所が早い順に並び変える
		loopClipDatas.Sort((a, b) => a.GetActionPointTime.CompareTo(b.GetActionPointTime));
	}

	/// <summary>
	/// 次のクリップのendに飛ぶ
	/// </summary>
	public void SkipToClipEnd()
	{
		if (!isPlaying) { return; }

		var dataIndex = loopClipDatas.FindIndex(data => { return !data.isPlayed; });
		if (dataIndex < 0) { return; }

		loopClipDatas[dataIndex].isPlayed = true;
		currentTime = loopClipDatas[dataIndex].endTime;
		UpdateTimeline();
	}

	/// <summary>
	/// 指定した番号のクリップを再生済みにする
	/// </summary>
	/// <param name="index">指定したい番号</param>
	public void SetClipPlayed(int index)
	{
		if (index < 0 || index >= loopClipDatas.Count) { return; }
		loopClipDatas[index].isPlayed = true;
	}

	/// <summary>
	/// 指定した番号のクリップを再生済みにする
	/// </summary>
	/// <param name="indexes">指定したい番号の配列</param>
	public void SetClipPlayed(int[] indexes)
	{
		for (int i = 0; i < indexes.Length; i++)
		{
			SetClipPlayed(indexes[i]);
		}
	}

	/// <summary>
	/// 最後のクリップ以外再生済みにする(電断復帰用)
	/// </summary>
	/// <returns>最終クリップの頭の時間</returns>
	public double SetClipPlayedWithoutLast()
	{
		List<int> indexList = new List<int>();
		for (int i = 0; i < loopClipDatas.Count - 1; i++)
		{
			indexList.Add(i);
		}
		SetClipPlayed(indexList.ToArray());

		var lastIndex = loopClipDatas.FindLastIndex(data => !data.isPlayed);
		if (lastIndex < 0) { return 0.0; }
		else { return loopClipDatas[lastIndex].startTime; }
	}
	#endregion

#if UNITY_EDITOR
	[CustomEditor(typeof(TimelineTimeManager))]
	public class LoopTimelineClipManagerEditor : Editor
	{
		private TimelineTimeManager Instance
		{
			get { return target as TimelineTimeManager; }
		}
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (Application.isPlaying)
			{
				if (GUILayout.Button("SetClips"))
				{
					Instance.SetLoopClipDatas();
				}
				using (new EditorGUILayout.HorizontalScope())
				{
					if (GUILayout.Button("Play"))
					{
						Instance.Play();
					}
					if (GUILayout.Button("Skip"))
					{
						Instance.SkipToClipEnd();
					}
					if (GUILayout.Button("Stop"))
					{
						Instance.Stop();
					}
				}
			}
		}
	}
#endif
}