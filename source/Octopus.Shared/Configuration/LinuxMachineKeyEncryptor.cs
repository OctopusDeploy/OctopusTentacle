using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Shared.Configuration
{
    public class LinuxMachineKeyEncryptor: IMachineKeyEncryptor
    { 
        static class LinuxMachineKey
        {
            private static string FileName = "/etc/octopus/machinekey";
     
            static void Generate()
            {
                var d = new RijndaelManaged();
                d.GenerateIV();
                d.GenerateKey();
                var raw = Convert.ToBase64String(d.Key) + "." + Convert.ToBase64String(d.IV);
     
                if (!Directory.Exists(Path.GetDirectoryName(FileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FileName));
                }
     
                File.WriteAllText(FileName, raw);
            }
     
            static (byte[] Key, byte[] IV) LoadFromFile()
            {
                var content = File.ReadAllText(FileName).Split('.');
                var key = Convert.FromBase64String(content[0]);
                var iv = Convert.FromBase64String(content[1]);
                return (key, iv);
            }
                 
            public static (byte[] Key, byte[] IV)  Load()
            {
                if (!File.Exists(FileName))
                {
                    Generate();
                }
                return LoadFromFile();
            }
        }
        
        public string Encrypt(string raw)
        {
            var (key, iv) = LinuxMachineKey.Load();
            using (var rijandel = new RijndaelManaged())
            using(var enc = rijandel.CreateEncryptor(key, iv))
            {                    
                var inBlock = Encoding.UTF8.GetBytes(raw);
                var trans = enc.TransformFinalBlock(inBlock, 0, inBlock.Length);
                return Convert.ToBase64String(trans);
            }
        }
            
        public string Decrypt(string encrypted)
        {
            var (key, iv) = LinuxMachineKey.Load();
            using (var rijandel = new RijndaelManaged())
            using(var dec = rijandel.CreateDecryptor(key, iv))
            {
                var fromBase = Convert.FromBase64String(encrypted);
                var asd = dec.TransformFinalBlock(fromBase, 0, fromBase.Length);            
                return Encoding.UTF8.GetString(asd);
            }
        }
    }
}