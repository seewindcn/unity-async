namespace Async


import System
import System.Collections
import System.Net
import System.Net.Sockets
import System.Threading


public class AsyncError(Exception):
	err as string
	def constructor(e as string):
		self.err = e

	def ToString():
		return self.err

public class TimeoutError(AsyncError):
	def constructor(s as string):
		super(s)



class YieldState:
	[Property(CYield)]
	_cyield as IEnumerator
	[Property(WakeTime)]
	_wakeTime as DateTime

	def ToString() as string:
		code = self.GetHashCode()
		return "Async.YieldState<$code>(WakeTime=$WakeTime)"



public class Hub:
	[ThreadStatic]
	static _instance as Hub
	public static CALLBACK as object = object()

	public IdleTime as int = 100
	public BusyTime as int = 10
	locker as object
	lets as Hash
	yields as Boo.Lang.List[of IEnumerator]
	times as Boo.Lang.List[of YieldState]
	cbIndex as int
	public cb as AsyncCallback
	mainThreadID as int

	public static def Default() as Hub:
		return Hub._instance
	virtual def SetDefault():
		Hub._instance = self


	[property(Stoped)]
	_stoped as bool = true

	[Getter(Current)]
	_current as IEnumerator
	CurrentLet as Greenlet:
		get:
			return self.lets[self._current] if self._current != null 

	public def GetLet(y as IEnumerator) as Greenlet:
		return self.lets[y]

	def constructor():
		self.locker = object()
		self.lets = Hash()
		self.yields = Boo.Lang.List[of IEnumerator]()
		self.times = Boo.Lang.List[of YieldState]()
		self.cb = AsyncCallback(self.callback)
		self.mainThreadID = Thread.CurrentThread.ManagedThreadId

	public def Stop():
		self.Stoped = true

	def nextIndex():
		self.cbIndex += 1
		return self.cbIndex

	def callback(ar as IAsyncResult):
		#Thread.Sleep(200)
		#print "[callback]", ar
		lock self.locker:
			y = ar.AsyncState cast IEnumerator
			let = self.GetLet(y)
			if let.AsyncResult != ar:
				return
			let.AsyncResult = null
			self.addYield(y)
			#print 'callback', Thread.CurrentThread.ManagedThreadId, ar, ar.AsyncState, s

	public def addYield(y as IEnumerator):
		lock self.locker:
			let = self.GetLet(y)
			let.AsyncResult = null if let != null
			self.yields.Add(y)

	public def addYields(yields as Boo.Lang.List[of IEnumerator]):
		lock self.locker:
			for y in yields:
				let = self.GetLet(y)
				let.AsyncResult = null if let != null
				self.yields.Add(y)

	def popYield() as IEnumerator:
		lock self.locker:
			if len(self.yields) == 0:
				return
			return self.yields.Pop(0)

	def addCallback(y as IEnumerator):
		lock self.locker:
			let = self.GetLet(y)
			if let == null:
				print "[addCallback]: let timeout??"
				return
			if let.AsyncResult:
				raise AsyncError("addCallback error: yield exist")
			#print "addCallback", let


	def addTimeUp(ms as int, y as IEnumerator):
		t = TimeSpan(TimeSpan.TicksPerMillisecond * ms)
		state = YieldState(CYield:y, WakeTime:DateTime.Now + t)
		self.times.Add(state)
		self.times.Sort() do (l as YieldState, r as YieldState):
			if l.WakeTime < r.WakeTime:
				return -1
			elif l.WakeTime > r.WakeTime:
				return 1
			else:
				return 0
		#print self.times


	def call(t as IEnumerator):
		self._current = t
		#print "call", self.GetLet(t)
		ok = t.MoveNext()
		self._current = null
		#print "call result", t.Current
		let = self.lets[t] as Greenlet
		if not ok:
			if let != null:
				#print "[call]:let close", let
				let.Close()
				self.lets.Remove(t)
			return

		if t.Current == Hub.CALLBACK: #call back
			self.addCallback(t)
		elif t.Current isa IAsyncResult:
			self.addCallback(t)
			let.AsyncResult = t.Current
		else: #continue yield
			self.addYield(t)

	virtual def tick() as bool:
		lyields = len(self.yields)
		ltimes = len(self.times)

		#schedule yield
		for i in range(lyields):
			y = self.popYield()
			if y == null:
				break
			self.call(y)

		#schedule times
		while len(self.times) and self.times[0].WakeTime <= DateTime.Now:
			state = self.times.Pop(0)
			#print "wakeUp:", state.WakeTime.Ticks
			self.call(state.CYield)
		return (lyields + ltimes) > 0


	public def StartCoroutine(routine) as Greenlet:
		y = call_yield(routine)
		t = y.GetEnumerator() cast IEnumerator
		let = Greenlet(CYield:t)
		self.addYield(t)
		self.lets[t] = let
		#print "StartCoroutine", let
		return let

	public def StartCoroutine(ms as int, routine) as Greenlet:
		y = call_yield(routine)
		t = y.GetEnumerator() cast IEnumerator
		let = Greenlet(CYield:t)
		self.addTimeUp(ms, t)
		self.lets[t] = let
		#print "StartCoroutine", let
		return let



	public def Sleep(ms as int) as IEnumerable:
		if self._current == null:
			raise AsyncError("Hub.Sleep only can call from coroutine")
		self.addTimeUp(ms, self._current)
		yield Hub.CALLBACK

	//waiting for stop
	public def Join(y as IEnumerator, timeout as int) as IEnumerable:
		let = self.lets[y]
		if let == null:
			return
		return (let cast Greenlet).Join(timeout)

	public virtual def print(*args):
		pass


def call_yield(f as IEnumerable) as IEnumerable:
	for j in f:
		if j isa IEnumerable:
			cy = call_yield(j)
			for i in cy:
				yield i
		else:
			yield j


public class NormalHub(Hub):
	public def Loop():
		self.Stoped = false
		while not self.Stoped:
			if not self.tick():
				self.onIdle()
		self.Stoped = true

	def onIdle():
		Thread.Sleep(self.IdleTime)



class Greenlet:
	[Property(CYield)]
	_cyield as IEnumerator

	[Property(Closed)]
	_closed as bool

	[Property(AsyncResult)]
	_AsyncResult as IAsyncResult

	[Property(Timeout)]
	_timeout as AsyncTimeout 

	evJoin as AsyncEvent
	def constructor():
		self.evJoin = AsyncEvent()
		self._closed = false

	public def Join(timeout as int):
		yield self.evJoin.Wait(timeout)

	def Close():
		self._closed = true
		self.evJoin.Set(true)

	public def CheckTimeout():
		return self.Timeout != null

	def ToString() as string:
		code = self.GetHashCode()
		return "Async.Greenlet<$code>"



public class AsyncEvent(IEnumerator):
	private static NULL = object()
	[Property(Value)]
	_value as object
	private yields as Boo.Lang.List[of IEnumerator]

	def constructor():
		self.yields = Boo.Lang.List[of IEnumerator]()
		self.Clear()

	public def Clear():
		self._value = AsyncEvent.NULL
		self.yields.Clear()

	def MoveNext():
		h = Hub.Default()
		if h.Current != self:
			raise AsyncError("Can not call")
		h.addYields(self.yields)
		self.yields.Clear()


	public def Wait(timeout as int) as IEnumerable:
		if self._value != AsyncEvent.NULL:
			return
		self.yields.Add(Hub.Default().Current)
		if timeout > 0:
			t = AsyncTimeout.WithStart(timeout)
		yield Hub.CALLBACK
		if timeout > 0:
			t.Cancel(false)


	public def Get(timeout as int, result as (object)) as IEnumerable:
		yield self.Wait(timeout)
		if self._value != AsyncEvent.NULL:
			result[0] = self._value

	public def Set(v as object):
		self._value = v
		if len(self.yields) > 0:
			Hub.Default().addYield(self)



public class AsyncTimeout(IEnumerator):
	y as IEnumerator
	[Property(Timeout)]
	_timeout as bool
	_cancel as bool
	mstime as int

	public static def WithStart(timeout as int) as AsyncTimeout:
		t = AsyncTimeout()
		t.Start(timeout)
		return t

	def MoveNext():
		if Hub.Default().Current != self:
			raise AsyncError("Can not call")
		if self._cancel:
			return
		#print "[Timeout]MoveNext", self
		self._timeout = true
		hub = Hub.Default()
		let = hub.GetLet(self.y)
		if let == null:
			return
		hub.GetLet(self.y).Timeout = self
		hub.addYield(self.y)

	public def Start(timeout as int):
		self._cancel = false
		h = Hub.Default()
		self.y = h.Current
		self.mstime = timeout
		h.addTimeUp(timeout, self)

	public def Cancel(isRaise as bool):
		h = Hub.Default()
		if h.CurrentLet.Timeout == self:
			h.CurrentLet.Timeout = null
		if isRaise and self._timeout:
			t = self.mstime
			raise TimeoutError("Timeout:$t")
		#Timeout use at short time, so not delete self from Hub.times,
		self._cancel = true

	def ToString() as string:
		code = self.GetHashCode()
		return "Async.AsyncTimeout<$code>"


public class SocketError(AsyncError):
	def constructor(s as string):
		super(s)


public class AsyncSocket:
	sock as Socket
	host as string
	port as int

	def constructor():
		self.sock = null

	Connected as bool:
		get:
			return self.sock != null and self.sock.Connected

	def Close():
		self.sock.Close() if self.sock != null
	
	def Connect(host as string, port as int):
		if self.sock != null and self.sock.Connected:
			return
		self.sock = Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
		ipAddr = IPAddress.Parse(host)
		ipEnd = IPEndPoint(ipAddr, port)

		hub = Hub.Default()
		rs = sock.BeginConnect(ipEnd, hub.cb, hub.Current)
		yield rs
		if hub.CurrentLet.CheckTimeout():
			self.Close()
		else:
			try:
				sock.EndConnect(rs)
			except e as SocketException:
				self.Close()
				return

		self.host = host
		self.port = port
		
	def Send(data as (byte)) as IEnumerable:
		if not self.Connected:
			return
			#raise SocketError("socket closed!")

		#print "[AsyncSocket.send]:", len(data)
		hub = Hub.Default()
		rs = self.sock.BeginSend(data, 0, len(data), SocketFlags.None, 
				hub.cb, hub.Current);
		yield rs
		if hub.CurrentLet.CheckTimeout():
			self.Close()
			length = 0
		else:
			try:
				length = self.sock.EndSend(rs)
			except e as SocketException:
				self.Close()
				return

		l = len(data)
		if length != l:
			print "[AsyncSocket.send]: size($length) != len(data)($l)"

	def SendString(data as string) as IEnumerable:
		buf = array(byte, len(data) * sizeof(char))
		Buffer.BlockCopy(data.ToCharArray(), 0, buf, 0, len(buf))
		return self.Send(System.Text.Encoding.UTF8.GetBytes(data))


	def Recv(len_bytes as (object)) as IEnumerable:
		hub = Hub.Default()
		data as (byte) = len_bytes[1] cast (byte)
		if not self.Connected:
			return
			#raise SocketError("socket closed!")
		rs = self.sock.BeginReceive(data, 0, len(data), SocketFlags.None,
				hub.cb, hub.Current)
		yield rs
		if hub.CurrentLet.CheckTimeout():
			self.Close()
		else:
			try:
				len_bytes[0] = self.sock.EndReceive(rs)
			except e as SocketException:
				self.Close()
				return

	//utf8 encoding
	def RecvString(len_bytes as (object)) as IEnumerable:
		yield self.Recv(len_bytes)
		l = len_bytes[0] cast int
		buf = len_bytes[1] cast (byte)
		len_bytes[1] = System.Text.Encoding.UTF8.GetString(buf[0:l])

class TestCase:
	hub as Hub
	def start():
		hub = Hub.Default()
		hub.StartCoroutine(self.test())

	def test():
		_start = hub.StartCoroutine
		print "***testTimeout***"
		let = _start(self.testTimeout())
		yield let.Join(0)

		print "***testEvent***"
		#yield self.testEvent()

		print "***testSocket***"
		#let = _start(self.testSocket())
		#yield let.Join(0)

		print "***testBen***"
		yield self.testBen()


	def testTimeout():
		#btime = DateTime.Now
		timeout = AsyncTimeout.WithStart(1000)
		yield hub.Sleep(1010)
		try:
			timeout.Cancel(true)
			print "no timeout"
		except e as TimeoutError:
			print "timeout"


	def _event(index as int, e as AsyncEvent) as IEnumerable:
		print "t3-$index"
		if index % 2 == 1:
			yield hub.Sleep(1000)
			e.Set("t$index")
			print "t3-$index end"
		else:
			yield e.Wait(2000)
			print "t3-$index Get:", e.Value
			rs = array(object, 1)
			yield e.Get(2000, rs)
			print "t3-$index Get:", rs[0]

	def testEvent():
		e = AsyncEvent()
		l1 = hub.StartCoroutine(self._event(1, e))
		l2 = hub.StartCoroutine(self._event(2, e))
		print "testEvent", l1, l2
		yield l1.Join(0)
		yield l2.Join(0)
		print "testEvent finish"

	def testSocket():
		s = AsyncSocket()
		timeout = AsyncTimeout.WithStart(1000)
		yield s.Connect("119.146.200.16", 80)
		#yield s.Connect("10.91.11.11", 80)
		print "socket connected:", s.Connected
		yield s.SendString("GET\n")
		len_bytes = (0, array(byte, 2048))
		yield s.RecvString(len_bytes)
		print "Recv:", len_bytes[0], (len_bytes[1] cast string)[0:10]
		try:
			timeout.Cancel(true)
		except:
			print "testSocket timeout ....."
		print "xx:", hub.CurrentLet.Timeout

	def _ben1(index as int):
		s = AsyncSocket()
		yield s.Connect("119.146.200.16", 80)
		yield hub.Sleep(10)
		print "($index) socket connected:", s.Connected

		yield s.SendString("GET\n")
		yield hub.Sleep(1010)

		rs = (0, array(byte, 2048))
		yield s.RecvString(rs)
		yield hub.Sleep(1010)

		print "Recv:", rs[0], (rs[1] as string)[0:10]

		print "sleep:", DateTime.Now.Ticks
		t = AsyncTimeout.WithStart(507)
		yield hub.Sleep(500)
		t.Cancel(true)
		print "Wake up"

	def _ben2():
		for i in range(100):
			yield i

	#benchmark
	def testBen():
		lets = []
		for i in range(1):
			lets.Add(hub.StartCoroutine(self._ben1(i)))
		for i in range(0):
			lets.Add(hub.StartCoroutine(self._ben2()))
		for l in lets:
			yield (l as Greenlet).Join(0)


def print(*args):
	Hub.Default().print(*args)

def Normal():
	h = NormalHub()
	h.SetDefault()

def NormalStartForever():
	(Hub.Default() as NormalHub).Loop()

def test():
	case = TestCase()
	case.start()

Normal()
test()
NormalStartForever()



