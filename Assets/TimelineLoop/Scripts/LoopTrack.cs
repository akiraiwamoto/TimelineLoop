using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.2794118f, 0.7117646f)]
[TrackClipType(typeof(LoopClip))]
public class LoopTrack : TrackAsset
{
	private int trackIndex = 0;
	public int GetIndex { get { return trackIndex; } }

	/// <summary>
	/// mixerの作成とクリップ名の変更
	/// </summary>
	public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
	{
		var clips = GetClips();

#if UNITY_EDITOR
		if (!Application.isPlaying)
		{
			if (clips != null)
			{
				foreach (var c in clips)
				{
					//分かりやすいようにクリップ名を変更する
					var lc = (LoopClip)c.asset;
					var name = "Clip";
					var clipIndex = lc.ClipIndex;
					switch (lc.GetControlType)
					{
						case ETimelineControlType.Loop:
							name = "LoopClip";
							break;
						case ETimelineControlType.Pause:
							name = "PauseClip";
							break;
						case ETimelineControlType.Skip:
							name = "SkipClip";
							break;
						case ETimelineControlType.AutoSkip:
							name = "AutoSkip";
							break;
					}
					c.displayName = string.Format("{0}_{1}", clipIndex, name);
				}
			}
		}
#endif

		trackIndex = GetTrackIndex();

		//mixerを作成してクリップ等を渡す
		var mixer = ScriptPlayable<LoopMixerBehaviour>.Create(graph, inputCount);
		//var behaviour = mixer.GetBehaviour();
		return mixer;
	}

	/// <summary>
	/// トラックのインデックスを取得する
	/// </summary>
	/// <returns>トラックのインデックス</returns>
	private int GetTrackIndex()
	{
		var root = this.parent as TimelineAsset;
		var group = this.GetGroup();
		while (group != null)
		{
			root = group.parent as TimelineAsset;
			group = group.GetGroup();
		}
		var count = root.outputTrackCount;
		int res = 0;

		for (int i = 0; i < count; i++)
		{
			if (root.GetOutputTrack(i) == this) { res = i; }
		}
		return res;
	}
}