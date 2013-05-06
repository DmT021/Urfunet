using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YnetFS.FileSystem
{

    public class FileMetaInfo
    {
        //[JsonProperty(IsReference = true, ItemIsReference = true, ReferenceLoopHandling = ReferenceLoopHandling.Serialize,ItemReferenceLoopHandling=ReferenceLoopHandling.Serialize)]
        [JsonIgnore]
        public IFile ParenFile { get; set; }

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

        #region DoNtLikeThat REFACTOR
        internal static FileMetaInfo ReadFrom(IFile Ifile)
        {
            var ret = JsonConvert.DeserializeObject<FileMetaInfo>(File.ReadAllText(Ifile.MetaPath));
            ret.ParenFile = Ifile;
            return ret;
        }
        public static FileMetaInfo CreateFromFile(IFile IFile)
        {
            byte[] md5data = null;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(IFile.RealPath))
                {
                    md5data = md5.ComputeHash(stream);
                }
            }
            var strHashData = System.BitConverter.ToString(md5data);
            strHashData = strHashData.Replace("-", "");
            return new FileMetaInfo()
            {
                Hash = strHashData,
                Id = Guid.NewGuid().ToString(),
                LastModifiedDate = DateTime.Now,
                CreateDate=DateTime.Now,
                ParenFile = IFile,
                Name=IFile.Name
            };
        } 
        #endregion

        internal void Save()
        {
            if (File.Exists(ParenFile.MetaPath)) File.Delete(ParenFile.MetaPath);
            File.WriteAllText(ParenFile.MetaPath, JsonConvert.SerializeObject(this), Encoding.Default);
        }

        public void SetOwner(Client client)
        {
            Owner = client.Id.ToString();
            Save();
        }

       
    }
}
