using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using Isis;

namespace IsisService {
	delegate void query(string command);
	
	public  class SynchronousSocketListener {
		private static int nodeNum = -1;              //Node number. Has to be specified
		private static int myRank = -1;               //My rank. Has to be specified
		private static int shardSize = -1;            //Shard size    
		
		private static int portNum = 1234;            //Default port number 1234
		private static ArrayList ClientSockets;       //Array to store client sockets
		private static bool ContinueReclaim = true;   //If continue reclaim
	 	private static Thread ThreadReclaim;          //Reclaim thread
		private static bool isVerbose = false;        //Is verbosely print out message
		
		private static Group[] shardGroup;            //Shard group
		private static int QUERY = 0;                 //Query number
		private static int timeout = 15000;           //Timeout. Default: 15 sec
		
	  	public  static  void StartListening() {
			ClientSockets = new ArrayList() ;
			int ClientNbr = 0;
			ThreadReclaim = new Thread(new ThreadStart(Reclaim));
			ThreadReclaim.Start() ;
		
			TcpListener listener = new TcpListener(portNum);
			try {
		    	listener.Start();
		    
		        // Start listening for connections.
		        if (isVerbose) {
		        	Console.WriteLine("Waiting for a connection...");
		        }
		        while (true) {
		        	TcpClient handler = listener.AcceptTcpClient();
		                    
		            if (handler != null)  {
		            	if (isVerbose) {
	 	            		Console.WriteLine("Client#{0} accepted!", ++ClientNbr) ;
		           	    }
		           	    // An incoming connection needs to be processed.
		            	lock( ClientSockets.SyncRoot ) {
		                	int i = ClientSockets.Add(new ClientHandler(handler, shardGroup, timeout, QUERY)) ;
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
		        	for (int x = ClientSockets.Count-1; x >= 0; x-- )  {
		                    Object Client = ClientSockets[x];
		                    if (!( ( ClientHandler ) Client ).Alive )  {
		                    	ClientSockets.Remove( Client );
		                    	if (isVerbose) {
		                        	Console.WriteLine("A client left");
		                        }
		                    }
		            }
		        }
		        Thread.Sleep(200) ;
		    }         
	  	}
	  	
	  	private static void printUsage() {
	  		Console.WriteLine("Usage:");
	  		Console.Write("-p: port number. Default: 1234\n" + 
	  		              "-v: is verbose. Default: no\n" + 
	  		              "-n: total node number. Has to be specified\n" + 
	  		              "-r: my rank. Has to be specified\n" +
	  		              "-t: timeout for ISIS query. Default: 15 sec" +
	  		              "-s: shard size. Has to be specifed\n");
	  	}
	  	
	  	private static void createGroup() {
	  		IsisSystem.Start();
	  		if (isVerbose) {
	  			Console.WriteLine("Isis system started!");
	  		}
	  		
	  		shardGroup = new Group[shardSize];
	  		int groupNum = myRank;
	  		for (int i = 0; i < shardSize; i++) {
	  			shardGroup[i] = new Group("group"+groupNum);
	  			
	  			groupNum--;
	  			if (groupNum < 0) {
	  				groupNum += nodeNum;
	  			}
	  		}
	  		
	  		for (int i = 0; i < shardSize; i++) {
	  			shardGroup[i].Handlers[QUERY] += (query)delegate(string command) {
	  				if (isVerbose) {
	  					Console.WriteLine("Got a command {0}" + command);
	  				}
	  				shardGroup[i].Reply("Yes");
	  			};
	  			shardGroup[i].ViewHandlers += (Isis.ViewHandler)delegate(View v) {
	  				if (isVerbose) {
	  					Console.WriteLine("Got a new view {0}" + v);
	  				}
	  			};
	  		}
	  		
	  		for (int i = 0; i < shardSize; i++) {
	  			shardGroup[i].Join();
	  		}
	  	}
	  	
	  	public  static  int Main(String[] args) {
			int i = 0;
			while (i < args.Length) {
				//port number
				if (args[i] == "-p") {
					portNum = Int32.Parse(args[++i]);
					i++;
				} else if (args[i] == "-v") {
					isVerbose = true;
					i++;
				} else if (args[i] == "-n") {
					nodeNum = Int32.Parse(args[++i]);
					i++;
				} else if (args[i] == "-r") {
					myRank = Int32.Parse(args[++i]);
					i++;
				} else if (args[i] == "-s") {
					shardSize = Int32.Parse(args[++i]);
					i++;
				} else if (args[i] == "-t") {
					timeout = Int32.Parse(args[++i]);
					i++;
				} else {
					Console.WriteLine("Unknown argument!");
					printUsage();
					return 0;
				}
			}
			
			if (nodeNum == -1 || myRank == -1 || shardSize == -1) {
				Console.WriteLine("Total node number, my rank and shard size have to be specified!");
				printUsage();
				return 0;
			}
			
			if (myRank >= nodeNum) {
				Console.WriteLine("Your rank can't be equal to or larger than total node number!");
				return 0;
			}
			
			if (shardSize > nodeNum) {
				Console.WriteLine("Shard size can't be larger than node number!");
				return 0;
			}
			
			if (isVerbose) {
				Console.WriteLine("Listening port number is {0}", portNum);
				Console.WriteLine("Total node number is {0}", nodeNum);
				Console.WriteLine("My rank is {0}", myRank);
				Console.WriteLine("Shard size is {0}", shardSize);
			}
			
			createGroup();
			StartListening();
			return 0;
	  	}
	}

	class ClientHandler {

		TcpClient ClientSocket ;
		bool ContinueProcess = false;
		Thread ClientThread;
		Group[] myGroup;
		Isis.Timeout timeout;
		int QUERY;
		
		public ClientHandler (TcpClient ClientSocket, Group[] myGroup, int timeout, int query) {
			this.ClientSocket = ClientSocket;
			this.myGroup = myGroup;
			this.timeout = new Isis.Timeout(timeout, Isis.Timeout.TO_FAILURE);
			QUERY = query;
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
					string command = "";
					while ((line = reader.ReadLine()) != null) {
						//End of command, use ISIS to send the command!
						if (line == "") {
							List<string> replyList = new List<string>();
							int nr = myGroup[0].Query(Group.ALL, timeout, QUERY, command, new EOLMarker(), replyList);
							foreach (string s in replyList) {
								Console.WriteLine(s);
							}
							command = "";
						} else {
							command += line;
							command += "\r\n";
						}
					}
				}
			   	 
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
}

