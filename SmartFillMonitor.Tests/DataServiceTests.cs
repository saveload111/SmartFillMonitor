using CsvHelper;
using FreeSql;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;
using System.Data;
using System.Globalization;
using System.Text;

namespace SmartFillMonitor.Tests;

public class TestDbFixture : IDisposable
{
    public string DbPath { get; }

    public TestDbFixture()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
        DbProvider.Initialize($"Data Source={DbPath}");
    }

    public void Dispose()
    {
        DbProvider.Fsql?.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(DbPath)) File.Delete(DbPath);
    }

    public async Task SeedAsync()
    {
        // 清空表，确保测试隔离（首次可能表还不存在，忽略错误）
        try { DbProvider.Fsql.Ado.ExecuteNonQuery("DELETE FROM ProductionRecord"); } catch { }

        var records = new List<ProductionRecord>
        {
            new() { Time = new DateTime(2026, 6, 1, 10, 0, 0), BatchNo = "B001", ActualCount = 100, TargetCount = 120, SettingTemp = 25.5, ActualTemp = 25.3, CycleTime = 1.5, IsNG = false },
            new() { Time = new DateTime(2026, 6, 1, 10, 1, 0), BatchNo = "B002", ActualCount = 101, TargetCount = 120, SettingTemp = 25.5, ActualTemp = 25.6, CycleTime = 1.6, IsNG = false },
            new() { Time = new DateTime(2026, 6, 1, 10, 2, 0), BatchNo = "B003", ActualCount = 99, TargetCount = 120, SettingTemp = 25.5, ActualTemp = 25.1, CycleTime = 1.4, IsNG = true  },
            new() { Time = new DateTime(2026, 6, 2, 11, 0, 0), BatchNo = "B004", ActualCount = 102, TargetCount = 120, SettingTemp = 26.0, ActualTemp = 26.0, CycleTime = 1.5, IsNG = false },
            new() { Time = new DateTime(2026, 6, 3, 12, 0, 0), BatchNo = "B005", ActualCount = 103, TargetCount = 120, SettingTemp = 26.5, ActualTemp = 26.6, CycleTime = 1.7, IsNG = false },
        };

        foreach (var r in records)
            await DbProvider.Fsql.Insert(r).ExecuteAffrowsAsync();
    }
}

public class DataServiceTests : IClassFixture<TestDbFixture>
{
    private readonly TestDbFixture _db;

    public DataServiceTests(TestDbFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task QueryRecordAsync_范围内查询_返回正确条数()
    {
        await _db.SeedAsync();

        var list = await DataService.QueryRecordAsync(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 2, 23, 59, 59));

        Assert.Equal(4, list.Count);
    }

    [Fact]
    public async Task QueryRecordAsync_单日查询_返回当天数据()
    {
        await _db.SeedAsync();

        var list = await DataService.QueryRecordAsync(
            new DateTime(2026, 6, 3),
            new DateTime(2026, 6, 3, 23, 59, 59));

        Assert.Single(list);
        Assert.Equal("B005", list[0].BatchNo);
    }

    [Fact]
    public async Task QueryRecordAsync_无匹配_返回空列表()
    {
        await _db.SeedAsync();

        var list = await DataService.QueryRecordAsync(
            new DateTime(2024, 1, 1),
            new DateTime(2024, 1, 2));

        Assert.Empty(list);
    }

    [Fact]
    public async Task ExportToCsvAsync_流式导出_生成正确CSV()
    {
        await _db.SeedAsync();

        var csvPath = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.csv");
        try
        {
            var (total, path) = await DataService.ExportToCsvAsync(
                new DateTime(2026, 6, 1),
                new DateTime(2026, 6, 3, 23, 59, 59),
                csvPath);

            Assert.Equal(5, total);
            Assert.True(File.Exists(path));

            var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8);
            // 表头 + 5 条数据 = 6 行
            Assert.True(lines.Length >= 6);

            // 验证 BOM
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal(0xEF, bytes[0]); // UTF-8 BOM

            // 验证分隔符分号
            var header = lines[0];
            Assert.Contains(";", header);
            Assert.Contains("Time", header, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("BatchNo", header);

            // 验证数据行包含正确批次号
            var allText = string.Join("\n", lines);
            Assert.Contains("B001", allText);
            Assert.Contains("B005", allText);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ExportToCsvAsync_无数据_返回0()
    {
        await _db.SeedAsync();

        var csvPath = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.csv");
        try
        {
            var (total, _) = await DataService.ExportToCsvAsync(
                new DateTime(2024, 1, 1),
                new DateTime(2024, 1, 2),
                csvPath);

            Assert.Equal(0, total);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ExportToCsvAsync_NG标记正确写入()
    {
        await _db.SeedAsync();

        var csvPath = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.csv");
        try
        {
            var (total, _) = await DataService.ExportToCsvAsync(
                new DateTime(2026, 6, 1),
                new DateTime(2026, 6, 1, 23, 59, 59),
                csvPath);

            Assert.Equal(3, total);

            var allText = await File.ReadAllTextAsync(csvPath);
            // B003 是 NG
            Assert.Contains("True", allText);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ExportToCsvAsync_大批量分批_全部写入()
    {
        // 插入 12000 条数据到当前共享库
        const int totalRecords = 12000;
        var insertBatch = new List<ProductionRecord>(5000);
        for (int i = 0; i < totalRecords; i++)
        {
            insertBatch.Add(new ProductionRecord
            {
                Time = new DateTime(2026, 7, 1).AddMinutes(i),
                BatchNo = $"B{i:D6}",
                ActualCount = 100 + i % 50,
                TargetCount = 120,
                SettingTemp = 25.5,
                ActualTemp = 25.3 + (i % 10) * 0.1,
                CycleTime = 1.5,
                IsNG = i % 100 == 0
            });

            if (insertBatch.Count >= 5000)
            {
                await DbProvider.Fsql.Insert(insertBatch).ExecuteAffrowsAsync();
                insertBatch.Clear();
            }
        }
        if (insertBatch.Count > 0)
            await DbProvider.Fsql.Insert(insertBatch).ExecuteAffrowsAsync();

        var csvPath = Path.Combine(Path.GetTempPath(), $"export_bulk_{Guid.NewGuid():N}.csv");
        try
        {
            var (total, _) = await DataService.ExportToCsvAsync(
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 31),
                csvPath);

            Assert.Equal(totalRecords, total);
            Assert.True(File.Exists(csvPath));

            var lineCount = (await File.ReadAllLinesAsync(csvPath)).Length;
            Assert.Equal(totalRecords + 1, lineCount);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
            // 清理大批量数据，不污染后续测试
            try { DbProvider.Fsql.Ado.ExecuteNonQuery("DELETE FROM ProductionRecord"); } catch { }
        }
    }
}
