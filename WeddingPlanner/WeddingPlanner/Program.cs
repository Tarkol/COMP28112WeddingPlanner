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
    const int TIMEOUT = 2000;
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
      Console.Out.WriteLine("Select server:");
      Console.Out.WriteLine("(H)otel");
      Console.Out.WriteLine("(B)and");
      Console.Out.WriteLine("(P)air of slots");
      string op = Console.In.ReadLine();
      if (op.ToUpper() == "H")
        op = urlHotel;
      else if (op.ToUpper() == "B")
        op = urlBand;
      else if (op.ToUpper() == "P")
      {
        int slotpair = reservePair();
        Console.Out.WriteLine("Reserved band and hotel slots " + slotpair);
        Console.In.ReadLine();
        Environment.Exit(0);
      }
      else
      {
        Console.Out.WriteLine("Not a valid server.");
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
          int targetSlot = Convert.ToInt32(Console.In.ReadLine());
          Console.Out.WriteLine(makeReservation(targetSlot, op));
          break;
        case (int)QueryType.cancel:
          Console.Out.WriteLine("Cancel which slot?");
          targetSlot = Convert.ToInt32(Console.In.ReadLine());
          Console.Out.WriteLine(cancelReservation(targetSlot, op));
          break;
        case (int)QueryType.availability:
          List<int> freeSlots = getFreeSlots(op);
          Console.Out.WriteLine("The following slots are available:");
          foreach (int slot in freeSlots)
            Console.Out.WriteLine(slot);
          break;
        case (int)QueryType.bookings:
          List<int> bookedSlots = getClientSlots(op);
          foreach (int slot in bookedSlots)
            Console.Out.WriteLine(slot);
          break;
        default:
          Console.Out.WriteLine("Invalid option.");
          break;
      }
      Console.In.Read();
    }

    //Reads settings into variables.
    static void readSettings()
    {
      username = WeddingPlanner.Properties.Resources.username;
      password = WeddingPlanner.Properties.Resources.password;
      requestID = Convert.ToInt32(File.ReadAllText("../../requestID.txt"));
      urlHotel = WeddingPlanner.Properties.Resources.urlHotel;
      urlBand = WeddingPlanner.Properties.Resources.urlBand;
    }

    //Sends a request to the server.
    static HttpWebResponse sendRequest(String message, String url)
    {
      byte[] arr = System.Text.Encoding.UTF8.GetBytes(message);
      HttpWebResponse response = null;
      int attempts = 0;
      while (true)
      {
        HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
        request.Method = "PUT";
        request.ContentType = "text/xml";
        request.Accept = "application/xml";

        Stream dataStream = request.GetRequestStream();
        dataStream.Write(arr, 0, arr.Length);
        dataStream.Close();

        incrementID();

        try
        {
          response = (HttpWebResponse)request.GetResponse();
          return response;
        }
        catch (WebException e)
        {
          response = ((HttpWebResponse)e.Response);
          Console.Out.Write("(" + attempts + ") ");
          if ((int)response.StatusCode == 503)
            Console.Out.Write("Server is unavailable.");
          else
            Console.Out.Write("Unknown error.");
          attempts++;
          System.Threading.Thread.Sleep(TIMEOUT);
          clearConsoleLine();
        }
      }
      return null;
    }

    //Checks the status of a request from the server.
    static HttpWebResponse getRequestStatus(String url)
    {
      HttpWebResponse response = null;
      int attempts = 0;
      while (true)
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
          Console.Out.Write("(" + attempts + ") ");
          if ((int)response.StatusCode == 503)
            Console.Out.Write("Server is unavailable.");
          else if ((int)response.StatusCode == 404)
            Console.Out.Write("Request not yet processed.");
          else if ((int)response.StatusCode == 401)
          {
            Console.Out.Write("Invalid username/password");
            return null;
          }
          else
            Console.Out.Write("Unknown error.");
          attempts++;
          System.Threading.Thread.Sleep(TIMEOUT);
          clearConsoleLine();
        }
      };
      return null;
    }

    //Constructs an XML request to be sent to the server.
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

    //Creates a revervation.
    static bool makeReservation(int slotID, string url)
    {
      Console.Out.WriteLine("Sending reservation request for slot " + slotID);
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.reserve, slotID), url);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = "";
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        responsePUT.Close();
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
          responseGET.Close();
          return true;
        }
      }
      return false;
    }

    //Cancels a reservation/
    static bool cancelReservation(int slotID, string url)
    {
      Console.Out.WriteLine("Cancelling reservation in slot " + slotID);
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.cancel, slotID), url);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = "";
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        responsePUT.Close();
        Console.Out.WriteLine("Retrieving server response.");
        HttpWebResponse responseGET = getRequestStatus(responseText);
        if (responseGET != null)
        {
          responseGET.Close();
          return true;
        }
      }
      return false;
    }

    //Gets a list of all free slots.
    static List<int> getFreeSlots(string url)
    {
      Console.Out.WriteLine("Requesting available slots.");
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.availability, 0), url);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = "";
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        responsePUT.Close();
        Console.Out.WriteLine("Retrieving server response.");
        HttpWebResponse responseGET = getRequestStatus(responseText);
        if (responseGET != null)
        {
          List<int> freeSlots = new List<int>();
          using (XmlReader reader = getReader(responseGET))
          {
            responseText = "The following slots are free:\n";
            reader.ReadToFollowing("slot_id");
            do
            {
              freeSlots.Add(reader.ReadElementContentAsInt());
            } while (reader.IsStartElement("slot_id"));
          }
          responseGET.Close();
          return freeSlots;
        }
      }
      return null;
    }

    //Gets a list of slots booked by this client.
    static List<int> getClientSlots(string url)
    {
      Console.Out.WriteLine("Requesting slots booked by this client.");
      HttpWebResponse responsePUT = sendRequest(buildRequest((int)QueryType.bookings, 0), url);
      if (responsePUT != null)
      {
        Console.Out.WriteLine("Request sent.");
        String responseText = "";
        using (XmlReader reader = getReader(responsePUT))
        {
          reader.ReadToFollowing("msg_uri");
          responseText = (reader.ReadElementContentAsString() + "?username=" + username + "&password=" + password);
        }
        responsePUT.Close();
        Console.Out.WriteLine("Retrieving server response.");
        HttpWebResponse responseGET = getRequestStatus(responseText);
        if (responseGET != null)
        {
          List<int> bookedSlots = new List<int>();
          using (XmlReader reader = getReader(responseGET))
          {
            responseText = "The following slots are booked by this client:\n";
            if (reader.ReadToFollowing("slot_id"))
              do
              {
                bookedSlots.Add(reader.ReadElementContentAsInt());
              } while (reader.IsStartElement("slot_id"));
          }
          responseGET.Close();
          return bookedSlots;
        }
      }
      return null;
    }

    //Books the lowest matching pair of slots for a hotel and band.
    static int reservePair()
    {
      //Will continue until a match is found.
      while (true)
      {
        int lowestSlot;
        Console.Out.WriteLine("Clearing existing bookings.");
        List<int> bookedBandSlots = getClientSlots(urlBand);
        List<int> bookedHotelSlots = getClientSlots(urlHotel);
        foreach (int slot in bookedBandSlots)
          cancelReservation(slot, urlBand);
        foreach (int slot in bookedHotelSlots)
          cancelReservation(slot, urlHotel);

        Console.Out.WriteLine("Reserving lowest available slots.");
        List<int> rsvdBandSlots = new List<int>();
        List<int> rsvdHotelSlots = new List<int>();
        rsvdBandSlots.Add(bookLowestSlot(urlBand, 0));
        rsvdHotelSlots.Add(bookLowestSlot(urlHotel, 0));
        Console.Out.WriteLine(String.Format("Initial slots\nBand: {0}\nHotel: {1}", rsvdBandSlots.Min(), rsvdHotelSlots.Min()));

        //Loop until a pair has been found. The highest slot in each pair sets the minimum slot number that will be booked.
        //If no slots are available for booking the whole process wil re-initialise.
        while (true)
        {
          //Check for a slot match
          if (rsvdHotelSlots.Min() == rsvdBandSlots.Min())
            return rsvdBandSlots.Min();
          //If lowest band > lowest hotel: find new lowest hotel
          else if (rsvdBandSlots.Min() > rsvdHotelSlots.Min())
          {
            //Update booked hotel slot with a value >= to that of the band.
            Console.Out.WriteLine("Replacing hotel slot " + rsvdHotelSlots.Min());
            lowestSlot = bookLowestSlot(urlHotel, rsvdBandSlots.Min());
            if (lowestSlot == -1) //Restart if no free slots are found.
              break;
            rsvdHotelSlots.Add(lowestSlot);
            cancelReservation(rsvdHotelSlots.Min(), urlHotel);
            Console.Out.WriteLine(String.Format("Cancelled hotel slot {0}, booked slot {1}", rsvdHotelSlots.Min(), rsvdHotelSlots.Max()));
            rsvdHotelSlots.Remove(rsvdHotelSlots.Min());
          }
          //If lowest hotel > lowest band: find new lowest band
          else
          {
            //Update booked band slot with a value >= to that of the hotel.
            Console.Out.WriteLine("Replacing band slot " + rsvdBandSlots.Min());
            lowestSlot = bookLowestSlot(urlBand, rsvdHotelSlots.Min());
            if (lowestSlot == -1)
              break;
            rsvdBandSlots.Add(lowestSlot);
            cancelReservation(rsvdBandSlots.Min(), urlBand);
            Console.Out.WriteLine(String.Format("Cancelled band slot {0}, booked slot {1}", rsvdBandSlots.Min(), rsvdBandSlots.Max()));
            rsvdBandSlots.Remove(rsvdBandSlots.Min());
          }
        }
        Console.Out.WriteLine("No free slots were found. Restarting.");
      }
    }

    //Returns an Xml reader for a Http response.
    static XmlReader getReader(HttpWebResponse response)
    {
      return XmlReader.Create(new StringReader(new StreamReader(response.GetResponseStream()).ReadToEnd()));
    }

    //Increments the unique request ID and writes the next value back to a file.
    static void incrementID()
    {
      requestID++;
      File.WriteAllText("../../requestID.txt", (requestID + 1).ToString());
    }

    //Books the lowest slot that is >= the set minimum value. Returns the slot ID. If no reservable slots exist: returns -1.
    static int bookLowestSlot(String url, int minimum)
    {
      List<int> avlbSlots = new List<int>();
      while (true)
      {
        avlbSlots = getFreeSlots(url);
        if (avlbSlots.Count > 0)
        {
          foreach (int slot in avlbSlots)
            if (slot >= minimum && makeReservation(slot, url))
              return slot;
        }
        else
          return -1;
      }
    }

    //Clears the output of one console line.
    static void clearConsoleLine()
    {
      Console.SetCursorPosition(0, Console.CursorTop);
      for (int i = 0; i < Console.WindowWidth; i++)
        Console.Out.Write(" ");
      Console.SetCursorPosition(0, Console.CursorTop - 1);
    }
  }
}
