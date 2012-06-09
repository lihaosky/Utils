using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Threading;


public  class SynchronousSocketListener {
        
  private  const int   portNum = 10116 ;
  private  static  ArrayList ClientSockets  ;
  private  static   bool ContinueReclaim =  true;
  private  static   Thread ThreadReclaim ;
  
  public  static  void StartListening() {

    ClientSockets = new ArrayList() ;
    
    ThreadReclaim = new Thread( new ThreadStart(Reclaim) );
    ThreadReclaim.Start() ;
    
    TcpListener listener = new TcpListener(portNum);
    try {
              listener.Start();
        
              int TestingCycle = 10 ; 
              int ClientNbr = 0 ;
        
              // Start listening for connections.
              Console.WriteLine("Waiting for a connection...");
              while (true) {
                      
                        TcpClient handler = listener.AcceptTcpClient();
                        
                        if (  handler != null)  {
                                Console.WriteLine("Client#{0} accepted!", ++ClientNbr) ;
                                // An incoming connection needs to be processed.
                                lock( ClientSockets.SyncRoot ) {
                                        int i = ClientSockets.Add( new ClientHandler(handler) ) ;
                                        ((ClientHandler) ClientSockets[i]).Start() ;
                                }
                                --TestingCycle ;
                        }
                        else 
                                break;                
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
                lock( ClientSockets.SyncRoot ) {
                  for (   int x = ClientSockets.Count-1 ; x >= 0 ; x-- )  {
                        Object Client = ClientSockets[x] ;
                        if ( !( ( ClientHandler ) Client ).Alive )  {
                                ClientSockets.Remove( Client )  ;
                                Console.WriteLine("A client left") ;
                        }
                   }
                }
                Thread.Sleep(200) ;
        }         
  }
  
  
  public  static  int Main(String[] args) {
    StartListening();
    return 0;
  }
}

class ClientHandler {

	TcpClient ClientSocket ;
	bool ContinueProcess = false ;
	Thread ClientThread ;

	public ClientHandler (TcpClient ClientSocket) {
		this.ClientSocket = ClientSocket ;
	}

	public void Start() {
		ContinueProcess = true ;
		ClientThread = new Thread ( new ThreadStart(Process) ) ;
		ClientThread.Start() ;
	}

	private  void Process() {
        int count = 12;

		// Incoming data from the client.
		 string data = null;

		// Data buffer for incoming data.
		byte[] bytes;
                int readBytes = 0;

		if ( ClientSocket != null ) {
                        NetworkStream networkStream = ClientSocket.GetStream();
                        ClientSocket.ReceiveTimeout = 100 ; // 1000 miliseconds

			while (count-- > 0  ) {
                                bytes = new byte[1024*1024];
                                try {
                                        int BytesRead = networkStream.Read(bytes, 0, 1024*1024);
                                        readBytes += BytesRead;

                                        Console.WriteLine("Read {0}", BytesRead);

                                        if ( BytesRead > 0 ) {
                                                data = Encoding.ASCII.GetString(bytes, 0, BytesRead);
                
                                                // Show the data on the console.
                                                Console.WriteLine( "Text received : {0}", data);
                
                                                // Echo the data back to the client.
                                              //  byte[] sendBytes = Encoding.ASCII.GetBytes(data);
                                               // networkStream.Write(sendBytes, 0, sendBytes.Length);
                                                
                                                if ( data == "quit\r\n" ) {
                                                    Console.WriteLine("Quit!");
                                                    break ;
                                                }

                                        }
                                }
                                catch  ( IOException ) { } // Timeout
                                catch  ( SocketException ) {
                                        Console.WriteLine( "Conection is broken!");
                                        break ;
                                }
	               } 
                       Console.WriteLine("Totally read {0}", readBytes); 
                       networkStream.Close() ;
        	       ClientSocket.Close();			
                   Console.WriteLine("Connection closed!");
		}
	}  // Process()

	public void Stop() 	{
		ContinueProcess = false ;
                if ( ClientThread != null  && ClientThread.IsAlive )
		      ClientThread.Join() ;
	}
        
        public  bool Alive {
                get {
                        return  ( ClientThread != null  && ClientThread.IsAlive  );
                }
        }
        
} // class ClientHandler 

