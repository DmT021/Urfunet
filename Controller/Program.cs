using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Controller
{
    class Program
    {
        static void Main(string[] args)
        {
            var c = new Client();

            bool ExitCmd = false;
            while (!ExitCmd)
            {
                Console.WriteLine("");
                Console.Write("Command> ");

                var str = Console.ReadLine();
                switch (str.ToUpper())
                {
                    case "UP":
                    case "U":
                        {
                            foreach (var item in c.Clients)
                            {
                                item.Send(new mUP());
                            }
                            break;
                        }
                    case "DOWN":
                    case "D":
                        {
                            foreach (var item in c.Clients)
                            {
                                item.Send(new mDown());
                            }
                            break;
                        }
                    case "R":
                        {
                            foreach (var item in c.Clients)
                            {
                                item.Send(new mDown());
                                item.Send(new mUP());
                            }
                            break;
                        }
                    case "QUIT":
                    case "Q": 
                        { ExitCmd = true; break; }
                    default: break;
                }


            }

        }
    }

    public class BaseCilent
    {
        public const int port = 8011;
    }


    public partial class Client : BaseCilent
    {

        public static Client instance { get; set; }

        public ObservableCollection<RemoteClient> Clients { get; set; }
        public RemoteClient SelectedClient { get; set; }
        public ObservableCollection<string> Log { get; set; }
        public ObservableCollection<string> files { get; set; }
        public void WriteLog(string str)
        {
            Console.WriteLine(str);
        }


        public Client()
        {
            instance = this;
            init_settings();

            StartTCPListner();
            StartBroadcastListner();


            new Thread(SayHello).Start();
        }

        private void init_settings()
        {
            Clients = new ObservableCollection<RemoteClient>();

            #region GetCurrentIP
            List<string> ipAdresses = new List<string>();
            ManagementClass mc = new ManagementClass(
              "Win32_NetworkAdapterConfiguration");
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
            IP = ipAdresses[0];
            #endregion
        }


        #region TCP listening

        public string IP { get; set; }
        private void StartTCPListner()
        {
            server = new TcpListener(IPAddress.Parse(IP), port + 1);
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
                        //the client has disconnected from the server
                        break;
                    }

                    //message has successfully been received
                    var encoder = new UTF8Encoding();
                    data += (encoder.GetString(message, 0, bytesRead));
                }



                tcpclient.Close();

                m = Message.Decode(data);

                var IP = IPAddress.Parse(m.FromIP);
                if (!Clients.Any(x => x.IP.Equals(IP)))
                {
                    var client = new RemoteClient(this, IP.ToString());
                    AddClient(client);
                }
                else
                {
                    var client = Clients.First(x => x.IP.Equals(IP));
                    MessageRecived(client, m);
                }

            }
        }

        public void AddClient(RemoteClient client)
        {

            Clients.Add(client); 

        }

        private void MessageRecived(RemoteClient client, Message m)
        {
            if (m == null)
            {
                WriteLog("Empty message from " + client.ToString());
                return;
            }

            ExcecuteCommand(m);

        }


        #endregion

        #region Broadcast listening
        public void StartBroadcastListner()
        {
            sock = new Socket(AddressFamily.InterNetwork,
            SocketType.Dgram, ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            IPEndPoint iep = new IPEndPoint(IPAddress.Any, port);
            sock.Bind(iep);
            ep = (EndPoint)iep;

            CreateReciver();
        }

        public void SendBroadcast(Message m)
        {
            m.FromIP = IP;
            IPEndPoint iep = new IPEndPoint(IPAddress.Broadcast, port);
            byte[] data = Encoding.UTF8.GetBytes(m.Code());
            sock.SendTo(data, iep);
        }

        private void CreateReciver()
        {
            new Thread(() =>
            {
                while (true)
                {
                    var lep = ep;

                    byte[] data = new byte[1024];
                    int recv = sock.ReceiveFrom(data, ref lep);
                    string stringData = Encoding.UTF8.GetString(data, 0, recv);
                    //Log.Add("received: {0} from: {1}",
                    //           stringData, ep.ToString());


                    BrodcastRecived(stringData, null);

                }
            }).Start();
        }
        public Socket sock { get; set; }
        public EndPoint ep { get; set; }


        void BrodcastRecived(object data, EndPoint from)
        {
            var str = data.ToString();
            Message m = Message.Decode(str);
            if (m.FromIP == IP) return;//ignore self messages

            ExcecuteCommand(m);
        }
        #endregion




        private void SayHello()
        {
            WriteLog("Try Hello (5 sec) ...");

            for (int i = 0; i < 10; i++)
            {
                SendBroadcast(new mHello());
                Thread.Sleep(500);
            }
            WriteLog(string.Format("FinishHello. Found {0} clients", Clients.Count));

        }




        public TcpListener server { get; set; }



    }
    public partial class Client
    {

        public void ExcecuteCommand(Message m)
        {
            m.Invoke(this);
        }


    }

    public abstract class Message
    {
        [JsonProperty(PropertyName = "Type")]
        protected virtual string Type { get { return this.GetType().Name; } }

        public static Message Decode(string s)
        {
            var o = JObject.Parse(s).GetValue("Type").ToString();
            var output = FindAllDerivedTypes<Message>(Assembly.GetAssembly(typeof(Message)));

            foreach (var it in output)
                if (it.Name == o)
                    return (Message)JsonConvert.DeserializeObject(s, it);
            throw new Exception("MessageType not found");
        }
        public string Code()
        {
            return JsonConvert.SerializeObject(this);
        }

        [JsonProperty(PropertyName = "Data")]
        public string Data { get; set; }

        [JsonProperty(PropertyName = "FromIP")]
        public string FromIP { get; set; }

        static List<Type> FindAllDerivedTypes<T>(Assembly assembly)
        {
            var derivedType = typeof(T);
            return assembly
                .GetTypes()
                .Where(t =>
                    t != derivedType &&
                    derivedType.IsAssignableFrom(t)
                    ).ToList();

        }

        /// <summary>
        /// действие, которое вызывается у клиета по приходу этого сообщения
        /// </summary>
        /// <param name="c"></param>
        public abstract void Invoke(Client c);

        public RemoteClient Sender
        {
            get { return Client.instance.Clients.FirstOrDefault(x => x.IP.ToString() == FromIP); }
        }

    }

    public class mHello : Message
    {

        public mHello()
        {
        }
        
        public override void Invoke(Client c)
        {
            if (!c.Clients.Any(x => x.IP.ToString() == FromIP))
            {
                var client = new RemoteClient(c, FromIP);
                c.AddClient(client);
                client.Send(new mHI());
            }
        }
    }

    public class mHI : Message
    {
        public mHI()
        {

        }

        public override void Invoke(Client c)
        {
            throw new NotImplementedException();
        }
    }

    public class mUP : Message
    {

        public override void Invoke(Client c)
        {
            c.WriteLog("UpCommandCome");
            if (Process.GetProcessesByName("YnetDashboard").Count() == 0)
                Process.Start("YnetDashboard.exe");
        }
    }
    public class mDown : Message
    {

        public override void Invoke(Client c)
        {
            c.WriteLog("DownCommandCome");
            foreach (var p in Process.GetProcessesByName("YnetDashboard"))
                p.Kill();
        }
    }

    public class RemoteClient : BaseCilent
    {
        public IPAddress IP { get; set; }
        Client from = null;
        public RemoteClient(Client local, string IP)
        {
            from = local;
            this.IP = IPAddress.Parse(IP);
        }

        public void Send(Message m)
        {
            m.FromIP = Client.instance.IP;
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipEndpoint = new IPEndPoint(IP, port + 1);
            listenSocket.Connect(ipEndpoint);
            listenSocket.Send(Encoding.UTF8.GetBytes(m.Code()));
            listenSocket.Close();
        }
        public override string ToString()
        {
            return IP.ToString();
        }

    }
}
