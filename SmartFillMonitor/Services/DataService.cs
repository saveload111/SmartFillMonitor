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
                .Where($"Time >= {start} and Time <= {end}")
                .ToListAsync();

        }

        public static async Task ExportToCsvAsync(List<ProductionRecord> records, string filePath)
        {
            if (records.Count == 0) return;
            await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true)); // true = 带 BOM
            await using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(records);

        }

    }
}
