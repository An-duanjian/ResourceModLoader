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
        string refName="";
        string container = "";
        public WrappableFileItem(int priority,string path) : base(priority)
        {
            this.name = Path.GetFileNameWithoutExtension(path) ;
            if (this.name.Contains("@"))
            {
                string[] parts = this.name.Split("@",2);
                this.name= parts[0];
                this.refName = parts[1];
            }
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".gif" || ext == ".bmp")
            {
                this.path = AB.CreateImageAbSingle(path, this.name);
                this.container = "d";
            }
            else if (ext == ".txt" || ext == ".text")
            {
                this.path = AB.CreateTextAbSingle(path, this.name);
                this.container = "2";
            }
            else throw new ArgumentException();
        }

        public static bool IsValid(string path,AddressableMgr addressableMgr)
        {
            string fileName= Path.GetFileName(path);
            if (!fileName.Contains('@') && !addressableMgr.IsAddressableName(fileName))
            {
                return false;
            }
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".png" || ext == ".jpg" || ext == ".gif" || ext == ".bmp")
                return true;
            if (ext == ".txt" || ext == ".text")
                return true;
            return false;
        }


        override public void Apply(ModContext context) {
            if (refName == "")
                context.Redirect(name, path, this.container, "");
            else
                context.NewItem(name, path, this.container, refName);
        }
    }
}
