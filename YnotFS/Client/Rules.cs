using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using YnetFS.FileSystem;
using YnetFS.InteractionEnvironment;
using YnetFS.Messages;

namespace YnetFS
{
    public abstract class yNotRule
    {
        public static Dictionary<FSObjectEvents, List<yNotRule>> RulePool { get; set; }
        static yNotRule()
        {
            RulePool = new Dictionary<FSObjectEvents, List<yNotRule>>();
            var derivedType = typeof(yNotRule);
            var ruleset = System.Reflection.Assembly.GetAssembly(typeof(yNotRule))
                .GetTypes()
                .Where(t =>
                    t != derivedType &&
                    derivedType.IsAssignableFrom(t)
                    ).ToList();
            foreach (var it in ruleset)
            {
                var r = (yNotRule)it.GetConstructor(new Type[] { }).Invoke(null);
                if (!RulePool.ContainsKey(r.TriggerEvent))
                    RulePool.Add(r.TriggerEvent, new List<yNotRule>());
                RulePool[r.TriggerEvent].Add(r);
            }
        }
        protected Client c { get; set; }
        protected BaseInteractionEnvironment env { get { return c.Environment; } }
        protected abstract FSObjectEvents TriggerEvent { get; }
        public virtual void Eval(Client c, IFSObject obj) { this.c = c; }

    }
    public class rDownloadOnRemoteCreate : yNotRule
    {

        public override void Eval(Client c, IFSObject obj)
        {
            base.Eval(c, obj);
            var srcFile = obj as BaseFile;
            if (c.GetFileReplics(srcFile).Contains(c))/// srcFile.InReplics(c.Id))
            {
                var owner = c.GetFileOwner(srcFile);
                c.Log(LogLevel.Info, "get replica for {0}", srcFile.Name);
                if (owner != null && owner.IsRemote)
                    (owner as RemoteClient).Send(new DownloadFileMessage(srcFile, null));
            }
        }

        protected override FSObjectEvents TriggerEvent
        {
            get
            {
                return FSObjectEvents.remote_create;
            }
        }
    }
    public class rSendDeleteOnLocalDelete : yNotRule
    {

        protected override FSObjectEvents TriggerEvent
        {
            get { return FSObjectEvents.local_delete; }
        }

        public override void Eval(Client c, IFSObject obj)
        {
            base.Eval(c, obj);
            var srcFile = obj as BaseFile;
            c.Log(LogLevel.Info, "send delte file {0} to all", srcFile.Name);
            c.Environment.SendToAll(new DeleteFSObjMessage(srcFile));
        }
    }
    public class rSetMeOwner_SetRandomReplics_SendNewMetaInfo_OnLocalCreate : yNotRule
    {
        protected override FSObjectEvents TriggerEvent
        {
            get { return FSObjectEvents.local_created; }
        }

        public override void Eval(Client c, IFSObject obj)
        {
            base.Eval(c, obj);
            var srcFile = obj as BaseFile;
            c.Log(LogLevel.Info, "become owner for {0}", srcFile.Name);
            srcFile.SetOwner(c.Id.ToString());

            lock (c.RemoteClients)
            {
                foreach (var r in c.GetRandomClients(2, c.RemoteClients))
                    srcFile.AddReplica(r.Id.ToString());

            }
            c.Log(LogLevel.Info, "Sending meta to all", srcFile.Name);
            env.SendToAll(new NewFileMessage(srcFile));
        }
    }

}
