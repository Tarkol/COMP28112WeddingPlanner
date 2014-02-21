using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;

namespace COMP28112ex2
{
    class Program
    {
        static int requestID = 1;
        static String username = "butts";
        static String password = "morebutts";

        static void Main(string[] args)
        {
            var client = new WebClient();
            String test = client.DownloadString("http://www.mspaintadventures.com");
            setReservation(1);
            Console.In.Read();
        }

        public static void setReservation(int slotID)
        {
            StringBuilder requestString = new StringBuilder();
            using (XmlWriter xmlWrite = XmlWriter.Create(requestString))
            {
                xmlWrite.WriteStartDocument();
                xmlWrite.WriteStartElement("reserve");
                xmlWrite.WriteElementString("request_id", requestID.ToString());
                xmlWrite.WriteElementString("username", username);
                xmlWrite.WriteElementString("password", password);
                xmlWrite.WriteElementString("slot_id", slotID.ToString());
                xmlWrite.WriteEndElement();
                xmlWrite.WriteEndDocument();
            }
            Console.Out.WriteLine(requestString);
        }

        //public abstract void clearReservation();

        //public abstract void getFreeSlots();

        //public abstract void getClientsSlots();
    }
}
