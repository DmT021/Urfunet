using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YnetFS.FileSystem.Mock;

namespace YnetFS.FileSystem
{

    public class FileMetaInfo
    {
        public FileMetaInfo() {
            return;
        }
        public FileMetaInfo(BaseFile baseFile)
        {
            if (baseFile == null) throw new ArgumentNullException();
            ParenFile = baseFile;
            //if metafile exists - read and return
            //else (if file exists) create metafile ,save metafile
            var metaexists =File.Exists(baseFile.MetaPath);
            var dataexists =File.Exists(baseFile.RealPath);
            Replics = new List<string>();

            if (metaexists)
            {
                var tmp = JsonConvert.DeserializeObject<FileMetaInfo>(File.ReadAllText(ParenFile.MetaPath, Encoding.UTF8));
                this.CreateDate = tmp.CreateDate;
                this.Hash = tmp.Hash;
                this.Id = tmp.Id;
                this.LastModifiedDate = tmp.LastModifiedDate;
                this.Name = tmp.Name;
                this.Owner = tmp.Owner;
                this.Replics = tmp.Replics;
                
                ParenFile = baseFile;
            }
            else
            {
                if (dataexists)
                {
                    var strHashData = baseFile.data.ComputeHash();
                    Hash = strHashData;
                    Id = Guid.NewGuid().ToString();
                    LastModifiedDate = DateTime.Now;
                    CreateDate = DateTime.Now;
                    Name = baseFile.Name;
                    Save();
                }
            }
        }

        //[JsonProperty(IsReference = true, ItemIsReference = true, ReferenceLoopHandling = ReferenceLoopHandling.Serialize,ItemReferenceLoopHandling=ReferenceLoopHandling.Serialize)]
        [JsonIgnore]
        public BaseFile ParenFile { get; set; }

        public string Name { get; set; }

        [JsonProperty(PropertyName = "Id")]
        public string Id { get; private set; }

        [JsonProperty(PropertyName = "Hash")]
        public string Hash { get; private set; }

        [JsonProperty(PropertyName = "Owner")]
        public string Owner { get; private set; }

        [JsonProperty(PropertyName = "Replics")]
        public List<string> Replics { get; private set; }

        [JsonProperty(PropertyName = "LastModifiedDate")]
        public DateTime LastModifiedDate { get; private set; }

        [JsonProperty(PropertyName = "CreateDate")]
        public DateTime CreateDate { get; private set; }

 
        internal void Save()
        {
            if (File.Exists(ParenFile.MetaPath)) File.Delete(ParenFile.MetaPath);
            File.WriteAllText(ParenFile.MetaPath, JSonPresentationFormatter.Format(JsonConvert.SerializeObject(this)), Encoding.UTF8);
        }

        public void SetOwner(string client)
        {
            Owner = client.ToString();
            Save();
        }

        internal void AddReplica(string r)
        {
            if (Replics == null) Replics = new List<string>();
            if (!Replics.Contains(r))
            {
                Replics.Add(r.ToString());
                Save();
            }
        }
        internal void RemoveReplica(string r)
        {
            if (Replics == null) Replics = new List<string>();
            if (Replics.Contains(r))
            {
                Replics.Remove(r.ToString());
                Save();
            }
        }
        internal void SetHash(string hash)
        {
            Hash = hash;
        }
    }
}
