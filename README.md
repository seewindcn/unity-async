unity-async
===========

unity3d's experimental Async package, make by [Boo](https://github.com/bamboo/boo/wiki).

idea from [gevent](http://gevent.org/).

Improve unity3d's coroutine, provide:

    UnityHub, AsyncEvent, AsyncTimeout, AsyncSocket.
    
**support:** 

    c#, boo or unityScript.

Demo(see Test1.cs): 
------------------
**AsyncTimeout**

    IEnumerable testTimeout() {
        AsyncTimeout timeout = AsyncTimeout.WithStart(100);
        yield return hub.Sleep(200);
        timeout.Cancel(false);
        if (timeout.Timeout) {
            msg += "testTimeout ok" + CR;
        }
    }

**AsyncEvent**

    coroutines can channel each other.

**AsyncSocket**

    use non-blocking socket, No need thread to support;
    Support connect, send, read timeout;
