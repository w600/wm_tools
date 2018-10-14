using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace wm_tools
{
    internal class Program
    {
        
        enum ACTION_ARGS 
        {
            NULL = 0,       //null，nothing to do
            DOWNLOAD ,      //download firmware
            ERASE ,         //erase the flash
            CHIP_ID ,       //read the chip id
            FLASH_ID ,      //read the flash id
            VERSION ,       //read the wm_tools version
        };
        
        static string Version = "v0.3";
        static ACTION_ARGS action_arg = ACTION_ARGS.NULL;
        
        private static int Main(string[] args)
        {
            var arguments = CommandLineArgumentParser.Parse(args);
        
            string filepath = "wm600_sec.bin";
            string portname = "COM1";
            string baudrate = "115200";
            
            if (arguments.Has("-h"))
            {
                Console.WriteLine("usage: \r\n" +
                                  "\twm_tools.exe [-h] [-p] [-b] {write_flash, erase_flash, version}\r\n" +
                                  "examples: \r\n" +
                                  "\twm_tools.exe  -p COM6 -b 2000000 erase_flash\r\n" +
                                  "\twm_tools.exe  -p COM6 -b 2000000 write_flash wm600_sec.img");
                return 0;
            }
            
            if (arguments.Has("-p"))
            {
                try 
                {
                    portname = arguments.Get("-p").Next;                    
                }
                catch (Exception) 
                {
                    Console.WriteLine("get port name error !");
                }
            }
            
            if (arguments.Has("-b"))
            {
                try 
                {
                    baudrate = arguments.Get("-b").Next;                  
                }
                catch (Exception) 
                {
                    Console.WriteLine("get baudrate error !");
                }
            }
            
            if (arguments.Has("write_flash"))
            {
                action_arg = ACTION_ARGS.DOWNLOAD;
                try 
                {
                    filepath  = arguments.Get("write_flash").Next;                
                }
                catch (Exception) 
                {
                    Console.WriteLine("get firmware path error !");
                }
            }
        
            if (arguments.Has("erase_flash"))
            {
                action_arg = ACTION_ARGS.ERASE;
            }
            
            if (arguments.Has("chip_id"))
            {
                action_arg = ACTION_ARGS.CHIP_ID;
            }
            
            if (arguments.Has("flash_id"))
            {
                action_arg = ACTION_ARGS.FLASH_ID;
            }
            
            if (arguments.Has("version"))
            {
                action_arg = ACTION_ARGS.VERSION;
            }
            
            switch(action_arg)
            {
                case ACTION_ARGS.NULL:
                    Console.WriteLine("error: invalid argument");
                    Console.WriteLine("usage: \r\n" +
                                  "\twm_tools.exe [-h] [-p] [-b] {write_flash, erase_flash, version}\r\n" +
                                  "examples: \r\n" +
                                  "\twm_tools.exe  -p COM6 -b 2000000 erase_flash\r\n" +
                                  "\twm_tools.exe  -p COM6 -b 2000000 write_flash wm600_sec.img");
                    break;
                case ACTION_ARGS.DOWNLOAD:
                    Console.WriteLine("need to download ...");
                    int baudrate_index = 4;
                    switch(baudrate)
                    {
                        case "2000000": baudrate_index = 0; break;
                        case "1000000": baudrate_index = 1; break;
                        case "921600": baudrate_index = 2; break;
                        case "460800": baudrate_index = 3; break;
                        default: baudrate_index = 4; break;
                    }
                    if(File.Exists(filepath) == false)
                    {
                        Console.WriteLine("Error firmware path !");
                        return -1;
                    }
                    var controller_write = new W600Controller();
                    try
                    {
                        controller_write.Open(portname);
                        Console.WriteLine("opend {0} !", portname);
                        controller_write.Sync_To_Download(baudrate_index);
                        controller_write.LoadFirmware(filepath);
                        Console.WriteLine("All done.");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return -1;
                    }
                    break;
                case ACTION_ARGS.ERASE:
                    Console.WriteLine("need to erase ...");
                    var controller_erase = new W600Controller();
                    try
                    {
                        controller_erase.Open(portname);
                        Console.WriteLine("opend {0} !", portname);
                        controller_erase.Sync_To_Erase();
                        Console.WriteLine("All done.");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return -1;
                    }
                    break;
                case ACTION_ARGS.CHIP_ID:
                    break;
                case ACTION_ARGS.FLASH_ID:
                    break;
                case ACTION_ARGS.VERSION:
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    Console.WriteLine("wm_tools {0} for w600", Version);
                    Console.WriteLine("written by thingsturn");
                    Console.WriteLine("compile @ {0}", System.IO.File.GetLastWriteTime(assembly.Location).ToString());
                    break;
            }
            return 0;
        }
    }
}