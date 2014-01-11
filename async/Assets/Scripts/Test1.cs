﻿using System;
using System.Collections;
using UnityEngine;
using Async;


public class Test1:MonoBehaviour {
    static string CR = "\r\n";
    string msg = "";
    int count = 8;
    UnityHub hub;
    public void Start() {
        Debug.Log("start");
        hub = UnityHub.Init(this);
    }


    IEnumerable testSock(int i) {
        msg += "testSock:" + i.ToString() + CR;
        int rt = (int)UnityEngine.Random.Range(100.0F, 1000.0F);
        AsyncTimeout timeout = AsyncTimeout.WithStart(100000);
        AsyncSocket s = new AsyncSocket();
        yield return s.Connect("119.146.200.16", 80);
        yield return hub.Sleep(rt);
        Debug.Log("socket connected:" + s.Connected);

        yield return s.SendString("GET\n");
        Debug.Log("Send ok");
        object[] rs = new object[2];
        rs[0] = 0;
        rs[1] = new byte[2048];

        yield return hub.Sleep(rt);
        yield return s.RecvString(rs);
        //Debug.Log("Recv:" + (string)rs[1]);
        string[] ss = ((string)rs[1]).Split(' ');
        msg += "testSock(" + i.ToString() +") ok:" + ss[0] + " " + ss[1] + "\n";
        try {
            timeout.Cancel(true);
        } catch (TimeoutError ){
            msg += "testSock timeout" + CR;
        }
    }


    IEnumerable testTimeout() {
        AsyncTimeout timeout = AsyncTimeout.WithStart(100);
        yield return hub.Sleep(200);
        timeout.Cancel(false);
        if (timeout.Timeout) {
            msg += "testTimeout ok" + CR;
        }
    }

    IEnumerable testEvent(int i, AsyncEvent ev, AsyncEvent nev) {
        object[] result = new object[1];
        yield return ev.Get(0, result);
        Debug.Log("bbbbb");
        msg += i.ToString() + " ->";
        if (nev != null) {
            nev.Set((string)result[0]);
        } else {
            msg += "result:" + (string)result[0] + CR;
        }
    }

    void OnGUI() {
        int y = 10;
        int x = 10;

        GUI.Label(new Rect(10, y, 90, 30), "count:" + count.ToString());
		count = (int)GUI.HorizontalSlider(new Rect(100, y, 90, 30), count, 1, 100);
        if (GUI.Button(new Rect(200, y, 90, 30), "Clean")) {
            msg = "";
        }
        if (GUI.Button(new Rect(300, y, 90, 30), "ChScene")) {
            if (Application.loadedLevelName == "scene1") {
                Application.LoadLevel("scene2");
            }
            else {
                Application.LoadLevel("scene1");
            }
        }

        y += 40;
        x = 0;
        if (GUI.Button(new Rect(10, y, 90, 30), "TimeoutTest")) {
            hub.StartCoroutine(testTimeout());
        }
        x += 100;
        if (GUI.Button(new Rect(x, y, 90, 30), "EventTest")) {
            AsyncEvent ev, nev, bev;
            bev = ev = new AsyncEvent();
            for (int i = 0; i < count; i++) {
                if (i+1 == count) {
                    nev = null;
                } else {
                    nev = new AsyncEvent();
                }
                hub.StartCoroutine(testEvent(i+1, ev, nev));
                ev = nev;
            }
            bev.Set("event");
            Debug.Log("aaaaaa");
        }
        x += 100;
        if (GUI.Button(new Rect(x, y, 90, 30), "SocketTest")) {
            for (int i = 0; i < count; i++) {
                hub.StartCoroutine(testSock(i+1));
            }
        }
        //y += 40;
        
        y += 50;
        GUI.TextArea(new Rect(10, y, 280, 120), msg);
    }

    void OnDestroy() {
        hub.Stop();
    }

}
