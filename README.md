`注意：本项目旨在取代 w600_download_tool.exe 和 makeimg.exe 以及 makeimg_all.exe 等工具，但目前尚未完善，仅供学习交流和测试使用~`

------------

wm_tools 是一个 w600 的 ROM 工具，可以实现 w600 的固件烧录，固件擦除，固件处理，ROM信息读取等功能，目前仅支持 windows 平台。

运行和编译需依赖 .Net 4.0 平台https://www.microsoft.com/en-us/download/details.aspx?id=17718

# 编译说明

使用 SharpDevelop 4.4 编译，也可使用 Visual Studio 2010 以上版本编译，

# 使用说明

* 参数
	
​	 wmtools.exe  \[-h]  \[-p]  \[-b] { write_flash, erase_flash, verison}

​	可以执行 wm_tools.exe -h 查看详细的参数说明

* 端口

  一般可从设备管理器中查看当前设备的端口号，如 COM5

* 波特率

  可选范围： 115200, 460800, 921600, 1000000, 2000000

  `注意：部分串口芯片并不能支持 2Mbps或1Mbps，我们推荐使用CH340系列芯片，CP210x 芯片仅能支持921600`


* 示例：

    wm_tools.exe  -h

    wm_tools.exe  -p COM6 -b 2000000 erase_flash

    wm_tools.exe  -p COM6 -b 2000000 write_flash wm600_sec.img

## 固件烧录
  `下载需进入下载模式或者secboot模式`

  支持 xxxx_sec.img xxxx_gz.img xxxx.fls 等文件格式

  示例：

​    wm_tools.exe  -p COM6 -b 2000000 write_flash wm600_sec.img

## 固件擦除

 示例：

​    wm_tools.exe  -p COM6 erase_flash

## 固件处理

`暂未支持，即将开放`

# 其它

有任何疑问或问题反馈，可联系 support@thingsturn.com