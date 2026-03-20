using ResourceModLoader.Module;
using ResourceModLoader.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ResourceModLoader.Mod.Item
{
    class WrappableFileItem : IModItem
    {
        string name;
        string path;
        string bundle="";
        public WrappableFileItem(int priority,string path) : base(priority)
        {
            this.name = Path.GetFileNameWithoutExtension(path) ;
            if (this.name.Contains("@"))
            {
                string[] parts = this.name.Split("@",2);
                this.name= parts[0];
                this.bundle = parts[1];
            }
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".gif")
                this.path = AB.createImageAbSingle(path, this.name);
            else throw new ArgumentException();
        }

        public static bool IsValid(string path)
        {
            string fileName= Path.GetFileName(path);
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".gif")
                return true;
            return false;
        }


        override public void Apply(ModContext context) {
            context.Redirect(name, path, "d", this.bundle);
        }
    }
}
