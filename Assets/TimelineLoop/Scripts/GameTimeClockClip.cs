using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class GameTimeClockClip : PlayableAsset, ITimelineClipAsset
{
	public ClipCaps clipCaps { get { return ClipCaps.None; } }

	private Dictionary<int, System.Action<double>> callBackDict = new Dictionary<int, System.Action<double>>();

	/// <summary>
	/// コールバックの登録
	/// </summary>
	/// <param name="callBack">登録するコールバック</param>
	/// <returns>登録したCBのID</returns>
	public int SetCB(System.Action<double> callBack)
	{
		var randValue = Random.Range(0, 2147483647);
		if (callBackDict == null)
		{
			callBackDict = new Dictionary<int, System.Action<double>>() { { randValue, callBack }, };
			return randValue;
		}
		else
		{
			while (callBackDict.ContainsKey(randValue)) { randValue = Random.Range(0, 2147483647); }
			callBackDict.Add(randValue, callBack);
			return randValue;
		}
	}

	/// <summary>
	/// コールバックの削除
	/// </summary>
	/// <param name="id">削除したいCBのID</param>
	public void RemoveCB(int id)
	{
		if (callBackDict == null || callBackDict.Count == 0) { return; }
		if (callBackDict.ContainsKey(id))
		{
			callBackDict.Remove(id);
		}
	}

	/// <summary>
	/// CBのクリア
	/// </summary>
	public void ClearCB()
	{
		callBackDict.Clear();
	}

	public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
	{
		var playable = ScriptPlayable<GameTimeClockBehaviour>.Create(graph);
		var behaviour = playable.GetBehaviour() as GameTimeClockBehaviour;
		behaviour.clip = this;
		return playable;
	}

	/// <summary>
	/// 全CBの実行
	/// </summary>
	/// <param name="deltaTime">前フレームからの経過時間</param>
	public void DoAllCB(double deltaTime)
	{
		if (callBackDict == null || callBackDict.Count == 0) { return; }
		foreach (var key in callBackDict.Keys)
		{
			callBackDict[key]?.Invoke(deltaTime);
		}
	}
}