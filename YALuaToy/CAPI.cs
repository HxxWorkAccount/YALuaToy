/* Todo - 该功能未实现 */
namespace YALuaToy {

using System;
using System.Runtime.InteropServices;

public static class CAPI
{
    /* 临时测试非托管调用 */
    [UnmanagedCallersOnly(EntryPoint = "YALuaToy_Wtf")]
    public static void Wtf() {
        Console.WriteLine("Hello, World!");
    }
}

}
