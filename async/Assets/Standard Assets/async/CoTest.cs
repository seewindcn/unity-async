using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Cothread {
	public class CothreadTestCase {
		public CothreadHub hub;
		public void Start() {
			hub = NormalHub.Install();
		}
		
		public void test(int count) {
			hub.StartCoroutine(testTimeout1());
			hub.StartCoroutine(testTimeout2());
			testEvent(count);
			testSock(count);
			hub.StartCoroutine(testU3d(count));
		}
		
		IEnumerator testTimeout1() {
			var timeout = CothreadTimeout.NewWithStart(1005);
			yield return hub.Sleep(1000);
			try {
				timeout.Cancel(true);
				CothreadHub.Log("[testTimeout1] ok");
			} catch ( CothreadTimeoutError ) {
				CothreadHub.Log("[testTimeout1] error");
			}
		}
		IEnumerator testTimeout2() {
			var timeout = CothreadTimeout.NewWithStart(1005);
			yield return hub.Sleep(1010);
			try {
				timeout.Cancel(true);
				CothreadHub.Log("[testTimeout2] CothreadTimeoutError error");
			} catch ( CothreadTimeoutError ) {
				CothreadHub.Log("[testTimeout2] CothreadTimeoutError ok");
			}
		}
		
		void testEvent(int count) {
			CothreadEvent ev, nev, bev;
			bev = ev = new CothreadEvent();
			for (int i = 0; i < count; i++) {
				if (i+1 == count) {
					nev = null;
				} else {
					nev = new CothreadEvent();
				}
				hub.StartCoroutine(testEvent1(i+1, ev, nev));
				ev = nev;
			}
			bev.Set("event");
		}
		IEnumerator testEvent1(int i, CothreadEvent ev, CothreadEvent nev) {
			yield return ev.Wait(0);
			var result = (string)ev.Get("");
			var msg = string.Format("{0} {1} ->", result, i.ToString());
			if (nev != null) {
				nev.Set(msg);
			} else {
				CothreadHub.Log("result: " + msg);
			}
		}


		//socket
		void testSock(int count) {
			for (int i=0; i<count; i++) {
				hub.StartCoroutine(testSock1(i));
			}
		}

		IEnumerator testSock1(int i) {
			string stri = i.ToString();
			CothreadHub.Log("testSock:" + stri);
			CothreadTimeout timeout = CothreadTimeout.NewWithStart(5000);
			CothreadSocket sock = new CothreadSocket();
			yield return sock.Connect("127.0.0.1", 8000);  //web server
			//yield return sock.Connect("www.baidu.com", 80);  //www.baidu.com
			//yield return sock.Connect("115.239.210.27", 80);  //www.baidu.com
			//yield return hub.Sleep(rt);
			//CothreadHub.Log(stri + "-socket connected:" + sock.Connected);
			
			yield return sock.SendString("GET / HTTP/1.0\n\n");
			//CothreadHub.Log(stri + "-Send ok");
			var recvData = new CothreadSocketRecvData(2048);
			var result = new CothreadResult();

			yield return hub.Sleep(1000);
			yield return sock.RecvString(recvData, result);
			string s1 = (string)result.Result;
			if (s1.Length <= 0) 
				CothreadHub.Log("testSock(" + stri +") error" + "-passTime:" + timeout.PassTime.ToString());
			else
				CothreadHub.Log("testSock(" + stri +") ok:" + s1.Length.ToString()  + "  data:" + s1.Substring(0, Math.Min(500, s1.Length)));
			try {
				timeout.Cancel(true);
			} catch (CothreadTimeoutError){
				CothreadHub.Log(stri + "-testSock timeout");
			} finally {
				sock.Close();
			}
		}


		//u3d
		IEnumerator testU3d(int count) {
			var w1 = new WWW("http://www.baidu.com");
			yield return w1;
			if (!w1.isDone)
				CothreadHub.Log("[testU3d] www error");
			else
				CothreadHub.Log("[testU3d] www ok: size:" + w1.text.Length.ToString());

			var result = new CothreadResult();
			var u1 = CoHub.Active.StartCoroutine(_u3dCoroutine1(result));
			yield return u1;
			if (result.Result.Equals(true))
				CothreadHub.Log("[testU3d]StartCoroutine ok");
			else
				CothreadHub.Log("[testU3d]StartCoroutine error");
		}

		IEnumerator _u3dCoroutine1(CothreadResult result) {
			yield return new WaitForSeconds(1);
			result.Result = true;
		}

	}

}








//////////////////////////////
//////////////////////////////















