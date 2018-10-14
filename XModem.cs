using System;
using System.Text;
using System.Threading;

namespace XModem
{
  ////////////////////////////////////
  // Created by Andreas Wögerbauer  //
  // http://net.trackday.cc         //
  //                                //
  // Thanks to:                     //
  // Dave Kubasiak                  //
  ////////////////////////////////////

  //Version 1.3

  //This class is tested to be 100% compatibel to Microsoft(r) Hyperterminal 5.1

  //XmodemTransmit: supports both CRC and checksum
  //XModemReceive:  only supports checksum 
  
  //Feel free to use/modify/copy/distribute this file
  public class XModem
  {
    #region Events

    public event System.EventHandler PacketReceived;
    public event System.EventHandler PacketSent;

    #endregion

    #region Members

    System.IO.Ports.SerialPort port = null;

    private byte SOH = 0x01; //header for 128byte-pakets 
    private byte STX = 0x02; //header for 1024byte-pakets 
    private byte EOT = 0x04; //end of transmission 
    private byte ACK = 0x06; //acknowledge
    private byte NAK = 0x15; //negativ acknowledge
    private byte CAN = 0x18; //cancel transfer
    private byte CTRLZ = 0x1A; //padding char to fill data blocks < buffer size
    private byte C = 0x43; //start of a CRC request

    private ushort MAXRETRANS = 25;

    //1024 for XModem 1k + 3 head chars + 2 crc + nul
    private byte[] xbuff = new byte[1030]; 

    private int bufsz, crc = -1;
    private byte packetno = 1;
    private int i, c, len = 0;
    private int retry;

    #endregion

    #region Constructor

    public XModem(System.IO.Ports.SerialPort port)
    {
      this.port = port;
    }

    #endregion

    #region Implementation

    #region XModemReceive

    ///////////////////////////
    //receive byte Array via XModem using either Checksum or CRC error detection
    /// ///////////////////////
    public byte[] XModemReceive(bool useCRC)
    {
      //since we don't know how many bytes we receive it's the
      //best solution to use a MemoryStream
      System.IO.MemoryStream buf = new System.IO.MemoryStream();
      
      int packetno = 1;
      int retry = 0;
      int i, c;
      double kb = 0;

      if (useCRC) // send and use CRC
          this.writeByte(C);
      else //send and don't use CRC          
          this.writeByte(NAK);

      while (retry < 16)
      {
        try { c = port.ReadByte(); }
        catch { c = 0x00; }

        if (c == SOH || c == STX)
        {
          #region SOH/STX

          retry = 0;
          bufsz = 128; // default buffer size
          if (c == STX)
              bufsz = 1024; // buffer size for Xmodem1K

          #region fill packet with datastream

          xbuff[0] = (byte)c;
          for (i = 0; i < (bufsz + (useCRC ? 1 : 0) + 3); i++)
          {
            xbuff[i + 1] = (byte)this.port.ReadByte();
          }

          #endregion

          if (xbuff[1] == (byte)(~xbuff[2]) && xbuff[1] == packetno && check(useCRC, xbuff, 3, bufsz))
          {
            //add buffer to memory stream
            buf.Write(xbuff, 3, bufsz);
            this.writeByte(ACK);

            #region fire event & increment packet number

            //128 or 1024 Byte per package
            double d = bufsz;
            d = d / 1024;
            kb = (kb + d);

            if (this.PacketReceived != null)
              PacketReceived(this, null);

            packetno++;
            if (packetno >= 256)
              packetno = 0;

            #endregion
          }
          else
          {
            this.writeByte(NAK);
          }

          #endregion
        }
        else if (c == EOT)
        {
          #region EOT

          port.DiscardInBuffer();
          this.writeByte(ACK);
          //convert from memory stream to byte[]
          return buf.ToArray();

          #endregion
        }
        else if (c == CAN)
        {
          #region CAN

          if (port.ReadByte() == CAN)
          {
            port.DiscardInBuffer();
            this.writeByte(ACK);
            return null; //canceled by remote
          }

          #endregion
        }
        else
        {
          retry++;
          port.DiscardInBuffer();
          this.writeByte(NAK);
        }
      }//while()

      //timeout
      this.writeByte(CAN);
      this.writeByte(CAN);
      this.writeByte(CAN);
      port.DiscardInBuffer();

      return null;
    }

    private bool check(bool isCRC, byte[] buf, int index, int sz)
    {
      if (isCRC) // use CRC checking
      {
          ushort crc = CRC16.CRC16_ccitt(buf, index, sz);
          ushort tcrc = (ushort)((buf[sz + index] << 8) + buf[sz + index + 1]);
          if (crc == tcrc)
              return true;
      }
      else { // use Checksum checking
          int i;
          byte cks = 0;
          for (i = 0; i < sz; ++i)
          {
              cks += buf[i + index];
          }
          if (cks == buf[sz + index])
              return true;
      }
      return false;
    }

    #endregion

    #region XmodemTransmit

    public int XmodemTransmit(byte[] src, int srcsz, bool use1K)
    {
      for (retry = 0; retry < 160; ++retry)
      {
        c = port.ReadByte();
        if (c >= 0)
        {
          if (c == 'C')
          {
            crc = 1;
            return this.startTrans(src, srcsz, use1K);
          }
          else if (c == NAK)
          {
            crc = 0;
            return this.startTrans(src, srcsz, use1K);
          }
          else if (c == CAN)
          {
            #region cancled by remote

            if ((c = port.ReadByte()) == CAN)
            {
              this.writeByte(ACK);
              port.DiscardInBuffer();
              return -1;
            }

            #endregion
          }
        }//if
      }//for - retry

      this.writeByte(CAN);
      this.writeByte(CAN);
      this.writeByte(CAN);
      port.DiscardInBuffer();
      return -2; //no sync
    }

    #endregion

    #region startTrans

    private int startTrans(byte[] src, int srcsz, bool use1K)
    {
      while (true)
      {
        bufsz = use1K ? 1024 : 128;

        xbuff[0] = use1K ? STX : SOH;
        xbuff[1] = packetno;
        xbuff[2] = (byte)((~packetno) & 0xFF);
        c = srcsz - len; //len = data already sent
        if (c > bufsz) c = bufsz;
        if (c > 0)
        {
          //clear buffer
          xbuff = memset(xbuff, 3, CTRLZ, bufsz);

          #region fill xbuff with data

          xbuff = memcpy(xbuff, 3, src, len, c);

          #endregion

          #region create CRC / Checksum

          if (crc == 1)
          {
            ushort ccrc = CRC16.CRC16_ccitt(xbuff, 3, bufsz);
            xbuff[bufsz + 3] = (byte)((ccrc >> 8) & 0xFF);
            xbuff[bufsz + 4] = (byte)(ccrc & 0xFF);
          }
          else
          {
            byte ccks = 0;
            for (i = 3; i < bufsz + 3; ++i)
            {
              ccks += xbuff[i];
            }
            xbuff[bufsz + 3] = ccks;
          }

          #endregion

          retry = 0;
          bool success = false;

          while (retry < MAXRETRANS && !success)
          {
            retry++;

            #region write xbuff out on port

            writeBuffer(xbuff, bufsz + 4 + crc);

            #endregion

            c = port.ReadByte();

            if (c >= 0)
            {
              if (c == ACK)
              {

                if (this.PacketSent != null)
                  PacketSent(this, null);
                
                packetno = (byte)((packetno + 1) & 0xFF);
                len += bufsz;
                success = true; //go ahead in transmition
              }
              if (c == CAN)
              {
                if ((c = port.ReadByte()) == CAN)
                {
                  this.writeByte(ACK);
                  port.DiscardInBuffer();
                  return -1; //canceled by remote
                }
              }
              if (c == NAK)
              {
                //do nothing
              }
            }
          }//while

          if (!success)
          {
            #region xmit error

            this.writeByte(CAN);
            this.writeByte(CAN);
            this.writeByte(CAN);
            port.DiscardInBuffer();
            return -4;

            #endregion
          }
        }
        else
        {
          #region transfer completed

          for (retry = 0; retry < 10; ++retry)
          {
            this.writeByte(EOT);
            
            //avoid problem with to short timeout on port
            Thread.Sleep(500);
            
            c = port.ReadByte();
            if (c == ACK)
              break;
          }
          //port.DiscardInBuffer();
          return (c == ACK) ? len : -5;

          #endregion
        }
      }//while
    }

    #endregion

    #endregion

    #region a few really cool C-style functions ;)

    private void writeBuffer(byte[] xbuff, int size)
    {
      this.port.Write(xbuff, 0, size);
    }

    //writes one single byte
    private void writeByte(byte b)
    {
      byte[] buffer = new byte[1];
      buffer[0] = b;
      this.port.Write(buffer, 0, 1);
    }

    //sets the first num bytes pointed by buffer to the value specified by c parameter.
    byte[] memset(byte[] xbuff, int index, byte c, int num)
    {
      for (int i = 0; i < num; i++)
      {
        xbuff[i + index] = c;
      }

      return xbuff;
    }

    //copies num bytes from src buffer to memory location pointed by dest.
    byte[] memcpy(byte[] dest, int d_i, byte[] src, int d_s, int num)
    {
      for (int i = 0; i < num; i++)
      {
        dest[i + d_i] = src[i + d_s];
      }

      return dest;
    }

    #endregion
  }
}
