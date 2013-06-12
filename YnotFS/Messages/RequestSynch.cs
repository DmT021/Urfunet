﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YnetFS;

namespace YnetFS.Messages
{
    public class RequestSync : Message
    {
        public override void OnRecived(RemoteClient from, Client to)
        {
            base.OnRecived(from, to);
            from.Send(new SyncMessage());
        }
    }

    public class RequestSync2 : Message
    {
        public override void OnRecived(RemoteClient from, Client to)
        {
            base.OnRecived(from, to);
            var env = Environment;
            if (to.Synchronized && env.IsNearest(from, to, to.RemoteClients.Where(x => x.Synchronized).ToList()))
                from.Send(new SyncMessage());
        }
    }
}
