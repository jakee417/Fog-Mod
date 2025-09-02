using StardewValley;
using Netcode;


namespace FogMod
{
    public class FoggyLocation : GameLocation
    {
        private readonly NetCollection<FogMod.NetGrouse> grouse = new NetCollection<FogMod.NetGrouse>();

        protected override void initNetFields()
        {
            base.initNetFields();
            NetFields.AddField(grouse);
        }

        public FoggyLocation()
            : base()
        {
            name.Value = string.Empty;
        }

        public FoggyLocation(string name)
            : this()
        {
            base.name.Value = name;
        }

        public void Clear()
        {
            grouse.Clear();
        }

        public NetCollection<FogMod.NetGrouse> Grouse
        {
            get
            {
                return grouse;
            }
        }
    }
}
