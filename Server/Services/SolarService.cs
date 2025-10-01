using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using Common;

namespace Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class SolarService : ISolarService, IDisposable
    {
        private string sessionId;
        private PvMeta currentMeta;
        private StreamWriter sessionWriter;
        private StreamWriter rejectWriter;
        private int receivedCount;
        private DateTime sessionStart;
        private bool disposed = false;

        // Pragovi iz konfiguracije
        private double overTempThreshold;
        private double voltageImbalancePct;
        private int powerFlatlineWindow;
        private double powerSpikeThreshold;
        private double dcSagThreshold;
        private double lowEfficiencyThreshold;

        // Za praćenje analitike
        private double? lastDcVolt;
        private Queue<double> recentPower;
        private List<string> warnings;

        // Događaji
        public event EventHandler OnTransferStarted;
        public event EventHandler<int> OnSampleReceived;
        public event EventHandler OnTransferCompleted;
        public event EventHandler<WarningEventArgs> OnWarningRaised;

        public SolarService()
        {
            LoadConfig();
            recentPower = new Queue<double>();
            warnings = new List<string>();
        }

        private void LoadConfig()
        {
            overTempThreshold = double.Parse(ConfigurationManager.AppSettings["OverTempThreshold"] ?? "50.0");
            voltageImbalancePct = double.Parse(ConfigurationManager.AppSettings["VoltageImbalancePct"] ?? "5.0");
            powerFlatlineWindow = int.Parse(ConfigurationManager.AppSettings["PowerFlatlineWindow"] ?? "10");
            powerSpikeThreshold = double.Parse(ConfigurationManager.AppSettings["PowerSpikeThreshold"] ?? "1000.0");
            dcSagThreshold = double.Parse(ConfigurationManager.AppSettings["DcSagThreshold"] ?? "50.0");
            lowEfficiencyThreshold = double.Parse(ConfigurationManager.AppSettings["LowEfficiencyThreshold"] ?? "0.8");
        }

        public ServerAck StartSession(PvMeta meta)
        {
            try
            {
                if (meta == null)
                    return new ServerAck { Success = false, Message = "Meta is null" };

                sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
                currentMeta = meta;
                sessionStart = DateTime.Now;
                receivedCount = 0;
                lastDcVolt = null;
                recentPower.Clear();
                warnings.Clear();

                // Kreiranje foldera
                var basePath = ConfigurationManager.AppSettings["DataPath"] ?? "Data";
                var plantPath = Path.Combine(basePath, meta.PlantId);
                var dateFolder = Path.Combine(plantPath, sessionStart.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(dateFolder);

                var sessionFile = Path.Combine(dateFolder, "session.csv");
                var rejectFile = Path.Combine(dateFolder, "rejects.csv");

                sessionWriter = new StreamWriter(sessionFile, append: true);
                rejectWriter = new StreamWriter(rejectFile, append: true);

                // Header
                if (new FileInfo(sessionFile).Length == 0)
                {
                    sessionWriter.WriteLine("RowIndex,Day,Hour,AcPwrt,DcVolt,Temper,Vl1to2,Vl2to3,Vl3to1,AcCur1,AcVlt1");
                }
                if (new FileInfo(rejectFile).Length == 0)
                {
                    rejectWriter.WriteLine("RowIndex,Reason,RawData");
                }

                OnTransferStarted?.Invoke(this, EventArgs.Empty);

                return new ServerAck
                {
                    Success = true,
                    Message = $"Session {sessionId} started",
                    ReceivedCount = 0,
                    PercentOfLimit = 0
                };
            }
            catch (Exception ex)
            {
                return new ServerAck { Success = false, Message = ex.Message };
            }
        }

        public ServerAck PushSample(PvSample sample)
        {
            try
            {
                if (sessionId == null)
                    return new ServerAck { Success = false, Message = "No active session" };

                if (sample == null)
                    return new ServerAck { Success = false, Message = "Sample is null" };

                // Validacija
                var validationError = ValidateSample(sample);
                if (!string.IsNullOrEmpty(validationError))
                {
                    LogReject(sample.RowIndex, validationError, SampleToString(sample));
                    return new ServerAck
                    {
                        Success = false,
                        Message = validationError,
                        ReceivedCount = receivedCount,
                        PercentOfLimit = CalcPercent()
                    };
                }

                // Snimanje
                WriteSample(sample);
                receivedCount++;

                // Analitika
                RunAnalytics(sample);

                OnSampleReceived?.Invoke(this, receivedCount);

                return new ServerAck
                {
                    Success = true,
                    Message = "OK",
                    ReceivedCount = receivedCount,
                    PercentOfLimit = CalcPercent()
                };
            }
            catch (Exception ex)
            {
                return new ServerAck { Success = false, Message = ex.Message };
            }
        }

        public ServerAck EndSession()
        {
            try
            {
                if (sessionId == null)
                    return new ServerAck { Success = false, Message = "No active session" };

                sessionWriter?.Flush();
                sessionWriter?.Close();
                rejectWriter?.Flush();
                rejectWriter?.Close();

                var duration = DateTime.Now - sessionStart;

                OnTransferCompleted?.Invoke(this, EventArgs.Empty);

                var msg = $"Session {sessionId} ended. Received {receivedCount} samples in {duration.TotalSeconds:F1}s";
                sessionId = null;

                return new ServerAck
                {
                    Success = true,
                    Message = msg,
                    ReceivedCount = receivedCount,
                    PercentOfLimit = CalcPercent()
                };
            }
            catch (Exception ex)
            {
                return new ServerAck { Success = false, Message = ex.Message };
            }
        }

        public List<string> GetWarnings()
        {
            return new List<string>(warnings);
        }

        private string ValidateSample(PvSample s)
        {
            // RowIndex mora biti monoton
            if (s.RowIndex <= 0)
                return "Invalid RowIndex";

            // AcPwrt >= 0
            if (s.AcPwrt.HasValue && s.AcPwrt < 0)
                return "AcPwrt cannot be negative";

            // Naponi > 0 ako nisu null
            if (s.DcVolt.HasValue && s.DcVolt <= 0)
                return "DcVolt must be positive";
            if (s.Vl1to2.HasValue && s.Vl1to2 <= 0)
                return "Vl1to2 must be positive";
            if (s.Vl2to3.HasValue && s.Vl2to3 <= 0)
                return "Vl2to3 must be positive";
            if (s.Vl3to1.HasValue && s.Vl3to1 <= 0)
                return "Vl3to1 must be positive";
            if (s.AcVlt1.HasValue && s.AcVlt1 <= 0)
                return "AcVlt1 must be positive";

            return null;
        }

        private void RunAnalytics(PvSample s)
        {
            // ZADATAK 9: DC napon i prekidi
            if (s.DcVolt.HasValue)
            {
                // DC Sag - nagli pad napona
                if (lastDcVolt.HasValue)
                {
                    var delta = Math.Abs(s.DcVolt.Value - lastDcVolt.Value);
                    if (delta > dcSagThreshold)
                    {
                        RaiseWarning("DcSagWarning",
                            $"DC Voltage drop: {delta:F1}V exceeds {dcSagThreshold}V",
                            s.RowIndex);
                    }
                }
                lastDcVolt = s.DcVolt;

                // DC fault - DcVolt = 0 dok je AcPwrt > 0
                if (Math.Abs(s.DcVolt.Value) < 0.01 && s.AcPwrt.HasValue && s.AcPwrt > 0)
                {
                    RaiseWarning("DcFaultWarning",
                        $"DC Voltage is 0 but AC Power is {s.AcPwrt:F1}W",
                        s.RowIndex);
                }
            }
            else
            {
                // Sentinel (null) dok je snaga > 0
                if (s.AcPwrt.HasValue && s.AcPwrt > 0)
                {
                    RaiseWarning("DcFaultWarning",
                        "DC Voltage is null (sentinel) but AC Power is active",
                        s.RowIndex);
                }
            }

            // ZADATAK 10: Efikasnost i temperatura
            // Efikasnost: ACPWRT / (ACVLT1 * ACCUR1)
            if (s.AcPwrt.HasValue && s.AcVlt1.HasValue && s.AcCur1.HasValue)
            {
                var apparentPower = s.AcVlt1.Value * s.AcCur1.Value;
                if (apparentPower > 1.0) // izbegavamo deljenje sa nulom
                {
                    var efficiency = s.AcPwrt.Value / apparentPower;
                    if (efficiency < lowEfficiencyThreshold)
                    {
                        RaiseWarning("LowEfficiencyWarning",
                            $"Efficiency {efficiency:F2} below threshold {lowEfficiencyThreshold:F2}",
                            s.RowIndex);
                    }
                }
            }

            // Temperatura
            if (s.Temper.HasValue && s.Temper > overTempThreshold)
            {
                RaiseWarning("OverTempWarning",
                    $"Temperature {s.Temper:F1}°C exceeds {overTempThreshold}°C",
                    s.RowIndex);
            }

            // Power spike (iz zadatka 9 kolege)
            if (s.AcPwrt.HasValue)
            {
                recentPower.Enqueue(s.AcPwrt.Value);
                if (recentPower.Count > powerFlatlineWindow)
                    recentPower.Dequeue();

                if (recentPower.Count >= 2)
                {
                    var arr = new List<double>(recentPower).ToArray();
                    var last = arr[arr.Length - 1];
                    var prev = arr[arr.Length - 2];
                    if (Math.Abs(last - prev) > powerSpikeThreshold)
                    {
                        RaiseWarning("PowerSpikeWarning",
                            $"Power spike: change of {Math.Abs(last - prev):F1}W",
                            s.RowIndex);
                    }
                }
            }

            // Voltage imbalance
            if (s.Vl1to2.HasValue && s.Vl2to3.HasValue && s.Vl3to1.HasValue)
            {
                var v1 = s.Vl1to2.Value;
                var v2 = s.Vl2to3.Value;
                var v3 = s.Vl3to1.Value;
                var max = Math.Max(Math.Max(v1, v2), v3);
                var min = Math.Min(Math.Min(v1, v2), v3);
                var avg = (v1 + v2 + v3) / 3.0;
                var imbalance = ((max - min) / avg) * 100.0;

                if (imbalance > voltageImbalancePct)
                {
                    RaiseWarning("VoltageImbalanceWarning",
                        $"Voltage imbalance {imbalance:F1}% exceeds {voltageImbalancePct}%",
                        s.RowIndex);
                }
            }
        }

        private void RaiseWarning(string type, string message, int rowIndex)
        {
            var w = $"[{type}] Row {rowIndex}: {message}";
            warnings.Add(w);
            OnWarningRaised?.Invoke(this, new WarningEventArgs
            {
                Type = type,
                Message = message,
                RowIndex = rowIndex
            });
        }

        private void WriteSample(PvSample s)
        {
            var line = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                s.RowIndex,
                s.Day ?? "",
                s.Hour ?? "",
                s.AcPwrt?.ToString(CultureInfo.InvariantCulture) ?? "",
                s.DcVolt?.ToString(CultureInfo.InvariantCulture) ?? "",
                s.Temper?.ToString(CultureInfo.InvariantCulture) ?? "",
                s.Vl1to2?.ToString(CultureInfo.InvariantCulture) ?? "",
                s.Vl2to3?.ToString(CultureInfo.InvariantCulture) ?? "",
                s.Vl3to1?.ToString(CultureInfo.InvariantCulture) ?? "",
                s.AcCur1?.ToString(CultureInfo.InvariantCulture) ?? "",
                s.AcVlt1?.ToString(CultureInfo.InvariantCulture) ?? "");
            sessionWriter.WriteLine(line);
        }

        private void LogReject(int rowIndex, string reason, string rawData)
        {
            var escaped = rawData.Replace("\"", "\"\"");
            rejectWriter.WriteLine($"{rowIndex},\"{reason}\",\"{escaped}\"");
        }

        private string SampleToString(PvSample s)
        {
            return $"Row {s.RowIndex}, Day={s.Day}, Hour={s.Hour}, " +
                   $"AcPwrt={s.AcPwrt}, DcVolt={s.DcVolt}, Temper={s.Temper}";
        }

        private double CalcPercent()
        {
            if (currentMeta == null || currentMeta.RowLimitN == 0)
                return 0;
            return (receivedCount * 100.0) / currentMeta.RowLimitN;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                sessionWriter?.Dispose();
                rejectWriter?.Dispose();
                disposed = true;
            }
        }
    }

    // Event args klasa
    public class WarningEventArgs : EventArgs
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public int RowIndex { get; set; }
    }
}