using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using YnetFS.InteractionEnvironment;

namespace YnetFS.FileSystem.Mock
{

    public class MockFS : BaseFileSystem
    {
        public MockFS(old_Client client, string rootpath) : base(rootpath) { }

    }
    public class newMockFS : BaseFileSystem
    {
        public newMockFS(Client client, string rootpath) : base(rootpath) { }

    }
}
