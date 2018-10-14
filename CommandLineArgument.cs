/*
 * 由SharpDevelop创建。
 * 用户： itmak
 * 日期: 2018/10/14 星期日
 * 时间: 10:42
 * 
 * 要改变这种模板请点击 工具|选项|代码编写|编辑标准头文件
 */
using System;
using System.Collections.Generic;

namespace wm_tools
{
    /// <summary>
    /// Description of CommandLineArgument.
    /// </summary>
public class CommandLineArgument
{
    List<CommandLineArgument> _arguments;

    int _index;

    string _argumentText;

    public CommandLineArgument Next
    {
        get {
            if (_index < _arguments.Count - 1) {
                return _arguments[_index + 1];
            }

            return null;
        }
    }
    public CommandLineArgument Previous
    {
        get {
            if (_index > 0)
            {
                return _arguments[_index - 1];
            }

            return null;
        }
    }
    internal CommandLineArgument(List<CommandLineArgument> args, int index, string argument)
    {
        _arguments = args;
        _index = index;
        _argumentText = argument;
    }

    public CommandLineArgument Take() {
        return Next;
    }

    public IEnumerable<CommandLineArgument> Take(int count)
    {
        var list = new List<CommandLineArgument>();
        var parent = this;
        for (int i = 0; i < count; i++)
        {
            var next = parent.Next;
            if (next == null)
                break;

            list.Add(next);

            parent = next;
        }

        return list;
    }

    public static implicit operator string(CommandLineArgument argument)
    {
        return argument._argumentText;
    }

    public override string ToString()
    {
        return _argumentText;
    }
}
}
