using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Threading;


public  class SynchronousSocketListener {
	private static int portNum = 1234;            //Default port number 1234
	private static ArrayList ClientSockets;
	private static bool ContinueReclaim = true;
 	private static Thread ThreadReclaim;
	private static bool isVerbose = false;
	
  	public  static  void StartListening() {
    	ClientSockets = new ArrayList() ;
		int ClientNbr = 0;    
    	ThreadReclaim = new Thread(new ThreadStart(Reclaim));
    	ThreadReclaim.Start() ;
    
    	TcpListener listener = new TcpListener(portNum);
    	try {
        	listener.Start();
        
            // Start listening for connections.
            Console.WriteLine("Waiting for a connection...");
            while (true) {
            	TcpClient handler = listener.AcceptTcpClient();
                        
                if (handler != null)  {
 	            	Console.WriteLine("Client#{0} accepted!", ++ClientNbr) ;
               	    // An incoming connection needs to be processed.
                	lock( ClientSockets.SyncRoot ) {
                    	int i = ClientSockets.Add(new ClientHandler(handler)) ;
                        ((ClientHandler) ClientSockets[i]).Start() ;
                    }
                }            
            }
            listener.Stop();
              
            ContinueReclaim = false ;
            ThreadReclaim.Join() ;
              
            foreach ( Object Client in ClientSockets ) {
            	( (ClientHandler) Client ).Stop() ;
            }
            
    	} catch (Exception e) {
        	Console.WriteLine(e.ToString());
    	}
        
    	Console.WriteLine("\nHit enter to continue...");
    	Console.Read();
    }
	
  	private static void Reclaim()  {
        while (ContinueReclaim) {
        	lock(ClientSockets.SyncRoot) {
            	for (int x = ClientSockets.Count-1 ; x >= 0 ; x-- )  {
                        Object Client = ClientSockets[x] ;
                        if (!( ( ClientHandler ) Client ).Alive )  {
                        	ClientSockets.Remove( Client )  ;
                            Console.WriteLine("A client left") ;
                        }
                }
            }
            Thread.Sleep(200) ;
        }         
  	}
  
  
  	public  static  int Main(String[] args) {
    	int i = 0;
    	while (i < args.Length) {
    		//port number
    		if (args[i] == "-p") {
    			portNum = Int32.Parse(args[++i]);
    			i++;
    		}
    		else if (args[i] == "-v") {
    			isVerbose = true;
    			i++;
    		} else {
    			i++;
    		}
    	}
    	
    	if (isVerbose) {
    		Console.WriteLine("Listening port number is {0}", portNum);
    	}
    	
    	StartListening();
    	return 0;
  	}
}

class ClientHandler {

	TcpClient ClientSocket ;
	bool ContinueProcess = false;
	Thread ClientThread;

	public ClientHandler (TcpClient ClientSocket) {
		this.ClientSocket = ClientSocket;
	}

	public void Start() {
		ContinueProcess = true ;
		ClientThread = new Thread (new ThreadStart(Process));
		ClientThread.Start();
	}

	private  void Process() {
		// Data buffer for incoming data.
		byte[] bytes;
        int readBytes = 0;
        string line;
        
		if (ClientSocket != null) {
        	NetworkStream networkStream = ClientSocket.GetStream();

			using (StreamReader reader = new StreamReader(ClientSocket.GetStream(), System.Text.Encoding.ASCII)) {
				while ((line = reader.ReadLine()) != null) {
					Console.WriteLine("Read a line");
					Console.Write(line);
				}
			}
			
			/*
			while (readBytes < totalToRecv) {
            	bytes = new byte[ClientSocket.ReceiveBufferSize];
                try {
                	int BytesRead = networkStream.Read(bytes, 0, (int)ClientSocket.ReceiveBufferSize);
                    readBytes += BytesRead;
                                        
                    if (BytesRead == 0) {
                    	break;
                    }

                    Console.Write("{0}", Encoding.ASCII.GetString(bytes, 0, BytesRead));
                } catch  ( IOException ) {
                } catch  ( SocketException ) {
                	Console.WriteLine( "Conection is broken!");
                    break ;
                }
	       	} 
	       	*/
	       	 
            networkStream.Close() ;
        	ClientSocket.Close();			
            Console.WriteLine("Connection closed!");
		}
	}  // Process()

	public void Stop() 	{
		ContinueProcess = false ;
        if ( ClientThread != null  && ClientThread.IsAlive ) {
			ClientThread.Join() ;
		}
	}
        
    public bool Alive {
    	get {
    		return  ( ClientThread != null  && ClientThread.IsAlive  );
    	}
   	}      
} 

