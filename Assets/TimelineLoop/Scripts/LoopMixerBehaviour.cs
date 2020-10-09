using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

public class LoopMixerBehaviour : PlayableBehaviour
{
    private IEnumerable<TimelineClip> clips;
    public IEnumerable<TimelineClip> Clips { set { clips = value; } }

    private LoopTimelineClipManager manager;
    public LoopTimelineClipManager SetManager { set { manager = value; } }

    private TrackAsset track;
    public TrackAsset SetTrack { set { track = value; } }

    private PlayableDirector director;

    public override void OnPlayableCreate(Playable playable)
    {
        director = playable.GetGraph().GetResolver() as PlayableDirector;
    }
    
    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        //自動設定が有効なときはマネージャーの初期処理を行う
        if (director.time > 0) { return; }
        if (manager == null) { return; }
        if (!manager.AutoClipSet) { return; }
        manager.PrepareClipToPlayDirector();
    }

    /// <summary>
    /// 次のクリップの取得とループ(ポーズ)処理を行う
    /// </summary>
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) { return; }
        #endif

        if (clips == null) { return; }
        if (manager == null) { return; }

        var timelineClip = manager.GetTargetClip;
        if (timelineClip == null || timelineClip.parentTrack != track) { return; }
        
        var loopClip = (LoopClip)timelineClip.asset;

        //ループ(ポーズ)処理開始
        var time = director.time;
        switch (loopClip.GetControlType)
        {
            case ETimelineControlType.Loop:
                if (time < timelineClip.end - loopClip.GetOffsetSecond) { return; }
                director.time = timelineClip.start;
                //ループ時は復活もさせる
                loopClip.ReviveAll();
                manager.SetTargetClip();
                break;
            case ETimelineControlType.Pause:
                if (time < timelineClip.start) { return; }
                director.time = timelineClip.start;
                director.Pause();
                break;
            //スキップ機能のみ使う場合はスキップしなかったときにIsPlayedをtrueにする
            case ETimelineControlType.Skip:
                if (time < timelineClip.end) { return; }
                loopClip.IsPlayed = true;
                break;
            case ETimelineControlType.AutoSkip:
                if (time < timelineClip.start) { return; }
                director.time = timelineClip.end;
                loopClip.IsPlayed = true;
                break;
        }
    }
}
