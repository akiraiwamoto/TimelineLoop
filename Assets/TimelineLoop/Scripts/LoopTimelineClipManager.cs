using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LoopTimelineClipManager : MonoBehaviour
{
    //autoClipSetがfalse時はDirector.Play()の前にPrepareClipToDirectorPlay()を呼ぶ必要がある
    [SerializeField, Header("muteになっていないトラックのクリップを自動で設定するか")]
    private bool autoClipSet = true;
    public bool AutoClipSet { get { return autoClipSet; } }

    private PlayableDirector director;
    private bool isTrackEnd;

    private TimelineClip targetClip;
    public TimelineClip GetTargetClip
    {
        get
        {
            if (isTrackEnd) { return null; }
            if (targetClip == null || ((LoopClip)targetClip.asset).IsPlayed) { SetTargetClip(); }
            return targetClip;
        }
    }
    public void SetTargetClip()
    {
        targetClip = GetMostPreviousClip();
        if (targetClip == null) { isTrackEnd = true; }
    }

    private Dictionary<TrackAsset, List<TimelineClip>> clipDict = new Dictionary<TrackAsset, List<TimelineClip>>();
    public void AddClip(TrackAsset key, TimelineClip clip)
    {
        if (!clipDict.ContainsKey(key)) { clipDict.Add(key, new List<TimelineClip>()); }
        var clips = clipDict[key];
        clips.Add(clip);
        SetClipIndex(clip);
    }
    public void ClearClips(TrackAsset key) { if (clipDict.ContainsKey(key)) { clipDict[key].Clear(); } }
    public void RemoveKey(TrackAsset key) { if (clipDict.ContainsKey(key)) { clipDict.Remove(key); } }
    public Dictionary<TrackAsset, List<TimelineClip>> GetClipDict { get { return clipDict; } }

    public void Start()
    {
        director = GetComponent<PlayableDirector>();
        if (director == null) { Debug.LogError("WaitTimelineはLoopを実装するDirectorと同じオブジェクトにアタッチしてください."); }
        #if UNITY_EDITOR
        SetClips();
        #endif
    }

    #region エディター上でのみ呼ばれる関数
    #if UNITY_EDITOR
    /// <summary>
    /// (OnEditor)トラックが全部削除されたときにGameObjectからこのコンポーネントをremoveする
    /// </summary>
    public void TryDestroyManager()
    {
        if (clipDict.Count == 0)
        {
            EditorApplication.delayCall += () => DestroyImmediate(this);
        }
    }

    /// <summary>
    /// (OnEditor)director初期化処理
    /// </summary>
    public void ReturnTop()
    {
        director.Stop();
    }
    
    /// <summary>
    /// (OnEditor)Timelineの再生
    /// </summary>
    public void PlayTimeline()
    {
        PrepareClipToPlayDirector();
        director.Play();
    }
    #endif
    #endregion

    #region public関数
    /// <summary>
    /// timelineを再生する前にクリップ情報を設定するために呼ぶ関数
    /// </summary>
    public void PrepareClipToPlayDirector()
    {
        InitProperty();
        SetClips();
        SetClipReviveList();
    }

    /// <summary>
    /// timelineを再生する前にクリップ情報を設定するために呼ぶ関数(muteになっていないトラック内で使いたくないクリップがある場合)
    /// </summary>
    public void PrepareClipToPlayDirector(List<int> playClipIndexes = null)
    {
        InitProperty();
        SetClips();
        SetAllClipPlayed(true);
        SetClipPlayedByIndex(playClipIndexes, false);
        SetClipReviveList();
    }
    
    /// <summary>
    /// 登録されているクリップの終わりまで移動して再開する
    /// </summary>
    public void SkipToClipEnd()
    {
        if (director == null || director.playableAsset == null) { return; }

        if (targetClip == null) { return; }

        var loopClip = (LoopClip)targetClip.asset;
        loopClip.IsPlayed = true;
        director.time = targetClip.end;
        director.Resume();
    }

    /// <summary>
    /// インデックスから再生済みフラグを設定する
    /// </summary>
    public void SetClipPlayedByIndex(int index, bool isPlayed)
    {
        foreach (var key in clipDict.Keys)
        {
            foreach (var c in clipDict[key])
            {
                var lc = (LoopClip)c.asset;
                if (lc.ClipIndex == index)
                {
                    lc.IsPlayed = isPlayed;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 複数指定のインデックスから再生済みフラグを設定する
    /// </summary>
    public void SetClipPlayedByIndex(List<int> indexes, bool isPlayed)
    {
        if (indexes == null || indexes.Count == 0) { return; }

        foreach (var index in indexes)
        {
            SetClipPlayedByIndex(index, isPlayed);
        }
    }
    #endregion

    #region private関数
    /// <summary>
    /// 最初から再生したいときに呼ぶことで初期化する
    /// </summary>
    private void InitProperty()
    {
        isTrackEnd = false;
        targetClip = null;
        clipDict.Clear();
    }

    /// <summary>
    /// トラックとクリップを登録する
    /// </summary>
    private void SetClips()
    {
        if (director == null) { director = GetComponent<PlayableDirector>(); }
        if (director.playableAsset == null) { return; }

        var outputs = director.playableAsset.outputs;
        List<TrackAsset> tracks = new List<TrackAsset>();
        foreach (var o in outputs)
        {
            if (o.sourceObject.GetType() == typeof(LoopTrack)) { tracks.Add((TrackAsset)o.sourceObject); }
        }

        foreach (var t in tracks)
        {
            ClearClips(t);
            foreach (var c in t.GetClips())
            {
                if (t.muted) { ((LoopClip)c.asset).IsPlayed = true; }
                else { ((LoopClip)c.asset).IsPlayed = false; }
                AddClip(t, c);
            }
        }
    }

    /// <summary>
    /// 最も手前で処理されるクリップを取得
    /// </summary>
    private TimelineClip GetMostPreviousClip()
	{
		TimelineClip resClip = null;
		double endTime = Mathf.Infinity;
		foreach (var key in clipDict.Keys)
		{
			foreach (var c in clipDict[key])
			{
				var lc = (LoopClip)c.asset;
				if (lc.IsPlayed) { continue; }

				var time = GetEndTime(c);
				//ループの尻(ポーズの頭)が早いものから再生されるので、一番早いものを取得する
				if (time < endTime) { resClip = c; endTime = time; }
				//ループの尻(ポーズの頭)が同じものは短いクリップ優先
				else if (time == endTime)
				{
					if (c.start > resClip.start) { resClip = c; endTime = time; }
					//頭も同じ場合はトラックのインデックスが若いもの優先
					else if (c.start == resClip.start)
					{
						var index = ((LoopTrack)c.parentTrack).GetIndex;
						var preIndex = ((LoopTrack)resClip.parentTrack).GetIndex;
						if (index < preIndex) { resClip = c; endTime = time; }
					}
				}
			}
		}

		return resClip;
	}

	/// <summary>
	/// mixerでの各処理が終了する時間を取得
	/// </summary>
	private double GetEndTime(TimelineClip c)
	{
		var lc = (LoopClip)c.asset;
		double endTime = 0;
		switch (lc.GetControlType)
		{
			case ETimelineControlType.Loop:
				endTime = c.end - lc.GetOffsetSecond;
				break;
			case ETimelineControlType.Pause:
				endTime = c.start;
				break;
			case ETimelineControlType.Skip:
            case ETimelineControlType.AutoSkip:
                endTime = c.end;
				break;
		}

		return endTime;
	}

	/// <summary>
	/// ループした場合はループ内にあった他処理を復活させるのでリストを更新する
	/// </summary>
	private void SetClipReviveList()
	{
        #if UNITY_EDITOR
		if (!Application.isPlaying) { return; }
        #endif

		foreach (var key in clipDict.Keys)
		{
			foreach (var c in clipDict[key])
			{
				var lc = (LoopClip)c.asset;
				lc.ClearReviveList();
				if (lc.GetControlType != ETimelineControlType.Loop) { continue; }

				var end = GetEndTime(c);
				var start = c.start;

				foreach (var c_key in clipDict.Keys)
				{
					foreach (var comparison in clipDict[c_key])
					{
                        if (((LoopClip)comparison.asset).IsPlayed) { continue; }
						if (comparison == c) { continue; }

						var compareEnd = GetEndTime(comparison);
						//ループの尻がかぶっていないならcontinue
						if (compareEnd <= start || compareEnd > end) { continue; }
						//終了時間が同じ場合
						else if (compareEnd == end)
						{
							//まったく同じクリップは下のトラックが復活させる
							if (comparison.start == start)
							{
								var index = ((LoopTrack)c.parentTrack).GetIndex;
								var compareIndex = ((LoopTrack)comparison.parentTrack).GetIndex;
								if (index > compareIndex) { lc.AddReviveList((LoopClip)comparison.asset); }
							}
							//基本は長いクリップが復活させる
							else if (comparison.start > start)
							{
								lc.AddReviveList((LoopClip)comparison.asset);
							}
						}
						//ループの尻が遅いほうが復活させる
						else { lc.AddReviveList((LoopClip)comparison.asset); }
					}
				}
			}
		}
	}

    /// <summary>
    /// すべてのクリップの再生済みフラグを設定する
    /// </summary>
    private void SetAllClipPlayed(bool isPlayed)
    {
        foreach (var key in clipDict.Keys)
        {
            foreach (var c in clipDict[key])
            {
                var lc = (LoopClip)c.asset;
                lc.IsPlayed = isPlayed;
            }
        }
    }

    /// <summary>
    /// 指定されたクリップのインデックスを設定する
    /// </summary>
    private void SetClipIndex(TimelineClip clip)
    {
        int count = 0;
        foreach (var key in clipDict.Keys)
        {
            foreach (var c in clipDict[key])
            {
                if (c == clip)
                {
                    ((LoopClip)clip.asset).ClipIndex = count;
                    return;
                }
                count++;
            }
        }
    }
    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(LoopTimelineClipManager))]
public class LoopTimelineClipManagerEditor : Editor
{
	LoopTimelineClipManager manager = null;

	private void OnEnable()
	{
		manager = target as LoopTimelineClipManager;
        if (!Application.isPlaying)
        {
            manager.PrepareClipToPlayDirector();
        }
	}

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Play"))
            {
                manager.PlayTimeline();
            }
            if (GUILayout.Button("Skip"))
            {
                manager.SkipToClipEnd();
            }
            if (GUILayout.Button("ReturnTop"))
            {
                manager.ReturnTop();
            }
            EditorGUI.EndDisabledGroup();
        }

        foreach (var key in manager.GetClipDict.Keys)
        {
            EditorGUILayout.LabelField(key.name);

            EditorGUI.indentLevel++;
            foreach (var c in manager.GetClipDict[key])
            {
                var lc = (LoopClip)c.asset;
                lc.IsPlayed = EditorGUILayout.ToggleLeft(c.displayName, lc.IsPlayed);
            }
            EditorGUI.indentLevel--;
        }
	}
}
#endif