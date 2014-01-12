using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LeatherLoader.ModList;

namespace DropParty
{
    public class DropPartyInfo : IModInfo
    {
        public DropPartyInfo()
        {

        }

        public string GetModName()
        {
            return "CANVOX-DropParty";
        }

        public string GetModVersion()
        {
            return "1.0.0";
        }

        public string GetPrettyModName()
        {
            return "Drop Party";
        }

        public string GetPrettyModVersion()
        {
            return "Version 1.0";
        }

        public bool CanAcceptModlessClients()
        {
            return true;
        }

        public bool CanConnectToModlessServers()
        {
            return true;
        }

        public string GetCreditString()
        {
            return "By CanVox";
        }
    }
}
