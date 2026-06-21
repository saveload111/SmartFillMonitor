using SmartFillMonitor.Services;
using System.Reflection;

namespace SmartFillMonitor.Tests;

public class PlcServiceTests
{
    // 反射调用私有方法 — 测试专用
    private static string ConvertRegisters(ushort[] regs)
    {
        var method = typeof(PlcService).GetMethod("ConverRegisterToString",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { regs })!;
    }

    [Fact]
    public void ConverRegisterToString_AB字符串_返回AB()
    {
        ushort[] regs = { 0x0041, 0x0042, 0x0000 };
        Assert.Equal("AB", ConvertRegisters(regs));
    }

    [Fact]
    public void ConverRegisterToString_空数组_返回空字符串()
    {
        Assert.Equal("", ConvertRegisters(Array.Empty<ushort>()));
    }

    [Fact]
    public void ConverRegisterToString_全零_返回空()
    {
        Assert.Equal("", ConvertRegisters(new ushort[] { 0x0000 }));
    }

    [Fact]
    public void ConverRegisterToString_数字123_返回123()
    {
        ushort[] regs = { 0x0031, 0x0032, 0x0033, 0x0000 };
        Assert.Equal("123", ConvertRegisters(regs));
    }
}

public class UserServiceTests
{
    private static string Hash(string password)
    {
        var method = typeof(UserService).GetMethod("HashPassword",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { password })!;
    }

    [Fact]
    public void HashPassword_相同输入_相同输出()
    {
        Assert.Equal(Hash("admin"), Hash("admin"));
    }

    [Fact]
    public void HashPassword_SHA256输出64位十六进制()
    {
        Assert.Equal(64, Hash("admin").Length);
    }

    [Fact]
    public void HashPassword_不同输入_不同输出()
    {
        Assert.NotEqual(Hash("admin"), Hash("engineer"));
    }

    [Fact]
    public void HashPassword_空字符串_返回空()
    {
        Assert.Equal("", Hash(""));
    }
}
