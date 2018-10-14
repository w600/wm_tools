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

        private static SerialPort OpenW600SerialPort()
        {
            var searcher =
                new ManagementObjectSearcher(
                    "select DeviceID,MaxBaudRate from Win32_SerialPort where Description = \"GHI Boot Loader Interface\"");
            foreach (ManagementBaseObject obj in searcher.Get())
            {
                return new SerialPort((string) obj["DeviceID"], (int) (uint) obj["MaxBaudRate"]);
            }
            throw new Exception("Unable to find FEZ device. Is it in bootloader mode?");
        }

        internal string GetLoaderVersion()
        {
            SendCommand(FezCommand.GetLoaderVersion);
            string response = _w600Port.ReadLine();
            _w600Port.ReadLine(); // Eat trailing BL\r\n
            return response;
        }

        private void UpdateMsg(string value)
        {
            Console.Write(value);
        }
                
        enum DL_STATE 
        {
            DL_SYNC_START = 0,      //开始同步
            DL_SYNC_PPPPP ,         //接收到PPPPP
            DL_SYNC_CCCCC ,         //接收到CCCCC
            DL_CHANGE_BAUD ,        //开始更改波特率
            DL_BAUD_SUCCESS ,       //更改波特率成功
            DL_TRAN_START ,         //开始传输
            DL_TRAN_SUCCESS,        //传输成功	
            DL_TRAN_FAILED,	        //输出失败
        };
        
        private DL_STATE dl_state = DL_STATE.DL_SYNC_START;
        
        internal void Sync_To_Download(int baudrate)
        {
            int count_c = 0;
            bool is_synced = false;
            while(is_synced == false)
            {
                System.Threading.Thread.Sleep(20);

                if(dl_state == DL_STATE.DL_SYNC_START || dl_state == DL_STATE.DL_SYNC_CCCCC || dl_state == DL_STATE.DL_SYNC_PPPPP )
                {
                    SendCommand(FezCommand.EnterLoadMode);                    
                }

                switch(dl_state)
                {
                    case DL_STATE.DL_SYNC_START:
                        UpdateMsg("reset device ");
                        //执行重启
                        _w600Port.RtsEnable = true;
                        System.Threading.Thread.Sleep(50);
                         _w600Port.RtsEnable = false;
                        _w600Port.WriteTimeout = 200;
                        _w600Port.ReadTimeout = 200;
                        count_c = 0;
                        dl_state = DL_STATE.DL_SYNC_CCCCC;
                        break;
                    case DL_STATE.DL_SYNC_CCCCC:
                        //检查是否接收到CCCC
                         int c = 0x00;
                         try { c = _w600Port.ReadByte(); }
                         catch { c = 0x00; }
        
                         if (c == 0x43)         //CCC
                         {
                            UpdateMsg("C");    
                            if(count_c ++ >= 2)
                             {
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
                         else 
                         {
                             _w600Port.DiscardInBuffer();
                         }
                        break;
                    case DL_STATE.DL_SYNC_PPPPP:
                        UpdateMsg("wait for PPPPPPP");
                        System.Threading.Thread.Sleep(5000);
                        dl_state = DL_STATE.DL_SYNC_CCCCC;
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
//                                _w600Port.Write(new [] {(byte) 0x21, (byte) 0x0a, (byte) 0x00, (byte) 0xef, (byte) 0x2a, (byte) 0x31, (byte) 0x00, (byte) 0x00, (byte) 0x00, (byte) 0x80, (byte) 0x84, (byte) 0x1e, (byte) 0x00}, 0, 13);
//                                System.Threading.Thread.Sleep(500);
//                                _w600Port.BaudRate = 2000000;
                                break;
                        }


                        dl_state = DL_STATE.DL_SYNC_START;
                        is_synced = true;
                        break;
//                    case DL_STATE.DL_BAUD_SUCCESS:
//                        UpdateMsg("change baud success !");
//                    case DL_STATE.DL_TRAN_SUCCESS:
//                        UpdateMsg("trans file success!");
//                        break;
//                    case DL_STATE.DL_TRAN_FAILED:
//                        break;
                        
                }
                
            }
        }
        internal bool EnterLoadMode()
        {
            _w600Port.RtsEnable = true;
            System.Threading.Thread.Sleep(50);
            _w600Port.RtsEnable = false;
            int reSendCount = 5;
            while(reSendCount -- > 0)
            {
                SendCommand(FezCommand.EnterLoadMode);
                System.Threading.Thread.Sleep(5);
            }
            
            if(_w600Port.ReadLine().Length > 5)
            {
                return true;
            }
            
            return false;
        }
        
        
        private void SendCommand(FezCommand fezCommand)
        {
            _w600Port.Write(new[] {(byte) fezCommand}, 0, 1);
        }

        internal void LoadFirmware(string filename)
        {
            // Load the file into a memory block

            byte[] data = ReadFileIntoByteArray(filename);

            // Set up an XMODEM object

            var xmodem = new XModem.XModem(_w600Port);
            int bytesSent = 0;
            xmodem.PacketSent += (sender, args) =>
                                     {
                                         bytesSent += 1024;
                                         Console.Write("{0}% sent\r", Math.Min(bytesSent, data.Length)*100/data.Length);
                                     };

            // Tell the FEZ to get ready for some firmware

            //SendCommand(FezCommand.LoadFirmware);
            //_w600Port.ReadLine(); // Eat "Start File Transfer" chit chat

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
            //    var line = fezPort.ReadLine();
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
                // When the FEZ reboots itself after a file upload, the serial
                // port is in a sad state. So be polite, do your best to close
                // it, and when he freaks out with:
                //
                // IOException: A device attached to the system is not functioning.
                //
                // Say "I never did mind the little things."
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

        #region Nested type: FezCommand

        private enum FezCommand : byte
        {
            None = 0,
            EnterLoadMode = (Byte) 0x1B,
            GetLoaderVersion = (Byte) 'V',
            LoadFirmware = (Byte) 'X',
        }

        #endregion
    }
}