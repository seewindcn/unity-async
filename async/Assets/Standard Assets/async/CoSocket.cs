#region Header
/**
 *  Cothread for unity3d
 *  Author: seewind
 *  https://github.com/seewindcn/unity-async
 *  email: seewindcn@gmail.com
 **/
#endregion

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.IO;


namespace Cothread {

	public class CothreadSocketRecvData {
		public byte[] Data;
		public int Length;
		public CothreadSocketRecvData(int len) {
			Data = new byte[len];
			Length = 0;
		}

		public void Clear() {
			Length = 0;
			Array.Clear(Data, 0, Data.Length);
		}
	}


	public class CothreadSocket {
		public string Host;
		public int Port;
		public bool Connected {
			get {
				return sock != null && sock.Connected;
			}
		}

		protected Socket sock;

		public CothreadSocket() {
			sock = null;
		}

		public void Close() {
			if (Connected)
				sock.Close();
		}
		
		public IEnumerator Connect(string host, int port) {
			if (Connected)
				yield break;

			sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			//var ipAddr = IPAddress.Parse(host);
			//var ipEnd = IPEndPoint(ipAddr, port);

			var hub = CothreadHub.Instance;
			var rs = sock.BeginConnect(host, port, hub.cb, hub.current);
			if (!rs.IsCompleted)
				yield return rs;
			if (hub.CurrentCothread.CheckTimeout()) 
				Close();
			else {
				try {
					sock.EndConnect(rs);
                } catch (SocketException) {
					Close();
					yield break;
                }
            }
			Host = host;
			Port = port;
        }
			
		public IEnumerator Send(byte[] data) {
			if (!Connected)
				yield break;

			var hub = CothreadHub.Instance;
			var rs = sock.BeginSend(data, 0, data.Length, SocketFlags.None, hub.cb, hub.current);
			//hub.Print("$(GetHashCode().ToString()) sock.BeginSend .....")
			if (!rs.IsCompleted)
				yield return rs;

			//hub.Print("$(GetHashCode().ToString()) sock.EndSending .....")
			int l;
			if (hub.CurrentCothread.CheckTimeout()) {
				Close();
				CothreadHub.Log(string.Format("[AsyncSocket.send]:{0} Send Timeout", GetHashCode()));
				yield break;
            } else {
				try {
					l = sock.EndSend(rs);
					//hub.Print("$(GetHashCode().ToString()) sock.EndSend")
                } catch (SocketException) {
					Close();
                    CothreadHub.Log(string.Format("[AsyncSocket.send]:{0} sock.EndSend SocketException", GetHashCode()));
					yield break;
                }
            }

			if (l != data.Length)
                CothreadHub.Log(string.Format("[AsyncSocket.send]:size({0}) != len(data)({1})", l, data.Length));
        }

		//utf8 encoding
		public IEnumerator SendString(string data) {
			if (!Connected)
				yield break;
			//yield return Send(System.Text.Encoding.UTF8.GetBytes(data));
			var buf = Array.CreateInstance(typeof(byte), data.Length * sizeof(char));
			Buffer.BlockCopy(data.ToCharArray(), 0, buf, 0, buf.Length);
			yield return Send((byte[])buf);
		}

		public IEnumerator Recv(CothreadSocketRecvData recvData) {
			if (!Connected)
				yield break;
			recvData.Clear();
			var hub = CothreadHub.Instance;
			var rs = sock.BeginReceive(recvData.Data, 0, recvData.Data.Length, 
			                           SocketFlags.None, hub.cb, hub.current);

			if (!rs.IsCompleted)
				yield return rs;
			if (hub.CurrentCothread.CheckTimeout())
				Close();
			else {
				try {
					recvData.Length = sock.EndReceive(rs);
				} catch (SocketException) {
					Close();
				}
			}
		}

		//utf8 encoding
		public IEnumerator RecvString(CothreadSocketRecvData recvData, CothreadResult result) {
			if (!Connected)
				yield break;
			yield return Recv(recvData);
			result.Result = System.Text.Encoding.UTF8.GetString(recvData.Data, 0, recvData.Length);
		}
	}
}