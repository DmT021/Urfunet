using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using YnetFS.Messages;

namespace YnetFS.InteractionEnvironment
{
    static class NetworkSettings
    {
        public static int StdTctPort = 24800;
        public static int StdBroadcastPort = 24800;
        public readonly static List<int> DataSendingPortrange = Enumerable.Range(24900, 24999).ToList();
   
    }

    public class NetworkIE : BaseInteractionEnvironment
    {
        public IPAddress IP { get; set; }

        public NetworkIE(Client Client):base (Client)
        {
            IP = GetCurrentIP();
            Addresses = new Dictionary<RemoteClient, IPAddress>();
        }

        public override void BootStrap()
        {
            //start tcp
            StartTCPListner();
            //start broadcast & say hello

            StartBroadcastListner();
            new Thread(() =>
            {

                for (int i = 0; i < 10; i++)
                {
                    SendBroadcast(new mHello());
                    Thread.Sleep(500);
                }
            }).Start();
        }

        Socket UdpSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private void StartBroadcastListner()
        {

            UdpSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            IPEndPoint iep = new IPEndPoint(IPAddress.Any, NetworkSettings.StdBroadcastPort);
            UdpSock.Bind(iep);
            EndPoint ep = (EndPoint)iep;

            new Thread(() =>
            {
                while (true)
                {
                    var lep = ep;

                    byte[] data = new byte[1024];
                    int recv = UdpSock.ReceiveFrom(data, ref lep);
                    string stringData = Encoding.UTF8.GetString(data, 0, recv);

                    Message m = Message.Decode(stringData, this);
                    if (ParentClient.Id == (m._fromId)) return;
                    m.OnRecived(null, ParentClient);
                }
            }).Start();
        }
        void SendBroadcast(NetworkMessage m)
        {
            IPEndPoint iep = new IPEndPoint(IPAddress.Broadcast, NetworkSettings.StdBroadcastPort);
            byte[] data = Encoding.UTF8.GetBytes(m.Code());
            UdpSock.SendTo(data, iep);
        }

        #region TCP Interaction

        TcpListener server { get; set; }
        private void StartTCPListner()
        {
            server = new TcpListener(IP, NetworkSettings.StdTctPort);
            server.Start();

            new Thread(StartListen).Start();
        }

        private void StartListen(object obj)
        {
            // listenSocket.Listen(5);
            string data = string.Empty;
            while (true)
            {
                Message m = null;

                TcpClient tcpclient = server.AcceptTcpClient();
                NetworkStream stream = tcpclient.GetStream();

                byte[] message = new byte[4096];
                int bytesRead;

                data = string.Empty;
                while (true)
                {
                    bytesRead = 0;

                    try
                    {
                        //blocks until a client sends a message
                        bytesRead = stream.Read(message, 0, 4096);
                    }
                    catch
                    {
                        //a socket error has occured
                        throw new Exception("WTF - Socket error???");
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        //the client has disconnected sourcelist the server
                        break;
                    }

                    //message has successfully been received
                    var encoder = new UTF8Encoding();
                    data += (encoder.GetString(message, 0, bytesRead));
                }



                tcpclient.Close();

                m = Message.Decode(data, this);

                string Id = (m._fromId);
                if (RemoteClients.Any(x=>x.Id==Id))
                {
                    var c = RemoteClients.First(x=>x.Id==Id);
                    OnMessageRecived(c, m);
                }
                else
                {
                    //DOTO: Inplement message recive sourcelist unknown remote client

                }
            }
        }
        #endregion

        IPAddress GetCurrentIP()
        {
            List<string> ipAdresses = new List<string>();
            ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = mc.GetInstances();

            foreach (ManagementObject mo in moc)
            {
                // Make sure this is a IP enabled device. 
                // Not something like memory card or VM Ware
                if (((bool)mo["ipEnabled"]))
                {
                    ipAdresses.Add(((string[])mo["IPAddress"])[0]);
                }
            }
            return IPAddress.Parse(ipAdresses[0]);
        }


        public Dictionary<RemoteClient, IPAddress> Addresses { get; set; }

        public override void Send(RemoteClient RemoteClient, Message message)
        {
            if (!(message is NetworkMessage)) throw new Exception("Network IE: Can send only networkmessage");

            message._fromId = RemoteClient.Id.ToString();
            (message as NetworkMessage).FromIP = Addresses[RemoteClient].ToString();
        }

        public override bool CheckRemoteClientState(RemoteClient rc)
        {
            return false;
        }
    }


}
