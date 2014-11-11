#region Header
/**
 *  Cothread for unity3d
 *  Author: seewind
 *  https://github.com/seewindcn/unity-async
 *  email: seewindcn@gmail.com
 **/
#endregion

using UnityEngine;
using System;
using System.Collections;

using Cothread;

public class CoHub: MonoBehaviour {
	public UnityHub Hub { get { return (UnityHub)UnityHub.Instance; } }

    public void Start() {
    	UnityHub.Install(this);
    }
}

public class UnityHub: CothreadHub {
	public static MonoBehaviour Active;

	public static CothreadHub Install(MonoBehaviour active) {
		Active = active;
		if (Instance != null)
			return Instance;
		var Hub = new UnityHub();
		CothreadHub.Instance = Hub;
		Hub.Start();
		return Hub;
	}

	public new void Start() {
		if (!Stoped)
			throw new SystemException("startLoop");
		base.Start();
		CothreadHub.LogHandler = Debug.Log;
		BusyTickTime = 0.00001f;
		RegisterU3d();
		Active.StartCoroutine(loop());
	}

	IEnumerator loop() {
		double sleepTime;
		while (!Stoped) {
			sleepTime = Tick();
			if (sleepTime > 0.001f)
				yield return new WaitForSeconds((float)sleepTime);
		}
	}

	public new void Stop() {
		if (Stoped)
			return;
		base.Stop();
		UnRegisterU3d();
	}

	public void test(int count) {
		var tc = new CothreadTestCase();
		tc.Start();
		tc.test(count);
	}


	#region 扩展支持u3d
	void RegisterU3d() {
		CothreadHub.GlobalAsyncHandle = new CothreadHub.AsyncHandler(u3dAsyncCheck);
	}

	void UnRegisterU3d() {
		CothreadHub.GlobalAsyncHandle = null;
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
		addCallback(ie);
		Active.StartCoroutine(_u3dAsyncHandle(current, ie));
		yield return CothreadHub.YIELD_CALLBACK;
	}

	IEnumerator _u3dAsyncHandle(IEnumerator curIE, IEnumerator ie) {
		var cur = ie.Current;
		yield return cur;
		if (ie.Current == cur)
			addCothread(curIE);
	}

	#endregion

}
