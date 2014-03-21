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
    const int MAX_ATTEMPTS = 10;
    const int TIMEOUT = 10000;
    static int requestID;
    static String username;
    static String password;
    static String urlBand;
    static String urlHotel;
    enum QueryType { reserve, cancel, availability, bookings };

    static void Main(string[] args)
    {
      try
      {
        readSettings();
      }
      catch (Exception e)
      {
        Console.Error.WriteLine("Error reading settings file: " + e.Message);
        Environment.Exit(0);
      }
      Console.Out.WriteLine("Choose operation:");
      Console.Out.WriteLine("0: Reserve a slot.");
      Console.Out.WriteLine("1: Cancel a slot.");
      Console.Out.WriteLine("2: Get all available slots.");
      Console.Out.WriteLine("3: Get slots booked by this client.");
      int selection = Convert.ToInt32(Console.In.ReadLine());
      switch (selection)
      {
        case (int)QueryType.reserve:
          Console.Out.WriteLine("Reserve which slot?");
          int slot = Convert.ToInt32(Console.In.ReadLine());
          Console.Out.WriteLine(makeReservation(slot));
          break;
        case (int)QueryType.cancel:
          Console.Out.WriteLine("Cancel which slot?");
          slot = Convert.ToInt32(Console.In.ReadLine());
          Console.Out.WriteLine(cancelReservation(slot));
          break;
        case (int)QueryType.availability:
          Console.Out.WriteLine(getFreeSlots());
          break;
        case (int)QueryType.bookings:
          Console.Out.WriteLine(getClientSlots());
          break;
        default:
          Console.Out.WriteLine("Invalid option.");
          break;
      }
      Console.In.Read();
    }

    static void readSettings()
    {
      username = WeddingPlanner.Properties.Resources.userName;
      password = WeddingPlanner.Properties.Resources.password;
      requestID = Convert.ToInt32(File.ReadAllText("../../requestID.txt"));
      urlHotel = WeddingPlanner.Properties.Resources.urlHotel;
      urlBand = WeddingPlanner.Properties.Resources.urlBand;

      File.WriteAllText("../../requestID.txt", (requestID + 1).ToString());
    }

    static HttpWebResponse sendRequest(String message, String url)
    {
      byte[] arr = System.Text.Encoding.UTF8.GetBytes(message);
      HttpWebResponse response = null;
      int attempts = 0;
      do
      {
        HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
        request.Method = "PUT";
        request.ContentType = "text/xml";
        request.Accept = "application/xml";
        request.Timeout = TIMEOUT;

        Stream dataStream = request.GetRequestStream();
        dataStream.Write(arr, 0, arr.Length);
        dataStream.Close();

        try
        {
          response = (HttpWebResponse)request.GetResponse();
          return response;
        }
        catch (WebException e)
        {
          response = ((HttpWebResponse)e.Response);
          if ((int)response.StatusCode == 503)
            Console.Out.WriteLine("Server is unavailable.");
          else
            Console.Out.WriteLine("Unknown error.");
          attempts++;
          if (attempts < MAX_ATTEMPTS)
          {
            Console.Out.WriteLine("Retrying...");
            System.Threading.Thread.Sleep(TIMEOUT);
          }
        }
      } while (attempts < MAX_ATTEMPTS);
      return null;
    }

    static HttpWebResponse getRequestStatus(String url)
    {
      HttpWebResponse response = null;
      int attempts = 0;
      do
      {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        try
        {
          response = (HttpWebResponse)request.GetResponse();
          return response;
        }
        catch (WebException e)
        {
          response = ((HttpWebResponse)e.Response);
          if ((int)response.StatusCode == 503)
            Console.Out.WriteLine("Server is unavailable.");
          else if ((int)response.StatusCode == 404)
            Console.Out.WriteLine("Request not yet processed.");
          else if ((int)response.StatusCode == 401)
          {
            Console.Out.WriteLine("Invalid username/password");
            return null;
          }
          else
            Console.Out.WriteLine("Unknown error.");
          attempts++;
          if (attempts < MAX_ATTEMPTS)
          {
            Console.Out.WriteLine("Retrying...");
            System.Threading.Thread.Sleep(TIMEOUT);
          }
        }
      } while (attempts < MAX_ATTEMPTS);
      return null;
    }

    static string buildRequest(int type, int slotID)
    {
      StringBuilder requestString = new StringBuilder();
      XmlWriterSettings xmlSettings = new XmlWriterSettings();
      xmlSettings.ConformanceLevel = ConformanceLevel.Fragment;
      xmlSettings.OmitXmlDeclaration = true;
      XmlWriter xmlWrite = XmlWriter.Create(requestString, xmlSettings);

      using (xmlWrite)
      {
          xmlWrite.WriteStartElement(Enum.GetName(typeof(QueryType), type));
          xmlWrite.WriteElementString("request_id", requestID.ToString());
          xmlWrite.WriteElementString("username", username);
          xmlWrite.WriteElementString("password", password);
          if (type == (int)QueryType.reserve || type == (int)QueryType.cancel)
            xmlWrite.WriteElementString("slot_id", slotID.ToString());
          xmlWrite.WriteEndElement();
      }
      return requestString.ToString();
    }

    static string makeReservation(int slotID)
    {
      Console.Out.WriteLine("Sending reservation request for slot " + slotID);
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.reserve, slotID), urlBand);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = ""; 
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        Console.Out.WriteLine("Retrieving server response.");
        HttpWebResponse responseGET = getRequestStatus(responseText);
        if (responseGET != null)
        {
          using (XmlReader reader = getReader(responseGET))
          {
             reader.ReadToFollowing("code");
            if (reader.ReadElementContentAsString() == "200")
              responseText = "Slot " + slotID + " has been booked.";
            else 
              responseText = reader.ReadElementContentAsString();
          }
          return responseText;
        }
      }
      return "Operation failed.\n";
    }

    static string cancelReservation(int slotID)
    {
      Console.Out.WriteLine("Cancelling reservation in slot " + slotID);
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.cancel, slotID), urlBand);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = "";
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        Console.Out.WriteLine("Retrieving server response.");
        HttpWebResponse responseGET = getRequestStatus(responseText);
        if (responseGET != null)
          return "The reservation has been cancelled";
      }
      return "Operation failed.\n";
    }

    static XmlReader getReader(HttpWebResponse response)
    {
      return XmlReader.Create(new StringReader(new StreamReader(response.GetResponseStream()).ReadToEnd()));
    }

    static string getFreeSlots()
    {
      Console.Out.WriteLine("Requesting available slots.");
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.availability, 0), urlBand);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = "";
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        Console.Out.WriteLine("Retrieving server response.");
        HttpWebResponse responseGET = getRequestStatus(responseText);
        if (responseGET != null)
        {
          using (XmlReader reader = getReader(responseGET))
          {
            responseText = "The following slots are free:\n";
            reader.ReadToFollowing("slot_id");
            do
            {
              responseText += reader.ReadElementContentAsString() + "\n";
            } while (reader.IsStartElement("slot_id"));
          }
          return responseText;
        }
      }
      return "Operation failed.\n";
    }

    static string getClientSlots()
    {
      Console.Out.WriteLine("Requesting slots booked by this client.");
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.bookings, 0), urlBand);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = "";
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        Console.Out.WriteLine("Retrieving server response.");
        HttpWebResponse responseGET = getRequestStatus(responseText);
        if (responseGET != null)
        {
          using (XmlReader reader = getReader(responseGET))
          {
            responseText = "The following slots are booked by this client:\n";
            if (reader.ReadToFollowing("slot_id"))
              do
              {
                responseText += reader.ReadElementContentAsString() + "\n";
              } while (reader.IsStartElement("slot_id"));
          }
          return responseText;
        }
      }
      return "Operation failed.\n";
    }
  }
}
