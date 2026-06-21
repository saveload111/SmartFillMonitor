using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartFillMonitor.Models;
using CsvHelper;
using System.IO;
namespace SmartFillMonitor.Services
{
    public class DataService
    {
        public static async Task SaveProductionRecordAsync(ProductionRecord record)
        {
            await DbProvider.Fsql.Insert(record).ExecuteAffrowsAsync();



        }



        public static async Task<List<ProductionRecord>> QueryRecordAsync(DateTime start, DateTime end)
        {
            return await DbProvider.Fsql.Select<ProductionRecord>()
                .Where(x => x.Time >= start && x.Time <= end)
                .ToListAsync();

        }

        /// <summary>
        /// 分批流式导出到 CSV，不一次性加载全部数据到内存
        /// </summary>
        public static async Task<(int total, string filePath)> ExportToCsvAsync(DateTime start, DateTime end, string filePath)
        {
            var total = (int)await DbProvider.Fsql.Select<ProductionRecord>()
                .Where(x => x.Time >= start && x.Time <= end)
                .CountAsync();

            if (total == 0) return (0, filePath);

            const int batchSize = 5000;
            int page = 0;

            await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
            };
            await using var csv = new CsvWriter(writer, config);
            csv.WriteHeader<ProductionRecord>();
            await csv.NextRecordAsync();

            while (true)
            {
                var batch = await DbProvider.Fsql.Select<ProductionRecord>()
                    .Where(x => x.Time >= start && x.Time <= end)
                    .OrderByDescending(r => r.Time)
                    .Skip(page * batchSize)
                    .Take(batchSize)
                    .ToListAsync();

                if (batch.Count == 0) break;

                await csv.WriteRecordsAsync(batch);
                await csv.FlushAsync();

                page++;
                if (batch.Count < batchSize) break;
            }

            return (total, filePath);
        }

    }
}
