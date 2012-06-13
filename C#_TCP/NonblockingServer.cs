using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Threading;

public class NonblockingServer {
    public static ManualResetEvent clientConnected = new ManualResetEvent(false);
    public byte[] buffer = new byte[1024];

    public static void doBeginAcceptSocket(TcpListener listener) {
        clientConnected.Reset();

        Console.WriteLine("Wait for connection...");

        listener.BeginAcceptSocket(new AsyncCallback(DoAcceptSocketCallback), listener);

        Console.WriteLine("Begin listening...");

        clientConnected.WaitOne();
    }

    public static void DoAcceptSocketCallback(IAsyncResult ar) {
        TcpListener listener = (TcpListener) ar.AsyncState;

        Socket socket = listener.EndAcceptSocket(ar);

        Console.WriteLine("Socket accepted!");
        
        Console.WriteLine("Begin reading...");
        byte[] buffer = new byte[1024];
        socket.BeginReceive(buffer, 0, 1024, 0, new AsyncCallback(ReadCallback), socket);
    }

    public static void ReadCallback(IAsyncResult ar) {
        Socket socket = (Socket) ar.AsyncState;

        int byteRead = socket.EndReceive(ar);

        if (byteRead > 0) {
            Console.WriteLine("{0} received!", byteRead);
            byte[] buffer = new byte[1024];
            socket.BeginReceive(buffer, 0, 1024, 0, new AsyncCallback(ReadCallback), socket);
        } else {
            Console.WriteLine("Connection closed!");
        }
    }

    public static void Main(String[] args) {
        TcpListener listener = new TcpListener(1234);
        listener.Start();
        doBeginAcceptSocket(listener);
    }
}
