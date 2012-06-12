using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Threading;


public  class SynchronousSocketListener {
        
  private  const int   portNum = 1234 ;
  private  static  ArrayList ClientSockets  ;
  private  static   bool ContinueReclaim =  true;
  private  static   Thread ThreadReclaim ;
  private  static int totalToRecv = 0;

  public  static  void StartListening() {
    ClientSockets = new ArrayList() ;
    
    ThreadReclaim = new Thread( new ThreadStart(Reclaim) );
    ThreadReclaim.Start() ;
    
    TcpListener listener = new TcpListener(portNum);
    try {
              listener.Start();
        
              int ClientNbr = 0 ;
        
              // Start listening for connections.
              Console.WriteLine("Waiting for a connection...");
              while (true) {
                      
                        TcpClient handler = listener.AcceptTcpClient();
                        
                        if (  handler != null)  {
                                Console.WriteLine("Client#{0} accepted!", ++ClientNbr) ;
                                // An incoming connection needs to be processed.
                                lock( ClientSockets.SyncRoot ) {
                                        int i = ClientSockets.Add(new ClientHandler(handler, totalToRecv)) ;
                                        ((ClientHandler) ClientSockets[i]).Start() ;
                                }
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
    totalToRecv = Int32.Parse(args[0]);
    Console.WriteLine("Total to receive is {0} bytes", totalToRecv);
    StartListening();
    return 0;
  }
}

class ClientHandler {

	TcpClient ClientSocket ;
	bool ContinueProcess = false ;
	Thread ClientThread ;
        int totalToRecv;

	public ClientHandler (TcpClient ClientSocket, int tTR) {
		this.ClientSocket = ClientSocket ;
                totalToRecv = tTR;
	}

	public void Start() {
		ContinueProcess = true ;
		ClientThread = new Thread ( new ThreadStart(Process) ) ;
		ClientThread.Start() ;
	}

	private  void Process() {
		// Data buffer for incoming data.
		byte[] bytes;
                int readBytes = 0;

		if ( ClientSocket != null ) {
                        NetworkStream networkStream = ClientSocket.GetStream();
                      //ClientSocket.ReceiveTimeout = 100 ; // 1000 miliseconds

			while (readBytes < totalToRecv) {
                                bytes = new byte[ClientSocket.ReceiveBufferSize];
                                try {
                                        int BytesRead = networkStream.Read(bytes, 0, (int)ClientSocket.ReceiveBufferSize);
                                        readBytes += BytesRead;

                                        Console.WriteLine("Read {0}", BytesRead);
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

