using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.IO;

namespace COMP28112ex2
{
    class Program
    {
        static int requestID;
        static String username;
        static String password;
        static String urlBand;
        static String urlHotel;

        static void Main(string[] args)
        {
            bool fileOK = true;
            try
            {
                readSettingsFile();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error reading settings file: " + e.Message);
                fileOK = false;
            }
            if (fileOK)
            {
                try
                {
                    setReservation(44);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Unknown HTTP Response: " + e.Message);
                }
                Console.In.Read();
            }
        }

        static void readSettingsFile()
        {
            String[] settings = File.ReadAllLines("clientsettings.txt");
            username = settings[0];
            password = settings[1];
            requestID = Convert.ToInt32(settings[2]);
            urlHotel = settings[3];
            urlBand = settings[4];

            File.WriteAllLines("clientsettings.txt", settings);
        }

        public static void sendRequest(String xml)
        {
            byte[] arr = System.Text.Encoding.UTF8.GetBytes(xml);
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(urlHotel);
            request.Method = "PUT";
            request.ContentType = "text/xml";
            request.Accept = "application/xml";
            //request.ContentLength = arr.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(arr, 0, arr.Length);
            dataStream.Close();
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                


            }
            else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new Exception(response.StatusCode + ": " + response.StatusDescription);
            }
            else
                throw new Exception(response.StatusCode.ToString());

            string returnString = response.StatusCode.ToString();
            Console.WriteLine(returnString);
        }

        public static void setReservation(int slotID)
        {
            StringBuilder requestString = new StringBuilder();
            XmlWriterSettings xmlSettings = new XmlWriterSettings();
            xmlSettings.ConformanceLevel = ConformanceLevel.Fragment;
            xmlSettings.OmitXmlDeclaration = true;
            XmlWriter xmlWrite = XmlWriter.Create(requestString, xmlSettings);
           
            using (xmlWrite)
            {
                xmlWrite.WriteStartElement("reserve");
                xmlWrite.WriteElementString("request_id", requestID.ToString());
                xmlWrite.WriteElementString("username", username);
                xmlWrite.WriteElementString("password", password);
                xmlWrite.WriteElementString("slot_id", slotID.ToString());
                xmlWrite.WriteEndElement();
            }
            Console.Out.WriteLine(requestString);
            sendRequest(requestString.ToString());
        }

        //public abstract void clearReservation();

        //public abstract void getFreeSlots();

        //public abstract void getClientsSlots();
    }
}
