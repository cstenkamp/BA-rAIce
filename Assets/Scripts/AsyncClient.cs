//MAIN TODO HIER: 
//1. die using von hier und vom AIInterface entsprechend aussortieren
//2. Warum wird laut console ein object weiter-used after being disposed?
//3. Wenn er nen paar mal versucht hat den server anzupingen und er ist nicht da, gibt auf
//4. Feedback und Delta mitsenden
//5. Am Anfang des Spiels die globalen params mitschicken (like, welche vektoren er senden wird), damit man nicht beides in python UND unity ändern muss

using UnityEngine;
using System.Collections;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

//stems from the Microsoft example... fun thing is only that it simply doesn't run asynchronously, haha.


public class AsynchronousClient {  //updating python's value should happen asynchronously.

	private const int WAITFORSERVER = 100;

	private static string preparestring(string fromwhat) {
		int len = fromwhat.Length;
		string ms = len.ToString();
		while (ms.Length < 5) {
			ms = "0" + ms;
		}
		ms = ms + fromwhat;
		return ms;
	}

	// ManualResetEvent instances signal completion.  //Notifies one or more waiting threads that an event has occurred
	private static ManualResetEvent connectDone = new ManualResetEvent(false);   
	private static ManualResetEvent sendDone =    new ManualResetEvent(false);  
	private static ManualResetEvent receiveDone = new ManualResetEvent(false);  

	//we have a sender-client, who every x seconds updates python's status
	public static void StartSenderClient(string data) {  
		try {  
			connectDone = new ManualResetEvent(false);   
			sendDone = new ManualResetEvent(false);  
			IPHostEntry ipHost = Dns.GetHostEntry("");
			IPAddress ipAddress = ipHost.AddressList[0];  
			IPEndPoint ipEndPoint  = new IPEndPoint(ipAddress, Consts.PORTSEND);  
			Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);    
			client.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallback), client); 
			if (connectDone.WaitOne(WAITFORSERVER)) {  //try to connect, if it doesn't work, the server seems down and you don't continue.
				Send(client,preparestring(data));  
				sendDone.WaitOne();  

				client.Shutdown(SocketShutdown.Send);  
				client.Close();  
			}
		} catch (Exception e) {  
			UnityEngine.Debug.Log(e.ToString());  
		}  
	}  



	//================================================================================

	// The response from the remote device.  
	public class response {
		public static String str = String.Empty;  
		public static int timestamp = Environment.TickCount;
	}

	//kann sich der Getter python-seitig auf nen anderen Port anmelden, sodass Python beim anmelden an diesen Port weiß dass es da senden soll? Ja
	public static void StartGetterClient() {  
		try {  
			connectDone = new ManualResetEvent(false);   
			sendDone = new ManualResetEvent(false); 
			receiveDone = new ManualResetEvent(false); 
			IPHostEntry ipHost = Dns.GetHostEntry("");
			IPAddress ipAddress = ipHost.AddressList[0];  
			IPEndPoint ipEndPoint  = new IPEndPoint(ipAddress, Consts.PORTASK);  
			Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);    
			client.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallback), client); 
			if (connectDone.WaitOne(WAITFORSERVER)) { //try to connect, if it doesn't work, the server seems down and you don't continue.
				Receive(client);  //das ganze ist ja asynchron, das heißt Receive kann nix returnen sondern nur den value updaten.. was aber ja sogar gewünscht ist!
				receiveDone.WaitOne();  

				client.Shutdown(SocketShutdown.Send);  
				client.Close();  
			}
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString());  }  
	}  


	private static void ConnectCallback(IAsyncResult ar) {  
		try {  
			Socket client = (Socket) ar.AsyncState;  // Retrieve the socket from the state object.  
			client.EndConnect(ar);  
			//UnityEngine.Debug.Log("Socket connected to {0}"+ client.RemoteEndPoint.ToString());  
			connectDone.Set();  
		} catch (Exception e) {  
			//hier kommt er rein wenn kein Server da ist, und hier soll er sich sagen "pff", wenn kein Server da ist.
			//TODO: hier könnte er die ersten 10 male noch warten, und ab dem 11. mal das ganze deaktivieren, davon ausgehend das kein server kommt. Wieder aktivieren wenn escape gedrückt wird.
			UnityEngine.Debug.Log(e.ToString());  
		}  
	}  

	private static void Send(Socket client, String data) {  
		byte[] byteData = Encoding.ASCII.GetBytes(data);  
		client.BeginSend(byteData, 0, byteData.Length, 0,  new AsyncCallback(SendCallback), client);  
	}  

	private static void SendCallback(IAsyncResult ar) {  
		try {  
			Socket client = (Socket) ar.AsyncState;  // Retrieve the socket from the state object. 
			int bytesSent = client.EndSend(ar);  
			//UnityEngine.Debug.Log("Sent {0} bytes to server.", bytesSent);  
			sendDone.Set();  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString());  }  
	}  


	private static void ReceiveCallback( IAsyncResult ar ) {  
		try {  
			//This callback will also be called if the client is already disposed - which is why we catch that.
			StateObject state = (StateObject) ar.AsyncState;  // Retrieve the state object and the client socket 
			Socket client = state.workSocket;  
			int bytesRead = client.EndReceive(ar); // Read data from the remote device. 

			if (bytesRead > 0) {  
				state.sb.Append(Encoding.ASCII.GetString(state.buffer,0,bytesRead));   //buffer so far...
				client.BeginReceive(state.buffer,0,StateObject.BufferSize,0, new AsyncCallback(ReceiveCallback), state);  //...look for more
			} else {  
				if (state.sb.Length > 1) {  // All the data has arrived; put it in response.  
					response.str = state.sb.ToString();  
					response.timestamp = Environment.TickCount;
					//UnityEngine.Debug.Log ("Python answered: "+response);
				}  
				receiveDone.Set();  
			}  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString()); }    //TODO: wenn er mit server verbunden ist, kommt hier noch die meldung "object was used after being disposed", which sucks!
	}  


	private static void Receive(Socket client) {  
		try {   
			StateObject state = new StateObject();  
			state.workSocket = client;  
			client.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);  
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString()); }  
	}  


	// State object for receiving data from remote device.  
	public class StateObject {  
		// Client socket.  
		public Socket workSocket = null;  
		// Size of receive buffer.  
		public const int BufferSize = 256;  
		// Receive buffer.  
		public byte[] buffer = new byte[BufferSize];  
		// Received data string.  
		public StringBuilder sb = new StringBuilder();  
	}  

} 