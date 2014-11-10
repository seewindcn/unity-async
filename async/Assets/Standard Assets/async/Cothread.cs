using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace Cothread {
	public class CothreadError : Exception {
		public string Msg;
		public CothreadError(string msg) {
			Msg = msg;
		}

		public override string ToString() {
			return Msg;
		}
	}

	public class CothreadTimeoutError : CothreadError {
		public CothreadTimeoutError(string msg):base(msg) {
		}
	}

	public class YieldState {
		public IEnumerator IE { get; set;} 
		public DateTime WakeTime { get; set; }
		
		public YieldState(IEnumerator ie, DateTime wakeTime) {
			IE = ie;
			WakeTime = wakeTime;
		}
		
		public override string ToString() {
			return string.Format("Cothread.YieldState<{0}>(wakeTime={1}", 
			                     GetHashCode(), WakeTime);
		}
	}

	public class CothreadResult {
		public object Result;
	}

	public class CothreadBaseHub {
		#region 属性定义
		[ThreadStatic]
		public static CothreadBaseHub Instance;

		public static object YIELD_CALLBACK = new object();

		public Action<object> LogHandler;
		public float IdleTickTime = 0.1f;
		public float BusyTickTime = 0.01f;

		public bool Stoped { get; set; }

		public int MainThreadID {
			get { return MainThreadID;}
		}
		protected int mainThreadID = Thread.CurrentThread.ManagedThreadId;

		public Cothread CurrentCothread {
			get { return GetCothread(current); }
		}

		internal protected IEnumerator current;
		protected object locker = new object();
		Dictionary<IEnumerator, Cothread> threads = new Dictionary<IEnumerator, Cothread>();
		List<IEnumerator> yields = new List<IEnumerator>();
		protected List<YieldState> times = new List<YieldState>();
		int cbIndex;
		AsyncCallback cb;

		#endregion

		public CothreadBaseHub() {
			Stoped = true;
		}

		#region 公共方法
		public Cothread GetCothread(IEnumerator ie) {
			if (threads.ContainsKey(ie))
				return threads[ie];
			return null;
		}

		public void Start() {
			Stoped = false;
			cb = new AsyncCallback(callBack);
		}
		public void Stop() {
			Stoped = true;
		}

		public Cothread StartCoroutine(IEnumerator routine) {
			var iee = callIEnumerable(routine);
			var ie = iee.GetEnumerator();
			var th = new Cothread(ie);
			addCothread(ie);
			threads[ie] = th;
			return th;
		}

		public Cothread StartCoroutine(int ms, IEnumerator routine) {
			var iee = callIEnumerable(routine);
			var ie = iee.GetEnumerator();
			var th = new Cothread(ie);
			addTimeUp(ms * TimeSpan.TicksPerMillisecond, ie);
			threads[ie] = th;
			return th;
		}

		public IEnumerable Sleep(int ms) {
			if (current == null) {
				throw new CothreadError("hup.Sleep only can be call from coroutine");
			}
			addTimeUp(ms * TimeSpan.TicksPerMillisecond, current);
			yield return CothreadBaseHub.YIELD_CALLBACK;
		}

		public IEnumerator Join(IEnumerator ie, double timeout) {
			var th = GetCothread(ie);
			if (th == null)
				return null;
			return th.Join(timeout);
		}

		public virtual void Log(object msg) {
			if (LogHandler != null)
				LogHandler(msg);
		}

		#endregion

		#region 私有方法
		protected int nextIndex() {
			cbIndex += 1;
			return cbIndex;
		}

		protected Cothread delCothread(IEnumerator ie) {
			if (threads.ContainsKey(ie)) {
				var rs = threads[ie];
				threads.Remove(ie);
				return rs;
			}
			return null;
		}

		private void callBack(IAsyncResult ar) {
			lock (locker) {
				var ie = (IEnumerator)ar.AsyncState;
				var th = GetCothread(ie);
				if (th.AsyncResult != ar)
					return;
				th.AsyncResult = null;
				addCothread(ie);
			}
		}

		internal protected void addCothread(IEnumerator ie) {
			lock (locker) {
				var th = GetCothread(ie);
				if (th != null) 
					th.AsyncResult = null;
				yields.Add(ie);
			}
		}

		internal protected void addCothreads(List<IEnumerator> ies) {
			lock (locker) {
				Cothread th;
				foreach (var ie in ies) {
					th = GetCothread(ie);
					if (th != null)
						th.AsyncResult = null;
					yields.Add(ie);
				}
			}
		}

		protected IEnumerator popYield() {
			lock (locker) {
				if (yields.Count > 0) 
				{
					var rs = yields[0];
					yields.RemoveAt(0);
					return rs;
				}
				return null;
			}
		}

		protected void addCallback(IEnumerator ie) {
			lock (locker) {
				var th = GetCothread(ie);
				if (th == null) {
					return;
				}
				if (th.AsyncResult != null) {
					throw new CothreadError("addCallback error: yield exist");
				}
			}
			return;
		}

		internal protected YieldState addTimeUp(long time, IEnumerator ie) {
			var t = new TimeSpan(time);
			var state = new YieldState(ie, DateTime.Now + t);
			times.Add(state);
			times.Sort((y1, y2)=> {
				if (y1.WakeTime < y2.WakeTime)
					return -1;
				if (y1.WakeTime > y2.WakeTime)
					return 1;
				return 0;
			});
			return state;
		}

		protected void call(IEnumerator ie) {
			current = ie;
			var ok = ie.MoveNext();
			current = null;
			var th = GetCothread(ie);
			if (!ok) {
				if (th != null) {
					th.Close();
					delCothread(ie);
				}
				return;
			}

			if (ie.Current == CothreadBaseHub.YIELD_CALLBACK)
				addCallback(ie);
			else if (ie.Current is IAsyncResult) {
				addCallback(ie);
				th.AsyncResult = (IAsyncResult)ie.Current;
			} else 
				addCothread(ie);
		}

		public double Tick() {
			var len = yields.Count;
			IEnumerator ie;
			for (int i=0; i < len; i++) {
				ie = popYield();
				if (ie == null) 
					break;
				call(ie);
			}

			YieldState state;
			while (times.Count > 0 && times[0].WakeTime <= DateTime.Now) {
				state = times[0]; 
				times.RemoveAt(0);
				call(state.IE);
			}

			double sleepTime;
			if (yields.Count > 0)
				sleepTime = BusyTickTime;
			else
				sleepTime = IdleTickTime;
			if (times.Count > 0)
				sleepTime = Math.Min(sleepTime, (times[0].WakeTime - DateTime.Now).TotalSeconds);

			return sleepTime;
		}

		static IEnumerable callIEnumerable(IEnumerable iee) {
			return callIEnumerable(iee.GetEnumerator());
		}

		static IEnumerable callIEnumerable(IEnumerator ie) {
			IEnumerable rss;
			while (ie.MoveNext()) {
				if (ie.Current is IEnumerable)
					rss = callIEnumerable(ie.Current as IEnumerable);
				else if (ie.Current is IEnumerator) 
					rss = callIEnumerable(ie.Current as IEnumerator);
				else
					rss = null;

				if (rss != null) {
					foreach (var i in rss)
						yield return i;
				} else
					yield return ie.Current;
			}
		}

		#endregion

		public static void test1() {
			callIEnumerable(let1());
		}

		private static IEnumerable let1() {
			yield return 1;
			yield return 2;
		}

	}

	public class NormalHub: CothreadBaseHub {
		public static CothreadBaseHub Install() {
			if (Instance != null) 
				return Instance;
			Instance = new NormalHub();
			return Instance;
		}

		public void Loop() {
			Stoped = false;
			while (!Stoped) {
				double sleepTime = Tick();
				Thread.Sleep((int)(sleepTime * TimeSpan.TicksPerMillisecond));
			}
		}
	}

	public class Cothread {
		public IEnumerator IE { get; set;}
		public bool Closed { get; set; }
		public IAsyncResult AsyncResult;
		public CothreadTimeout Timeout;

		CothreadEvent ev;

		public Cothread(IEnumerator ie) {
			IE = ie;
			Closed = false;
			ev = null;
		}

		public void Close() {
			Closed = true;
		}

		public IEnumerator Join(double timeout) {
			return ev.Wait(timeout);
		}

	}


	public class CothreadEvent: IEnumerator {
		public object Current { get; set;}

		private static object NULL = new object();
		private List<IEnumerator> yields = new List<IEnumerator>();

		public CothreadEvent() {
			Current = NULL;
		}

		public void Clear() {
			Current = NULL;
			yields.Clear();
		}

		public bool MoveNext() {
			var hub = CothreadBaseHub.Instance;
			if (hub.current != this)
				throw new CothreadError("Can no call from other Cothread");
			hub.addCothreads(yields);
			yields.Clear();
			return false;
		}

		public void Reset() {
			Clear();
		}

		public IEnumerator Wait(double timeout) {
			if (Current != NULL) {
				yield return Current;
				yield break;
			}
			CothreadTimeout t=null;
			yields.Add(CothreadBaseHub.Instance.current);
			if (timeout > 0)
				t = CothreadTimeout.NewWithStart(timeout);
			yield return CothreadBaseHub.YIELD_CALLBACK;
			if (timeout > 0)
				t.Cancel(false);
		}

		public IEnumerator Get(int timeout, CothreadResult result) {
			yield return Wait(timeout);
			if (Current != NULL) 
				result.Result = Current;
		}

		public void Set(object v) {
			Current = v;
			if (yields.Count > 0) 
				CothreadBaseHub.Instance.addCothread(this);
		}
	}


	public class CothreadTimeout: IEnumerator {
		public object Current { get { return Timeout; } }
		public bool Timeout { get; set; }
		public double TimeoutTime;
		public TimeSpan PassTime { get { return (DateTime.Now - startTime);} }

		private IEnumerator ie;
		private bool cancel;
		private DateTime startTime;
		private YieldState state;

		public static CothreadTimeout NewWithStart(double timeout) {
			var rs = new CothreadTimeout();
			rs.Start(timeout);
			return rs;
		}

		public bool MoveNext() {
			if (CothreadBaseHub.Instance.current != this) 
				throw new CothreadError("CothreadTimeout.MoveNext Can not call from other");
			if (cancel) 
				return false;

			Timeout = true;
			var hub = CothreadBaseHub.Instance;
			var th = hub.GetCothread(ie);
			if (th == null) 
				return false;
			th.Timeout = this;
			hub.addCothread(ie);
			return false;
		}

		public void Reset() {
			Cancel(true);
		}

		public void Start(double timeout) {
			cancel = false;
			var hub = CothreadBaseHub.Instance;
			ie = hub.current;
			TimeoutTime = timeout;
			startTime = DateTime.Now;
			state = hub.addTimeUp((long)timeout * TimeSpan.TicksPerMillisecond, this);
		}

		public void Cancel(bool isThrow) {
			var hub = CothreadBaseHub.Instance;
			if (hub.CurrentCothread.Timeout == this)
				hub.CurrentCothread.Timeout = null;
			if (isThrow && Timeout) 
				throw new CothreadTimeoutError("Timeout");
			cancel = true;
		}

	}


	public class CothreadTestCase {
		public CothreadBaseHub hub;
		public void Start() {
			hub = NormalHub.Install();
		}

		public void test(int count) {
			hub.StartCoroutine(testTimeout1());
			hub.StartCoroutine(testTimeout2());
			testEvent(count);
		}

		IEnumerator testTimeout1() {
			var timeout = CothreadTimeout.NewWithStart(1005);
			yield return hub.Sleep(1000);
			try {
				timeout.Cancel(true);
				hub.Log("[testTimeout1] ok");
			} catch ( CothreadTimeoutError ) {
				hub.Log("[testTimeout1] error");
			}
		}
		IEnumerator testTimeout2() {
			var timeout = CothreadTimeout.NewWithStart(1005);
			yield return hub.Sleep(1010);
			try {
				timeout.Cancel(true);
				hub.Log("[testTimeout2] CothreadTimeoutError error");
			} catch ( CothreadTimeoutError ) {
				hub.Log("[testTimeout2] CothreadTimeoutError ok");
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
			CothreadResult result = new CothreadResult();
			yield return ev.Get(0, result);
			var msg = string.Format("{0} {1} ->", result.Result, i.ToString());
			if (nev != null) {
				nev.Set(msg);
			} else {
				hub.Log("result: " + msg);
			}
		}
	}

}



