using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YnetFS;

namespace YnetFS.Messages
{
    public class RequestSynch : Message
    {
        public override void OnRecived(RemoteClient from, Client to)
        {
            base.OnRecived(from, to);
            from.Send(new SynchMessage());
        }
    }
}
