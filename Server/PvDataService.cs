using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using System.Text;
using System.Globalization;
using Common;

namespace Server
{
    // Implementacija servisa
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class PvDataService : IPvDataService, IDisposable
    {
        private string currentSessionId;
        private PvMeta currentMeta;
        private string sessionDataPath;
        private string rejectsPath;
        private StreamWriter sessionWriter;
        private StreamWriter rejectsWriter;
        private int receivedSamplesCount;
        private DateTime sessionStartTime;
        private bool disposed = false;

        // Konfiguracija pragova iz app.config
        private double overTempThreshold;
        private double voltageImbalancePct;
        private int powerFlatlineWindow;
        private double powerSpikeThreshold;
        private double powerFlatlineEpsilon;

        // Za praćenje power flatline
        private Queue<double> recentPowerValues;
        private double? lastPowerValue;

        // Događaji
        public event EventHandler<TransferStartedEventArgs> OnTransferStarted;
        public event EventHandler<SampleReceivedEventArgs> OnSampleReceived;
        public event EventHandler<TransferCompletedEventArgs> OnTransferCompleted;
        public event EventHandler<WarningRaisedEventArgs> OnWarningRaised;

        public PvDataService()
        {
            LoadConfiguration();
            recentPowerValues = new Queue<double>();

            // Pretplate na događaje za logovanje
            OnTransferStarted += LogTransferStarted;
            OnSampleReceived += LogSampleReceived;
            OnTransferCompleted += LogTransferCompleted;
            OnWarningRaised += LogWarningRaised;
        }

        private void LoadConfiguration()
        {
            overTempThreshold = double.Parse(ConfigurationManager.AppSettings["OverTempThreshold"] ?? "50.0");
            voltageImbalancePct = double.Parse(ConfigurationManager.AppSettings["VoltageImbalancePct"] ?? "5.0");
            powerFlatlineWindow = int.Parse(ConfigurationManager.AppSettings["PowerFlatlineWindow"] ?? "10");
            powerSpikeThreshold = double.Parse(ConfigurationManager.AppSettings["PowerSpikeThreshold"] ?? "1000.0");
            powerFlatlineEpsilon = double.Parse(ConfigurationManager.AppSettings["PowerFlatlineEpsilon"] ?? "0.1");
        }

        [OperationBehavior(AutoDisposeParameters = true)]
        public SessionResult StartSession(PvMeta meta)
        {
            try
            {
                if (meta == null)
                {
                    throw new FaultException<PvFaultException>(
                        new PvFaultException("Meta data cannot be null", "INVALID_META"));
                }

                currentSessionId = Guid.NewGuid().ToString();
                currentMeta = meta;
                sessionStartTime = DateTime.Now;
                receivedSamplesCount = 0;

                // Kreiranje direktorijuma
                var basePath = ConfigurationManager.AppSettings["DataPath"] ?? "Data";
                var plantPath = Path.Combine(basePath, meta.PlantId ?? "DefaultPlant");
                var datePath = Path.Combine(plantPath, sessionStartTime.ToString("yyyy-MM-dd"));

                Directory.CreateDirectory(datePath);

                sessionDataPath = Path.Combine(datePath, "session.csv");
                rejectsPath = Path.Combine(datePath, "rejects.csv");

                // Otvaranje fajlova
                sessionWriter = new StreamWriter(sessionDataPath, append: true);
                rejectsWriter = new StreamWriter(rejectsPath, append: true);

                // Pisanje header-a ako je fajl prazan
                if (new FileInfo(sessionDataPath).Length == 0)
                {
                    sessionWriter.WriteLine("Day,Hour,AcPwrt,DcVolt,Temper,Vl1to2,Vl2to3,Vl3to1,AcCur1,AcVlt1,RowIndex");
                }

                if (new FileInfo(rejectsPath).Length == 0)
                {
                    rejectsWriter.WriteLine("RowIndex,Reason,RawData");
                }

                // Pokretanje događaja
                OnTransferStarted?.Invoke(this, new TransferStartedEventArgs
                {
                    SessionId = currentSessionId,
                    Meta = meta
                });

                return new SessionResult
                {
                    Success = true,
                    Message = "Session started successfully",
                    SessionId = currentSessionId
                };
            }
            catch (Exception ex)
            {
                throw new FaultException<PvFaultException>(
                    new PvFaultException($"Failed to start session: {ex.Message}", "SESSION_START_ERROR"));
            }
        }

        [OperationBehavior(AutoDisposeParameters = true)]
        public SampleResult PushSample(PvSample sample)
        {
            try
            {
                if (currentSessionId == null)
                {
                    throw new FaultException<PvFaultException>(
                        new PvFaultException("No active session", "NO_SESSION"));
                }

                if (sample == null)
                {
                    return new SampleResult
                    {
                        Success = false,
                        Message = "Sample is null",
                        ProcessedCount = receivedSamplesCount
                    };
                }

                // Validacija
                if (!sample.IsValid(out string validationError))
                {
                    WriteReject(sample.RowIndex, validationError, sample.ToString());
                    return new SampleResult
                    {
                        Success = false,
                        Message = $"Validation failed: {validationError}",
                        ProcessedCount = receivedSamplesCount
                    };
                }

                // Pisanje validnog uzorka
                WriteSample(sample);
                receivedSamplesCount++;

                // Analitika i upozorenja
                PerformAnalytics(sample);

                // Pokretanje događaja
                var progressPercentage = currentMeta != null ?
                    (receivedSamplesCount * 100.0 / currentMeta.RowLimitN) : 0;

                OnSampleReceived?.Invoke(this, new SampleReceivedEventArgs
                {
                    Sample = sample,
                    TotalReceived = receivedSamplesCount,
                    ProgressPercentage = progressPercentage
                });

                return new SampleResult
                {
                    Success = true,
                    Message = "Sample processed successfully",
                    ProcessedCount = receivedSamplesCount
                };
            }
            catch (Exception ex)
            {
                throw new FaultException<PvFaultException>(
                    new PvFaultException($"Failed to process sample: {ex.Message}", "SAMPLE_PROCESS_ERROR"));
            }
        }

        public SessionResult EndSession()
        {
            try
            {
                if (currentSessionId == null)
                {
                    throw new FaultException<PvFaultException>(
                        new PvFaultException("No active session", "NO_SESSION"));
                }

                var sessionId = currentSessionId;
                var duration = DateTime.Now - sessionStartTime;

                // Zatvaranje fajlova
                sessionWriter?.Flush();
                sessionWriter?.Close();
                rejectsWriter?.Flush();
                rejectsWriter?.Close();

                // Pokretanje događaja
                OnTransferCompleted?.Invoke(this, new TransferCompletedEventArgs
                {
                    SessionId = sessionId,
                    TotalSamples = receivedSamplesCount,
                    Duration = duration
                });

                // Reset stanja
                currentSessionId = null;
                currentMeta = null;
                receivedSamplesCount = 0;
                recentPowerValues.Clear();
                lastPowerValue = null;

                return new SessionResult
                {
                    Success = true,
                    Message = $"Session completed. Processed {receivedSamplesCount} samples in {duration.TotalSeconds:F1} seconds",
                    SessionId = sessionId
                };
            }
            catch (Exception ex)
            {
                throw new FaultException<PvFaultException>(
                    new PvFaultException($"Failed to end session: {ex.Message}", "SESSION_END_ERROR"));
            }
        }

        private void WriteSample(PvSample sample)
        {
            var line = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                sample.Day,
                sample.Hour,
                sample.AcPwrt?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.DcVolt?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.Temper?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.Vl1to2?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.Vl2to3?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.Vl3to1?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.AcCur1?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.AcVlt1?.ToString(CultureInfo.InvariantCulture) ?? "",
                sample.RowIndex);

            sessionWriter.WriteLine(line);
        }

        private void WriteReject(int rowIndex, string reason, string rawData)
        {
            rejectsWriter.WriteLine($"{rowIndex},\"{reason}\",\"{rawData}\"");
        }

        private void PerformAnalytics(PvSample sample)
        {
            // 1. Over-temperature check
            if (sample.Temper.HasValue && sample.Temper > overTempThreshold)
            {
                OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                {
                    WarningType = WarningType.OverTemperature.ToString(),
                    Message = $"Temperature {sample.Temper:F1}°C exceeds threshold {overTempThreshold}°C",
                    RelatedSample = sample,
                    Timestamp = DateTime.Now
                });
            }

            // 2. Voltage imbalance check
            if (sample.Vl1to2.HasValue && sample.Vl2to3.HasValue && sample.Vl3to1.HasValue)
            {
                var voltages = new[] { sample.Vl1to2.Value, sample.Vl2to3.Value, sample.Vl3to1.Value };
                var max = Math.Max(Math.Max(voltages[0], voltages[1]), voltages[2]);
                var min = Math.Min(Math.Min(voltages[0], voltages[1]), voltages[2]);
                var avg = (voltages[0] + voltages[1] + voltages[2]) / 3;
                var range = max - min;
                var imbalancePercent = (range / avg) * 100;

                if (imbalancePercent > voltageImbalancePct)
                {
                    OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                    {
                        WarningType = WarningType.VoltageImbalance.ToString(),
                        Message = $"Voltage imbalance {imbalancePercent:F1}% exceeds threshold {voltageImbalancePct}%",
                        RelatedSample = sample,
                        Timestamp = DateTime.Now
                    });
                }
            }

            // 3. Power analysis
            if (sample.AcPwrt.HasValue)
            {
                var currentPower = sample.AcPwrt.Value;

                // Power spike check
                if (lastPowerValue.HasValue)
                {
                    var powerDelta = Math.Abs(currentPower - lastPowerValue.Value);
                    if (powerDelta > powerSpikeThreshold)
                    {
                        OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                        {
                            WarningType = WarningType.PowerSpike.ToString(),
                            Message = $"Power spike detected: {powerDelta:F1}W change exceeds threshold {powerSpikeThreshold}W",
                            RelatedSample = sample,
                            Timestamp = DateTime.Now
                        });
                    }
                }

                // Power flatline check
                recentPowerValues.Enqueue(currentPower);
                if (recentPowerValues.Count > powerFlatlineWindow)
                {
                    recentPowerValues.Dequeue();
                }

                if (recentPowerValues.Count == powerFlatlineWindow)
                {
                    var powerArray = recentPowerValues.ToArray();
                    var maxPower = powerArray[0];
                    var minPower = powerArray[0];

                    for (int i = 1; i < powerArray.Length; i++)
                    {
                        if (powerArray[i] > maxPower) maxPower = powerArray[i];
                        if (powerArray[i] < minPower) minPower = powerArray[i];
                    }

                    if (Math.Abs(maxPower - minPower) < powerFlatlineEpsilon)
                    {
                        OnWarningRaised?.Invoke(this, new WarningRaisedEventArgs
                        {
                            WarningType = WarningType.PowerFlatline.ToString(),
                            Message = $"Power flatline detected over {powerFlatlineWindow} samples (variation < {powerFlatlineEpsilon}W)",
                            RelatedSample = sample,
                            Timestamp = DateTime.Now
                        });
                    }
                }

                lastPowerValue = currentPower;
            }
        }

        // Event handlers za logovanje
        private void LogTransferStarted(object sender, TransferStartedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Transfer started - Session: {e.SessionId}, File: {e.Meta.FileName}");
        }

        private void LogSampleReceived(object sender, SampleReceivedEventArgs e)
        {
            if (e.TotalReceived % 10 == 0) // Log svaki 10. uzorak
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Progress: {e.TotalReceived} samples ({e.ProgressPercentage:F1}%)");
            }
        }

        private void LogTransferCompleted(object sender, TransferCompletedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Transfer completed - Session: {e.SessionId}, Total: {e.TotalSamples}, Duration: {e.Duration.TotalSeconds:F1}s");
        }

        private void LogWarningRaised(object sender, WarningRaisedEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WARNING - {e.WarningType}: {e.Message}");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    sessionWriter?.Dispose();
                    rejectsWriter?.Dispose();
                    currentMeta?.Dispose();
                }
                disposed = true;
            }
        }

        ~PvDataService()
        {
            Dispose(false);
        }
    }
}