using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartFillMonitor.Services
{
    //参数配置的管理，保存，读取
    public static class ConfigServices
    {
        private const string SettingsFileName = "device-settings.json";
        private static readonly SemaphoreSlim tolock = new SemaphoreSlim(1, 1);//使用SemaphoreSlim来控制对配置文件的访问，确保在多线程环境下的线程安全，允许一个线程访问资源，其他线程等待
        public static string GetSettingsPath() => Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        public static async Task<bool> SaveDeviceSettingsAsync(DeviceSettings settings)
        {
            if (settings == null) { return false; }
            var path = GetSettingsPath();
            var tempFilePath = path + ".tmp";//使用临时文件来确保写入过程的原子性，避免在写入过程中读取到不完整的文件


            await tolock.WaitAsync();//确保在写入文件时没有其他线程正在读取或写入文件，进入临界区，等待获取IO锁
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });//将设置对象序列化为JSON字符串，使用WriteIndented选项使输出更易读
                await File.WriteAllTextAsync(tempFilePath, json);//将JSON字符串写入临时文件
                File.Move(tempFilePath, path, true);//将临时文件移动到目标路径，覆盖原文件，这样可以确保写入过程的原子性
                LogService.Info($"配置文件已保存成功：{path}");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error($"保存配置文件失败：{ex.Message}");
                return false;
            }
            finally
            {
                tolock.Release();
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); }
                    catch { }

                }
            }
            return false;
        }
        public static async Task<DeviceSettings> LoadDeviceSettingsAsync()
        {
            var path = GetSettingsPath();
            DeviceSettings? settings = null;
            await tolock.WaitAsync();//确保在读取文件时没有其他线程正在写入文件，进入临界区，等待获取IO锁
            try
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(path);
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };//反序列化时忽略属性名称的大小写
                        settings = JsonSerializer.Deserialize<DeviceSettings>(json, options);
                        if (settings != null)
                        {
                            LogService.Info($"配置文件已加载成功：{path}");
                            return settings;
                        }
                    }

                    catch (JsonException jsonEx)
                    {
                        LogService.Error($"配置文件格式错误，将其重置为默认值：{jsonEx.Message}");
                        BackCorruptFile(path);
                    }
                    catch (Exception ex)
                    {
                        LogService.Error($"读取配置文件失败：{ex.Message}");
                    }


                }
                else
                {
                    LogService.Warn($"配置文件不存在{path}，将创建默认配置");
                }
            }

            finally
            {
                tolock.Release();
            }
            if (settings == null)
            {
                //如果读取失败，返回默认设置
                settings = new DeviceSettings();
                //保存默认设置
                await SaveDeviceSettingsAsync(settings);
            }
            return settings;
        }
        public static void BackCorruptFile(string orginalPath)
        {
            try
            {
                var backupPath = orginalPath + ".Corrupt" + DateTime.Now.ToString("yyyyMMddHHmmss");
                if (File.Exists(orginalPath))
                {
                    try
                    {
                        File.Copy(orginalPath, backupPath, true);
                    }
                    catch { }
                }
            }
            catch { }












        }







    }
}
