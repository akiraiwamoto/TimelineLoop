using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum ETimelineControlType
{
    Loop,
    Pause,
    Skip,
    AutoSkip,
}

[Serializable]
public class LoopClip : PlayableAsset, ITimelineClipAsset
{
    private const int fps = 30;

    [SerializeField]
    private ETimelineControlType controlType;
    public ETimelineControlType GetControlType { get { return controlType; } }
    [SerializeField, Header("loopEnd = end - offset")]
    private int offsetFrame = 0;
    public double GetOffsetSecond { get { return (double)offsetFrame / fps; } }
    
    public bool IsPlayed { get; set; }

    private List<LoopClip> reviveClips = new List<LoopClip>();
    public void AddReviveList(LoopClip clip) { reviveClips.Add(clip); }
    public void ClearReviveList() { reviveClips.Clear(); }
    public void ReviveAll()
    {
        if (reviveClips == null || reviveClips.Count == 0) { return; }
        foreach (var c in reviveClips) { c.IsPlayed = false; }
    }

    public int ClipIndex { get; set; }

    /// <summary>
    /// LoopClip自身はループ等させたくないのでClipCapsはNone
    /// </summary>
	public ClipCaps clipCaps
	{
		get { return ClipCaps.None; }
	}

    /// <summary>
    /// behaviourの作成
    /// </summary>
	public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<LoopBehaviour>.Create(graph);

        return playable;
    }
}
