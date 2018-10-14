/*
 * 由SharpDevelop创建。
 * 用户： itmak
 * 日期: 2018/10/14 星期日
 * 时间: 10:43
 * 
 * 要改变这种模板请点击 工具|选项|代码编写|编辑标准头文件
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace wm_tools
{
    /// <summary>
    /// refer to https://www.cnblogs.com/linxuanchen/p/c-sharp-command-line-argument-parser.html
    /// </summary>
public class CommandLineArgumentParser
{

    List<CommandLineArgument> _arguments;
    public static CommandLineArgumentParser Parse(string[] args) {
        return new CommandLineArgumentParser(args);
    }

    public CommandLineArgumentParser(string[] args)
    {
        _arguments = new List<CommandLineArgument>();

        for (int i = 0; i < args.Length; i++)
        {
            _arguments.Add(new CommandLineArgument(_arguments,i,args[i]));
        }

    }

    public CommandLineArgument Get(string argumentName)
    {
        return _arguments.FirstOrDefault(p => p == argumentName);
    }

    public bool Has(string argumentName) {
        return _arguments.Count(p=>p==argumentName)>0;
    }
}
}
