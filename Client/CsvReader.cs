using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;


namespace Client
{
    public class CsvReader : IDisposable
    {
        private StreamReader reader;
        private StreamWriter rejectWriter;
        private Dictionary<string, int> columnIndices;
        private bool disposed = false;
        private const double SENTINEL = 32767.0;

        public CsvReader(string csvPath, string rejectPath = null)
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"CSV file not found: {csvPath}");

            reader = new StreamReader(csvPath);

            rejectPath = rejectPath ?? Path.Combine(
                Path.GetDirectoryName(csvPath) ?? "",
                "rejected_client1.csv"
            );

            rejectWriter = new StreamWriter(rejectPath, false);
            rejectWriter.WriteLine("RowIndex,Reason,RawLine");

            ParseHeader();
        }

        private void ParseHeader()
        {
            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
                throw new InvalidOperationException("CSV is empty");

            var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();
            columnIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Length; i++)
                columnIndices[headers[i]] = i;

            var required = new[] { "DAY", "HOUR", "ACPWRT", "DCVOLT", "TEMPER",
                                   "VL1TO2", "VL2TO3", "VL3TO1", "ACCUR1", "ACVLT1" };
            var missing = required.Where(c => !columnIndices.ContainsKey(c)).ToList();

            if (missing.Any())
                throw new InvalidOperationException($"Missing columns: {string.Join(", ", missing)}");
        }

        public IEnumerable<PvSample> ReadSamples(int maxRows)
        {
            int rowIndex = 0;
            string line;

            while ((line = reader.ReadLine()) != null && rowIndex < maxRows)
            {
                rowIndex++;
                PvSample sample = ParseLine(line, rowIndex);

                if (sample == null)
                {
                    continue;
                }

                yield return sample;
            }
        }

        private PvSample ParseLine(string line, int rowIndex)
        {
            var fields = line.Split(',');

            var day = GetField(fields, "DAY");
            var hour = GetField(fields, "HOUR");

            if (string.IsNullOrWhiteSpace(day) || string.IsNullOrWhiteSpace(hour))
            {
                LogReject(rowIndex, "Missing DAY or HOUR", line);
                return null;
            }

            var sample = new PvSample
            {
                RowIndex = rowIndex,
                Day = day,
                Hour = hour,
                AcPwrt = ParseDouble(fields, "ACPWRT"),
                DcVolt = ParseDouble(fields, "DCVOLT"),
                Temper = ParseDouble(fields, "TEMPER"),
                Vl1to2 = ParseDouble(fields, "VL1TO2"),
                Vl2to3 = ParseDouble(fields, "VL2TO3"),
                Vl3to1 = ParseDouble(fields, "VL3TO1"),
                AcCur1 = ParseDouble(fields, "ACCUR1"),
                AcVlt1 = ParseDouble(fields, "ACVLT1")
            };

            return sample;
        }

        private string GetField(string[] fields, string columnName)
        {
            if (!columnIndices.TryGetValue(columnName, out int index))
                return null;
            if (index >= fields.Length)
                return null;
            return fields[index].Trim();
        }

        private double? ParseDouble(string[] fields, string columnName)
        {
            var str = GetField(fields, columnName);
            if (string.IsNullOrWhiteSpace(str))
                return null;

            if (!double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return null;

            if (Math.Abs(val - SENTINEL) < 0.001)
                return null;

            return val;
        }

        private void LogReject(int rowIndex, string reason, string rawLine)
        {
            var escapedLine = rawLine.Replace("\"", "\"\"");
            rejectWriter.WriteLine($"{rowIndex},\"{reason}\",\"{escapedLine}\"");
            rejectWriter.Flush();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                reader?.Dispose();
                rejectWriter?.Dispose();
                disposed = true;
            }
        }
    }
}
