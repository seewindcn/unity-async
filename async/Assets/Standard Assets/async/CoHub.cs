using UnityEngine;
using System;
using System.Collections;

using Cothread;

public class CoHub: MonoBehaviour {
	public static CothreadBaseHub Hub;
	public static CoHub Active;

	public bool Started;

	public void Install() {
		if (Active != null && Active.Started)
			return;
		if (Hub == null) {
			Hub = new CothreadBaseHub();
			CothreadBaseHub.Instance = Hub;
		} 
		Hub.LogHandler += Debug.Log;
		startLoop();
	}

	void startLoop() {
		Started = true;
		Active = this;
		Hub.BusyTickTime = 0.00001f;
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

}
