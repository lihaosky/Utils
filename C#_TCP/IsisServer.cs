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
	delegate void insert(string command, int rank);
	delegate void query(string command, int rank);
	
	public  class SynchronousSocketListener {
		private static int nodeNum = -1;              //Node number. Has to be specified
		private static int myRank = -1;               //My rank. Has to be specified
		private static int shardSize = -1;            //Shard size    
		private static bool[] groupJoin;              //If all member in a group join
		private static bool allJoin = false;          //If all the member has join
		
		private static int portNum = 1234;            //Default port number 1234
		private static int memPortNum = 9999;         //Default memcached port number 9999
		private static ArrayList ClientSockets;       //Array to store client sockets
		private static bool ContinueReclaim = true;   //If continue reclaim
	 	private static Thread ThreadReclaim;          //Reclaim thread
		private static bool isVerbose = false;        //Is verbosely print out message
		
		private static Group[] shardGroup;            //Shard group
		private static int INSERT = 0;      //Insert number
		private static int GET = 1;         //Get number
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
	 	            		Console.WriteLine("Client#{0} accepted!", ++ClientNbr);
		           	    }
		           	    // An incoming connection needs to be processed.
		            	lock( ClientSockets.SyncRoot ) {
		                	int i = ClientSockets.Add(new ClientHandler(handler, shardGroup, timeout, isVerbose));
		                    ((ClientHandler) ClientSockets[i]).Start();
		                }
		            }            
		        }
		        listener.Stop();
		          
		        ContinueReclaim = false ;
		        ThreadReclaim.Join();
		          
		        foreach ( Object Client in ClientSockets ) {
		        	( (ClientHandler) Client ).Stop();
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
		        Thread.Sleep(200);
		    }         
	  	}
	  	
	  	private static void printUsage() {
	  		Console.WriteLine("Usage:");
	  		Console.Write("-p: listening port number. Default: 1234\n" + 
	  					  "-m: memcached port number. Default: 9999\n" +
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
	  		groupJoin = new bool[shardSize];
	  		
	  		int groupNum = myRank;
	  		for (int i = 0; i < shardSize; i++) {
	  			shardGroup[i] = new Group("group"+groupNum);
	  			groupJoin[i] = false;
	  			
	  			groupNum--;
	  			if (groupNum < 0) {
	  				groupNum += nodeNum;
	  			}
	  		}
	  		
	  		for (int i = 0; i < shardSize; i++) {
	  			int local = i;
	  			
	  			//Insert handler
	  			shardGroup[i].Handlers[INSERT] += (insert)delegate(string command, int rank) {
	  				if (isVerbose) {
	  					Console.WriteLine("Got a command {0}", command);
	  				}
	  				
	  				if (shardGroup[local].GetView().GetMyRank() == rank) {
	  					if (isVerbose) {
	  						Console.WriteLine("Got a message from myself!");
	  					}
	  					shardGroup[local].Reply("Yes");
	  				} else {
	  					string ret = talkToMem(command, INSERT);
	  					if (ret == "STORED") {
	  						shardGroup[local].Reply("Yes");
	  					} else {
	  						shardGroup[local].Reply("No");
	  					}
	  				}
	  			};
	  			
	  			//Get handler
	  			shardGroup[i].Handlers[GET] += (query)delegate(string command, int rank) {
	  				if (isVerbose) {
	  					Console.WriteLine("Got a command {0}", command);
	  				}
  					if (shardGroup[local].GetView().GetMyRank() == rank) {
  						if (isVerbose) {
  							Console.WriteLine("Got a message from myself!");
  						}
  						shardGroup[local].Reply("END\r\n"); //Definitely not presented in local memcached!
  					} else {
  						string ret = talkToMem(command, GET);
  						shardGroup[local].Reply(ret);
  					}
	  			};
	  			
	  			//View handler
	  			shardGroup[i].ViewHandlers += (Isis.ViewHandler)delegate(View v) {
	  				if (isVerbose) {
	  					Console.WriteLine("Got a new view {0}" + v);
	  					Console.WriteLine("Group {0} has {1} members", local, shardGroup[local].GetView().GetSize());
	  				}
	  				
	  				if (shardGroup[local].GetView().GetSize() == shardSize) {
	  					groupJoin[local] = true;
	  				}
	  				
	  				bool isAll = true;
	  				for (int j = 0; j < shardSize; j++) {
	  					if (groupJoin[j] == false) {
	  						isAll = false;
	  						break;
	  					}
	  				}
	  				
	  				if (isAll) {
	  					allJoin = true;
	  					if (isVerbose) {
	  						Console.WriteLine("All the members have joined!");
	  					}
	  				}
	  			};
	  		}
	  		
	  		for (int i = 0; i < shardSize; i++) {
	  			shardGroup[i].Join();
	  		}
	  	}
	  	
	  	//Talk to local memcached
	  	private static string talkToMem(string command, int commandType) {
	  		TcpClient client = new TcpClient();
	  		string line = "";
	  		string reply = "";
	  		
	  		try {
	  			client.Connect("localhost", memPortNum);
	  			NetworkStream ns = client.GetStream();
	  			byte[] sendBytes = Encoding.ASCII.GetBytes(command);
	  			StreamReader reader = new StreamReader(client.GetStream(), System.Text.Encoding.ASCII);
	  			
	  			//Send command to local memcached
	  			if (ns.CanRead && ns.CanWrite) {
	  				ns.Write(sendBytes, 0, sendBytes.Length);
	  			}
	  			
	  			if (commandType == INSERT) {
  					line = reader.ReadLine();
  					reply = line;
  					client.Close();
	  			} else if (commandType == GET) {
  					while ((line = reader.ReadLine()) != null) {
  						reply += line;
  						reply += "\r\n";
  						
  						if (line == "END") {
  							break;
  						}
  					}
  					client.Close();
	  			}
	  		} catch (Exception e) {
	  			Console.WriteLine("Exception in talking to memcached!");
	  			if (client != null) {
	  				client.Close();
	  			}
	  		}	
	  			return reply;
	  	}
	  	
	  	//Main
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
				} else if (args[i] == "-m") {
					memPortNum = Int32.Parse(args[++i]);
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
			while (allJoin == false);
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
		const int INSERT_CMD = 0;
		const int GET_CMD = 1;
		bool isVerbose;
		
		public ClientHandler (TcpClient ClientSocket, Group[] myGroup, int timeout, bool isVerbose) {
			this.ClientSocket = ClientSocket;
			this.myGroup = myGroup;
			this.timeout = new Isis.Timeout(timeout, Isis.Timeout.TO_FAILURE);
			this.isVerbose = isVerbose;
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
					int commandType = 0;
					
					while ((line = reader.ReadLine()) != null) {
						if (isVerbose) {
							Console.WriteLine("Received a line {0} from client", line);
						}
						
						if (line == "insert") {
							commandType = INSERT_CMD;
							continue;
						}
						
						if (line == "get") {
							commandType = GET_CMD;
							continue;
						}
						
						//End of command, use ISIS to send the command!
						if (line == "") {
							if (isVerbose) {
								Console.WriteLine("Send command to other nodes. Wait for reply!");
							}
							
							List<string> replyList = new List<string>();
							
							int	nr = myGroup[0].Query(Group.ALL, timeout, commandType, command, myGroup[0].GetView().GetMyRank(), new EOLMarker(), replyList);

							if (isVerbose) {
								foreach (string s in replyList) {
									Console.WriteLine("Received reply {0}", s);
								}
							}
							
							byte[] sendBytes;
							string reply;
							//Send reply to memcached
							switch (commandType) {
								//Insert reply
								case INSERT_CMD:
									reply = "OK.\n";
									sendBytes = Encoding.ASCII.GetBytes(reply);
									networkStream.Write(sendBytes, 0, sendBytes.Length);
									break;
								
								//Get reply
								case GET_CMD:
									reply = "END\r\n";
									foreach (string s in replyList) {
										if (s != "END\r\n") {
											reply = s;
											break;
										}
									}
									sendBytes = Encoding.ASCII.GetBytes(reply);
									networkStream.Write(sendBytes, 0, sendBytes.Length);
									break;
							}
							
							command = "";
						} else {
							command += line;
							command += "\r\n";
						}
					}
				}
			   	 
		        networkStream.Close();
		    	ClientSocket.Close();			
		    	
		    	if (isVerbose) {
		        	Console.WriteLine("Connection closed!");
		        }
			}
		}  // Process()

		public void Stop() 	{
			ContinueProcess = false;
		    if (ClientThread != null && ClientThread.IsAlive) {
				ClientThread.Join();
			}
		}
		    
		public bool Alive {
			get {
				return  (ClientThread != null  && ClientThread.IsAlive);
			}
	   	}      
	} 
}

