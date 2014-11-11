using UnityEngine;
using System;
using System.Collections;

using Cothread;

public class CoHub: MonoBehaviour {
	public static CothreadHub Hub;
	public static CoHub Active;

	public bool Started;

	public void Install() {
		if (Active != null && Active.Started)
			return;
		if (Hub == null) {
			Hub = new CothreadHub();
			CothreadHub.Instance = Hub;
		} 
		CothreadHub.LogHandler = Debug.Log;
		startLoop();
	}

	void startLoop() {
		Started = true;
		Active = this;
		Hub.BusyTickTime = 0.00001f;
		RegisterU3d();
		StartCoroutine(loop());
	}

	IEnumerator loop() {
		double sleepTime;
		while (Started) {
			sleepTime = Hub.Tick();
			if (sleepTime > 0.001f)
				yield return new WaitForSeconds((float)sleepTime);
		}
	}

    public void Start() {
    	Install();
    }

	public void Stop() {
		if (Active != this)
			return;
		Started = false;
		Active = null;
	}

	public void test(int count) {
		var tc = new CothreadTestCase();
		tc.Start();
		tc.test(count);
	}


	#region 扩展支持u3d
	public void RegisterU3d() {
		CothreadHub.GlobalAsyncHandle = new CothreadHub.AsyncHandler(u3dAsyncCheck);
	}

	object u3dAsyncCheck(IEnumerator ie) {
		if (ie.Current.GetType().IsSubclassOf(typeof(YieldInstruction)) 
		    || ie.Current is WWW 
		    || ie.Current is WWWForm
		    || ie.Current is Coroutine) {
			return u3dAsyncHandle(ie);
		}
		return null;
	}

	IEnumerable u3dAsyncHandle(IEnumerator ie) {
		Hub.addCallback(ie);
		StartCoroutine(_u3dAsyncHandle(Hub.current, ie));
		yield return CothreadHub.YIELD_CALLBACK;
	}

	IEnumerator _u3dAsyncHandle(IEnumerator curIE, IEnumerator ie) {
		var cur = ie.Current;
		yield return cur;
		if (ie.Current == cur)
			Hub.addCothread(curIE);
	}

	#endregion

}
