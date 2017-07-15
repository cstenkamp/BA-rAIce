﻿//MAIN TODO HIER: 
//4. Feedback und Delta mitsenden
//5. Am Anfang des Spiels die globalen params mitschicken (like, welche vektoren er senden wird), damit man nicht beides in python UND unity ändern muss

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

//stems from the Microsoft example... fun thing is only that it simply doesn't run asynchronously, haha.

public class AsynchronousClient {  //updating python's value should happen asynchronously.

	//consts are always static in c#
	public const int WAITFORSOCKET = 100; 
	private const int MAXCONNECTTRIALS = 5;
	private const int SOCKETSTRINGDIGITS = 5;

   //these are non-static
	private int serverconnecttrials;
	public bool serverdown;
	private bool is_sender;
	public Response response;

	// ManualResetEvent instances signal completion (notifies one or more waiting threads that an event has occurred)
	private ManualResetEvent connectDone;   
	private ManualResetEvent sendDone;  
	private ManualResetEvent receiveDone;  

	// es wird 2 asyncclients geben, einer fürs senden und einer fürs receiven, also wird dieser socket ENTWEDER sender ODER receiver
	public Socket socket;    


	public AsynchronousClient(bool for_sender){
		//these are non-static
		serverconnecttrials = 0;
		serverdown = true;
		connectDone = new ManualResetEvent(false);   
		sendDone =    new ManualResetEvent(false);  
		receiveDone = new ManualResetEvent(false);  
		is_sender = for_sender;
		if (!is_sender) {
			response = new Response();
		}
	}


	public void ResetServerConnectTrials() {
		serverconnecttrials = 0;
	}


	private string preparestring(string fromwhat) {
		int len = fromwhat.Length;
		string ms = len.ToString();
		while (ms.Length < 5) {
			ms = "0" + ms;
		}
		ms = ms + fromwhat;
		return ms;
	}




	//used for both sender and receiver
	public void StartClientSocket() {  //this only starts the client and saves the socket.
		if (serverdown)
			return;
		int port;
		if (is_sender)
			port = Consts.PORTSEND;
		else 
			port = Consts.PORTASK;

		try {  
			connectDone = new ManualResetEvent(false);   
			IPHostEntry ipHost = Dns.GetHostEntry("");
			IPAddress ipAddress = ipHost.AddressList[0];  
			IPEndPoint ipEndPoint  = new IPEndPoint(ipAddress, port);  
			Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);    
			client.BeginConnect(ipEndPoint, new AsyncCallback(ConnectCallback), client); 
			if (connectDone.WaitOne(WAITFORSOCKET)) {  //try to connect, if it doesn't work, the server seems down and you don't continue.
				socket = client; 
			} else {
				increasesvtrials();
			}
		} catch (Exception e) {  
			UnityEngine.Debug.Log(e.ToString()); 
		}  
	}

	//used for both sender and receiver
	private void ConnectCallback(IAsyncResult ar) {  
		try {  
			Socket client = (Socket) ar.AsyncState;  // Retrieve the socket from the state object.  
			client.EndConnect(ar);  
			UnityEngine.Debug.Log(SoR(is_sender) + "Socket connected to "+ client.RemoteEndPoint.ToString());  
			connectDone.Set();  
		} catch (SocketException) {  
			//hier kommt er rein wenn kein Server da ist -> teste einige male mehr, wenn zu oft, stop trying.
			increasesvtrials ();
		}  
	}  

	//used for both sender and receiver
	private void increasesvtrials() {
		if (serverdown)
			return;
		serverconnecttrials += 1;
		if (serverconnecttrials > MAXCONNECTTRIALS) {
			UnityEngine.Debug.Log (SoR(is_sender) + "Stopping to try to Connect to Server. Once you set up a server, press [C].");
			serverdown = true;
		}
	}

	//used for both sender and receiver
	public void StopClient() {
		try {
			socket.Shutdown(SocketShutdown.Send);  
			socket.Close(); 
			socket = null; 
			UnityEngine.Debug.Log(SoR(is_sender) + "Disconnected. You can manually reconnect");
		} 
		catch (ObjectDisposedException) {}
		catch (NullReferenceException) {}
	}


	public String SoR(bool is_sender) {
		String prestring = "(receiver) ";
		if (is_sender)
			prestring = "(sender) ";
		return prestring;
	}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////// used only for senderclient ///////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 

	public void SendAufJedenFall(String data) {
		if (serverdown) 
			return;
		try {
			Send (socket, data);  
		} catch (Exception e) {
			if (e is ObjectDisposedException || e is SocketException || e is NullReferenceException) {
				//etabliere NEUE verbindung, die daten müssen schließlich rüber!
				StartClientSocket (); //overwrites the old "socket"
				AiInterface.print("This shouldnd happen too often deleteme");
				Send (socket, data);  
			} else {
				UnityEngine.Debug.Log(e.ToString());
			}
		}
	}

	public void Send(Socket client, String data) {  
		try {
			data = preparestring(data);
			byte[] byteData = Encoding.ASCII.GetBytes(data);  
			client.BeginSend(byteData, 0, byteData.Length, 0,  new AsyncCallback(SendCallback), client);  
		} catch (Exception) {
			throw;
		}
	}  

	private void SendCallback(IAsyncResult ar) {  
		try {  
			Socket client = (Socket) ar.AsyncState;  // Retrieve the socket from the state object. 
			int bytesSent = client.EndSend(ar);  
			//UnityEngine.Debug.Log("Sent {0} bytes to server.", bytesSent);  
			sendDone.Set();  
			AiInterface.print(AiInterface.MSTime() + "Some sending done");
		} catch (Exception) {  
			throw;
		}  
	}  


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////// used only for receiverclient /////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 

	public void StartReceiveLoop() {
		while (true) {
			if (serverdown)
				return;
			try {
				receiveDone = new ManualResetEvent(false); 
				Receive ();
				receiveDone.WaitOne ();
				UnityEngine.Debug.Log ("Response updated to: "+response.pedals+" Time it took: "+(response.timestampReceive-response.timestampStarted).ToString()+"ms");  //ASDF
			} catch (NullReferenceException) { //wird in Receive() gethrowt wenn python not connected
				StartClientSocket ();
				//öfter versuchen als receiver zu connecten, wenns nicht geht das melden
			}
		}
	}


	private void Receive() {  
		try {   
			StateObject state = new StateObject();  
			state.workSocket = socket;  
			socket.BeginReceive( state.buffer, 0, SOCKETSTRINGDIGITS, 0, new AsyncCallback(ReceiveStringLengthCallback), state); //at first you read the first 5 digits, which should be the length of the following
		} catch (NullReferenceException) {
			throw; //der StartReceiveLoop muss sich drum kümmern, da er ansonsten wartet!
		} catch (Exception e) {  UnityEngine.Debug.Log(e.ToString()); }  
	}  


	private void ReceiveStringLengthCallback( IAsyncResult ar ) {  
		try {  
			StateObject state = (StateObject) ar.AsyncState;  // Retrieve the state object and the client socket 
			Socket client = state.workSocket;  
			int bytesRead = client.EndReceive(ar); // Read data from the remote device. 

			int stringlength = Int32.Parse(Encoding.ASCII.GetString(state.buffer,0,bytesRead));
	
			client.BeginReceive(state.buffer, 0, stringlength, 0, new AsyncCallback(ReceiveCallback), state);

		} catch (System.ObjectDisposedException) { } //This callback will also be called if the client is already disposed - which is why we catch that.
		catch (System.FormatException) { //Wenn man pyton mittendrin beendet. Kommt dann genau ein mal.
			UnityEngine.Debug.Log (SoR(is_sender)+"Did Python just crash?");
			StopClient ();
		} 
		catch (Exception e) { UnityEngine.Debug.Log(e.ToString()); }    
	}  


	private void ReceiveCallback( IAsyncResult ar ) {  
		try {  
			StateObject state = (StateObject) ar.AsyncState;  // Retrieve the state object and the client socket 
			Socket client = state.workSocket;  
			int bytesRead = client.EndReceive(ar); // Read data from the remote device. 

			state.sb.Append(Encoding.ASCII.GetString(state.buffer,0,bytesRead));   //buffer so far...
			response.update(state.sb.ToString());  
			receiveDone.Set();  

		} catch (System.ObjectDisposedException) { } //This callback will also be called if the client is already disposed - which is why we catch that.
		catch (Exception e) { UnityEngine.Debug.Log(e.ToString()); }    
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


	// The response from the remote device.  
	public class Response {
		public String pedals;
		public long timestampStarted;
		public long timestampReceive;
		public bool othercommand;
		public String command;

		public Response(){
			pedals = String.Empty;  
			timestampReceive = 0;
			timestampStarted = 0;
			othercommand = false;
			command = String.Empty;
		}

		public void update(String newstr){
			try {
				UnityEngine.Debug.Log ("RECEIVING TIME: " + timestampStarted + " @ " + AiInterface.MSTime ());
				if (newstr.Substring (0, 1) != "[") {
					if (othercommand == false) 
						timestampReceive = AiInterface.MSTime();
					othercommand = true;
					if (newstr.IndexOf("Time(") > 0) {
						newstr = newstr.Substring(0, newstr.IndexOf("Time("));
					}
					command = newstr;
				} else {
					pedals = newstr.Substring (0, newstr.IndexOf ("]")+1);
					timestampStarted = (long) float.Parse(newstr.Substring (newstr.IndexOf ("Time(")+5, newstr.LastIndexOf (")")-newstr.IndexOf ("Time(")-5 ));
					timestampReceive = (long) AiInterface.MSTime();
					othercommand = false;
				}
			} catch (ArgumentOutOfRangeException e) {
				UnityEngine.Debug.Log ("error: " + e.ToString ());
			}
		}

		public void reset() {
			pedals = String.Empty;  
			timestampReceive = 0;
			timestampStarted = 0;
			othercommand = false;
			command = String.Empty;			
		}
	}
} 