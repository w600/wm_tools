using System;
using System.IO;
using System.IO.Ports;
using System.Management;

namespace wm_tools
{
    internal class W600Controller
    {
        private SerialPort _w600Port;

        internal void Open(string portname)
        {
            _w600Port = new SerialPort();
            _w600Port.PortName = portname;
            _w600Port.BaudRate = 115200;
            _w600Port.DataBits = 8;
            //_w600Port.RtsEnable = false;
            //_w600Port.DtrEnable = false;
            _w600Port.RtsEnable = false;
            _w600Port.StopBits = (StopBits)Enum.Parse(typeof(StopBits), "1");
            _w600Port.Parity = (Parity)Enum.Parse(typeof(Parity), "None");
            _w600Port.Handshake = (Handshake)Enum.Parse(typeof(Handshake), "None");
            _w600Port.WriteTimeout = 1000; /*Write time out*/
            _w600Port.ReadTimeout = 1000;

            _w600Port.Open();
            _w600Port.NewLine = "\r\n";
        }

        // http://www.codeproject.com/Messages/3692563/Get-correct-driver-name-from-Device-management-ass.aspx
        // http://geekswithblogs.net/PsychoCoder/archive/2008/01/25/using_wmi_in_csharp.aspx

        private void UpdateMsg(string value)
        {
            Console.Write(value);
        }
                
        enum DL_STATE 
        {
            DL_SYNC_START = 0,      //开始同步
            DL_SYNC_CCCCC ,         //接收到CCCCC
            DL_CHANGE_BAUD ,        //开始更改波特率
            DL_BAUD_SUCCESS ,       //更改波特率成功
            DL_TRAN_START ,         //开始传输
            DL_TRAN_SUCCESS,        //传输成功	
            DL_TRAN_FAILED,	        //输出失败
            DL_ERASE_SECBOOT,	    //擦除secboot
            DL_ERASE_START,	        //擦除Flash
            DL_ERASE_WAIT,	        //等待擦除
            DL_ERASE_SUCCESS,	    //擦除成功
        };
        
        private DL_STATE dl_state = DL_STATE.DL_SYNC_START;
        
        internal void Sync_To_Download(int baudrate)
        {
            int count_c = 0;
//            int count_p = 0;
            _w600Port.DiscardInBuffer();
            _w600Port.WriteTimeout = 20;
            _w600Port.ReadTimeout = 20;
			UpdateMsg("\r\nstart connect device");
            DL_STATE dl_state = DL_STATE.DL_SYNC_START;
            bool is_synced = false;
            int timeout = 50;
            while(is_synced == false)
            {
                if(dl_state == DL_STATE.DL_SYNC_START || dl_state == DL_STATE.DL_SYNC_CCCCC)
                {
                    _w600Port.Write(new[] {(byte) 0x1B}, 0, 1);                  
                }

                switch(dl_state)
                {
                    case DL_STATE.DL_SYNC_START:
                        byte ccc = 0x00;
                         try { ccc = (byte) _w600Port.ReadByte(); }
                         catch { ccc = 0x00; }
        
                         if (ccc == 0x43)         //CCC
                         {
                         	timeout = 50;	//续命
                            UpdateMsg("C");    
                            if(count_c ++ >= 2)
                             {
                                UpdateMsg("\r\nsync success, ");
                                count_c = 0;
                                dl_state = DL_STATE.DL_CHANGE_BAUD; 
                                break;
                             }
                         }
                         else if(ccc == 0x50)     //PPP
                         {
                         	count_c = 0;
                         	timeout = 50;	//续命
                         	_w600Port.DiscardInBuffer();
                             UpdateMsg("P");
                             break;
                         }
                         else
                         {
                         	if(ccc != 0x00)
                         	{
                         		count_c = 0;
                         	}
                         	
                         	_w600Port.DiscardInBuffer();
                         	if(timeout -- <= 0)
                         	{
                         		timeout = 50;		//续命
                         		UpdateMsg("\r\ntimeout, try to reset device");
		                        _w600Port.RtsEnable = true;
		                        System.Threading.Thread.Sleep(20);
	                         	_w600Port.RtsEnable = false;
	             	            _w600Port.ReadTimeout = 20;
		                        dl_state = DL_STATE.DL_SYNC_CCCCC;
		                        break;
                         	}
                         }
                         dl_state = DL_STATE.DL_SYNC_START; 
                        break;
                    case DL_STATE.DL_SYNC_CCCCC:
                        //检查是否接收到CCCC
                         byte c = 0x00;
                         try { c = (byte) _w600Port.ReadByte(); }
                         catch { c = 0x00; }
        
                         if (c == 0x43)         //CCC
                         {
                            UpdateMsg("C");    
                            if(count_c ++ >= 2)
                             {
                                UpdateMsg("\r\nsync success, ");
                                count_c = 0;
                                dl_state = DL_STATE.DL_CHANGE_BAUD; 
                                break;
                             }
                         }
                         else if(c == 0x50)     //PPP
                         {
                     		count_c = 0;
                         	UpdateMsg("P");
                         }
                         else if(c != 0x00)
                     	{
                     		count_c = 0;
                     	}
                        break;
                    case DL_STATE.DL_CHANGE_BAUD:

                        //21 0a 00 ef 2a 31 00 00 00 80 84 1e 00 
                        //2M设置指令：       21 0a 00 ef 2a 31 00 00 00 80 84 1e 00 
                        //1M设置指令：       21 0a 00 5e 3d 31 00 00 00 40 42 0f 00 
                        //921600设置指令：   21 0a 00 5d 50 31 00 00 00 00 10 0e 00 
                        //460800设置指令：   21 0a 00 07 00 31 00 00 00 00 08 07 00 
                        //115200设置指令：   21 0a 00 97 4b 31 00 00 00 00 c2 01 00 
                        switch(baudrate)
                        {
                            case 0: //2000000
                                UpdateMsg("\r\nchange baud to 2000000!\r\n");
                                _w600Port.Write(new [] {(byte) 0x21, (byte) 0x0a, (byte) 0x00, (byte) 0xef, (byte) 0x2a, (byte) 0x31, (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x80, (byte) 0x84, (byte) 0x1e, (byte) 0x00}, 0, 13);
                                System.Threading.Thread.Sleep(500);
                                _w600Port.BaudRate = 2000000;
                                break;
                            case 1: //1000000
                                UpdateMsg("\r\nchange baud to 1000000!\r\n");
                                _w600Port.Write(new [] {(byte) 0x21, (byte) 0x0a, (byte) 0x00, (byte) 0x5e, (byte) 0x3d, (byte) 0x31, (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x40, (byte) 0x42, (byte) 0x0f, (byte) 0x00}, 0, 13);
                                System.Threading.Thread.Sleep(500);
                                _w600Port.BaudRate = 1000000;
                                break;
                            case 2: //921600
                                UpdateMsg("\r\nchange baud to 921600!\r\n");
                                _w600Port.Write(new [] {(byte) 0x21, (byte) 0x0a, (byte) 0x00, (byte) 0x5d, (byte) 0x50, (byte) 0x31, (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x10, (byte) 0x0e, (byte) 0x00}, 0, 13);
                                System.Threading.Thread.Sleep(500);
                                _w600Port.BaudRate = 921600;
                                break;
                            case 3: //460800
                                UpdateMsg("\r\nchange baud to 460800!\r\n");
                                _w600Port.Write(new [] {(byte) 0x21, (byte) 0x0a, (byte) 0x00, (byte) 0x07, (byte) 0x00, (byte) 0x31, (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x08, (byte) 0x07, (byte) 0x00}, 0, 13);
                                System.Threading.Thread.Sleep(500);
                                _w600Port.BaudRate = 460800;
                                break;
                            default:    //115200
                                UpdateMsg("\r\nuse default baud 115200!\r\n");
                                break;
                        }
                        dl_state = DL_STATE.DL_SYNC_START;
                        is_synced = true;
                        break;
                        
                }
                
            }
        }
        
        internal void Sync_To_Erase()
        {
            int count_c = 0;
            int count_p = 0;
            UpdateMsg("start sync !!!! \r\n");
            bool is_synced = false;
            dl_state = DL_STATE.DL_SYNC_START;
            while(is_synced == false)
            {
                System.Threading.Thread.Sleep(20);

                if(dl_state == DL_STATE.DL_SYNC_START || dl_state == DL_STATE.DL_SYNC_CCCCC)
                {
//                    UpdateMsg("send 0x1b\r\n");
                    _w600Port.Write(new[] {(byte) 0x1B}, 0, 1); 
                }

                switch(dl_state)
                {
                    case DL_STATE.DL_SYNC_START:
                        UpdateMsg("reset device ");
                        //执行重启
                        _w600Port.DiscardInBuffer();
                        _w600Port.RtsEnable = true;
                        System.Threading.Thread.Sleep(50);
                         _w600Port.RtsEnable = false;
                        _w600Port.WriteTimeout = 50;
                        _w600Port.ReadTimeout = 50;
                        
                        dl_state = DL_STATE.DL_SYNC_CCCCC;
                        break;
                    case DL_STATE.DL_SYNC_CCCCC:
                        //检查是否接收到CCCC
//                        UpdateMsg("check reciver\r\n");
                         int c = 0x00;
                         try { c = _w600Port.ReadByte(); }
                         catch { c = 0x00; }
        
                         if (c == 0x43)         //CCC
                         {
                            UpdateMsg("C");    
                            if(count_c ++ >= 2)
                             {
                                count_c = 0;
                                dl_state = DL_STATE.DL_ERASE_SECBOOT; 
                                break;
                             }
                         }
                         else if(c == 0x50)     //PPP
                         {
                             UpdateMsg("P");
                             if(count_p++ > 2)
                             {
                                 count_p = 0;
                                 dl_state = DL_STATE.DL_ERASE_START;
                                 break;
                             }
                         }
                         else 
                         {
//                             _w600Port.DiscardInBuffer();
                         }
                        break;
                    case DL_STATE.DL_ERASE_SECBOOT:
                        //21 06 00 c7 7c 3f 00 00 00
                        UpdateMsg("\r\nerase secboot!\r\n");
                        _w600Port.Write(new [] {(byte) 0x21, (byte) 0x06, (byte) 0x00, (byte) 0xc7, (byte) 0x7c, (byte) 0x3f, (byte) 0x00, (byte) 0x00, (byte) 0x00}, 0, 9);

                        dl_state = DL_STATE.DL_SYNC_START;
                        break;
                    case DL_STATE.DL_ERASE_START:

                        //21 06 00 41 45 32 00 00 00
                        UpdateMsg("\r\nstart erase flash, please wait a moment!\r\n");
                        _w600Port.Write(new [] {(byte) 0x21, (byte) 0x06, (byte) 0x00, (byte) 0x41, (byte) 0x45, (byte) 0x32, (byte) 0x00, (byte) 0x00, (byte) 0x00}, 0, 9);

                        dl_state = DL_STATE.DL_ERASE_WAIT;
                        break;
                    case DL_STATE.DL_ERASE_WAIT:
                        c = 0x00;
                         try { c = _w600Port.ReadByte(); }
                         catch { c = 0x00; }
        
                         if (c == 0x43)         //CCC
                         {
                            UpdateMsg("C");    
                            if(count_c ++ >= 2)
                             {
                                count_c = 0;
                                dl_state = DL_STATE.DL_ERASE_SUCCESS; 
                                break;
                             }
                         }
                         else 
                         {
//                             _w600Port.DiscardInBuffer();
                         }
                        break;
                    case DL_STATE.DL_ERASE_SUCCESS:
                        UpdateMsg("\r\nerase flash finished!\r\n");
                        Close();
                        is_synced = true;
                        break;
                }
                
            }
        }
        
        internal void LoadFirmware(string filename)
        {
            // Load the file into a memory block
            _w600Port.WriteTimeout = 1000; /*Write time out*/
            _w600Port.ReadTimeout = 1000;
            byte[] data = ReadFileIntoByteArray(filename);

            // Set up an XMODEM object
            var xmodem = new XModem.XModem(_w600Port);
            int bytesSent = 0;
            xmodem.PacketSent += (sender, args) =>
                                     {
                                         bytesSent += 1024;
                                         Console.Write("{0}% sent\r", Math.Min(bytesSent, data.Length)*100/data.Length);
                                     };

            // Transfer the block
            int result = xmodem.XmodemTransmit(data, data.Length, true);

            // Throw an exception if anything freaked
            if (result < data.Length)
            {
                throw new Exception("Failed to transmit file " + result);
            }

            Console.WriteLine();

            // There is a bunch more yak on the serial port, and the device
            // has restarted in normal operation. You know, just FYI.
            //while (true)
            //{
            //    var line = _w600Port.ReadLine();
            //    System.Console.WriteLine(line);
            //}

            Close();
        }

        private void Close()
        {
            try
            {
                if(_w600Port != null)
                    _w600Port.Close();
            }
            catch (IOException)
            {
                // Nothing to see here. Move along.
            }
            _w600Port = null;
        }

        private static byte[] ReadFileIntoByteArray(string filename)
        {
            MemoryStream outputStream;

            using (var inputStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                outputStream = new MemoryStream((int) inputStream.Length);
                inputStream.CopyTo(outputStream);
            }

            return outputStream.ToArray();
        }
    }
}