import System
import System.Collections
import UnityEngine

import Async

class UnityHub(Hub):
	public static def Init(behaviour as MonoBehaviour) as UnityHub:
		hub = Hub.Default() cast UnityHub
		if hub != null:
			if hub.Stoped:
				hub.StartLoop(behaviour)
			return hub
		#Debug.Log("Init")
		h = UnityHub()
		h.SetDefault()
		h.StartLoop(behaviour)
		return h

	def StartLoop(behaviour as MonoBehaviour):
		if not self.Stoped:
			return
		if behaviour == null:
			behaviour = Camera.main as MonoBehaviour
		self.Stoped = false
		behaviour.StartCoroutine(self.loop())
		#behaviour.OnDestroy += self.OnDestroy


	public def loop() as IEnumerator:
		while not self.Stoped:
			if not self.tick():
				yield self.BusyTime
				#Debug.Log("loop")
			else:
				yield self.onIdle()

	public def onIdle() as IEnumerator:
		yield WaitForSeconds(self.IdleTime)

	def OnDestroy():
		self.Stop()

	public override def print(*args):
		msg = join(args)
		Debug.Log(msg)



class uasync(MonoBehaviour):
	msg as string = "数据"
	hub as UnityHub
	def Start():
		Debug.Log("start")
		hub = UnityHub.Init(self)

	def test_sock() as IEnumerable:
		s = AsyncSocket()
		yield s.Connect("119.146.200.16", 80)
		Debug.Log("socket connected:" + s.Connected)
		yield s.SendString("GET\n")
		Debug.Log("Send ok")
		rs = (0, array(byte, 2048))
		yield s.RecvString(rs)
		Debug.Log("Recv:" + (rs[1] cast string))
		self.msg = rs[1]

	def OnGUI():
		if GUI.Button(Rect(10, 10, 90, 30), "连接网络"):
			Debug.Log(hub)
			hub.StartCoroutine(test_sock())
		if GUI.Button(Rect(10, 50, 90, 30), "换场景"):
			if Application.loadedLevelName == "scene1":
				Application.LoadLevel("scene2")
			else:
				Application.LoadLevel("scene1")
		
		GUI.TextArea(Rect(10, 100, 280, 120), self.msg)

	def OnDestroy():
		hub.Stop()


