
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MonitorRecursosLinux
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            System.Timers.Timer timer = new();
            timer.Elapsed += async (sender, args) => await OnTimer();
            timer.Interval = Convert.ToDouble(2000);
            timer.Enabled = true;
            timer.Start();

            Console.WriteLine("Press \'q\' to exit");
            while (Console.Read() != 'q') ;
        }

        private static string cpuReadingsLinux;
        private static string networkReadingsLinux;

        private static async Task OnTimer()
        {
            var time = DateTime.Now;
            Console.WriteLine("Starting at " + time);
            List<string> SystemMacs = GetMacAddresses();
            string SystemName = Environment.MachineName;
            string OsNameVersion = RuntimeInformation.OSDescription;
            string CpuInfo = GetCpuInfo();
            int CpuCores = Environment.ProcessorCount;
            double TotalMemory = GetTotalMemory();
            SortedList<string, double> CreateDiskSpecs = GetDisks();
            UpdateCpuReadingsLinux();
            UpdateNetworkReadingsLinux();
            UpdateDiskReadingsLinux();
            double TotalUsageCPU = GetCpuTotalUsage();
            SortedList<string, double> CPUPerCoreUsage = GetCpuPerCoreUsage();
            SortedList<string, double> DiskUsage = GetDiskUsage();
            float RemainMemory = GetRemainingMemory();
            float SystemUptime = GetSystemUptime();

            Console.WriteLine("System Name: " + SystemName);
            Console.WriteLine("Os Version: " + OsNameVersion);
            SystemMacs.ForEach(Console.WriteLine);

            Console.WriteLine("Disk Specs. ");
            foreach (KeyValuePair<string, double> kvp in CreateDiskSpecs)
                Console.WriteLine("key: {0}, value: {1}", kvp.Key, kvp.Value);

            Console.WriteLine("CPU: " + CpuInfo);
            Console.WriteLine("CPU Cores: " + CpuCores);
            Console.WriteLine("Total Memory: " + TotalMemory);
            Console.WriteLine("Total CPU Usage: " + TotalUsageCPU);

            Console.WriteLine("CPU Usage Per Core. ");
            foreach (KeyValuePair<string, double> kvp in CPUPerCoreUsage)
                Console.WriteLine("key: {0}, value: {1}", kvp.Key, kvp.Value);

            Console.WriteLine("Disk Usage. ");
            foreach (KeyValuePair<string, double> kvp in DiskUsage)
                Console.WriteLine("key: {0}, value: {1}", kvp.Key, kvp.Value);

            Console.WriteLine("Remain Memory: " + RemainMemory);
            Console.WriteLine("System UpTime: " + SystemUptime);

        }

        private static List<string> GetMacAddresses()
        {
            var result = new List<string>();
            var adapters = GetAllNetworkAdapters();
            foreach (var adapter in adapters)
            {
                var command = new ProcessStartInfo("cat")
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"cat /sys/class/net/" + adapter + "/address\"",
                    RedirectStandardOutput = true
                };
                using (var process = Process.Start(command))
                {
                    if (process == null)
                    {
                        throw new Exception("Error when executing process: " + command.Arguments);
                    }
                    result.Add(process.StandardOutput.ReadToEnd()[..^1]);
                }
            }
            return result;
        }

        private static List<string> GetAllNetworkAdapters()
        {
            var command = new ProcessStartInfo("tcpdump")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"tcpdump --list-interfaces\"",
                RedirectStandardOutput = true
            };
            var commandOutput = "";
            using (var process = Process.Start(command))
            {
                if (process == null)
                {
                    throw new Exception("Error when executing process: " + command.Arguments);
                }
                commandOutput = process.StandardOutput.ReadToEnd();
            }
            var omitInterfaces = new string[] { "Loopback", "Pseudo-device", "none", "Bluetooth adapter", "Linux netfilter" };
            var networkAdapters = commandOutput.Split("\n", StringSplitOptions.RemoveEmptyEntries).Where(x => !omitInterfaces.Any(y => x.Contains(y)) && x.Contains("Running")).ToList();
            for (int i = 0; i < networkAdapters.Count; i++)
            {
                networkAdapters[i] = networkAdapters[i][(networkAdapters[i].IndexOf('.') + 1)..].Split(" ")[0];
            }
            return networkAdapters;
        }

        private static string GetCpuInfo()
        {
            var command = new ProcessStartInfo("cat")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"cat /proc/cpuinfo | grep 'model name' | uniq\"",
                RedirectStandardOutput = true
            };
            var commandOutput = "";
            using (var process = Process.Start(command))
            {
                if (process == null)
                {
                    throw new Exception("Error when executing process: " + command.Arguments);
                }
                commandOutput = process.StandardOutput.ReadToEnd();
            }
            return commandOutput[(commandOutput.IndexOf(':') + 1)..][1..^1];
        }

        private static double GetTotalMemory()
        {
            var command = new ProcessStartInfo("cat")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"cat /proc/meminfo | grep 'MemTotal'\"",
                RedirectStandardOutput = true
            };
            var commandOutput = "";
            using (var process = Process.Start(command))
            {
                if (process == null)
                {
                    throw new Exception("Error when executing process: " + command.Arguments);
                }
                commandOutput = process.StandardOutput.ReadToEnd();
            }
            return Convert.ToDouble(commandOutput.Split(" ", StringSplitOptions.RemoveEmptyEntries)[^2]);
        }

        private static SortedList<string, double> GetDisks()
        {
            var result = new SortedList<string, double>();
            var command = new ProcessStartInfo("lsblk")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"lsblk -bdno NAME,SIZE\"",
                RedirectStandardOutput = true
            };
            var commandOutput = "";
            using (var process = Process.Start(command))
            {
                if (process == null)
                {
                    throw new Exception("Error when executing process: " + command.Arguments);
                }
                commandOutput = process.StandardOutput.ReadToEnd();
            }
            foreach (var line in commandOutput.Split("\n", StringSplitOptions.RemoveEmptyEntries))
            {
                var size = Convert.ToDouble(line.Split(" ", StringSplitOptions.RemoveEmptyEntries)[1][..^1].Replace(',', '.'));
                result[line.Split(" ")[0]] = size;
                //result.Add(new CreateDiskSpecs()
                //{
                //    DiskName = line.Split(" ")[0],
                //    DiskSize = size
                //});
            }
            return result;
        }

        private static void UpdateCpuReadingsLinux()
        {
            var command = new ProcessStartInfo("mpstat")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"mpstat -P ALL 1 1\"",
                RedirectStandardOutput = true
            };
            using var process = Process.Start(command);
            if (process == null)
            {
                throw new Exception("Error when executing process: " + command.Arguments);
            }
            cpuReadingsLinux = process.StandardOutput.ReadToEnd();
        }

        private static void UpdateNetworkReadingsLinux()
        {
            var adapters = GetAllNetworkAdapters();
            var command = new ProcessStartInfo("ifstat")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"ifstat -i " + string.Join(',', adapters) + " 1 1\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(command);
            if (process == null)
            {
                throw new Exception("Error when executing process: " + command.Arguments);
            }
            networkReadingsLinux = process.StandardOutput.ReadToEnd();
        }

        private static void UpdateDiskReadingsLinux()
        {
            var command = new ProcessStartInfo("iostat")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"iostat -dxy 1 1\"",
                RedirectStandardOutput = true
            };
            using (var process = Process.Start(command))
            {
                if (process == null)
                {
                    throw new Exception("Error when executing process: " + command.Arguments);
                }
                cpuReadingsLinux = process.StandardOutput.ReadToEnd();
            }
        }

        private static double GetCpuTotalUsage()
        {
            var lines = cpuReadingsLinux.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            var usage = 100.0 - Convert.ToDouble(lines[^(Environment.ProcessorCount + 1)].Split(" ", StringSplitOptions.RemoveEmptyEntries)[^1].Replace(',', '.'));
            return usage;
        }

        private static SortedList<string, double> GetCpuPerCoreUsage()
        {
            var usage = new SortedList<string, double>();
            var lines = cpuReadingsLinux.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - Environment.ProcessorCount; i < lines.Length; i++)
            {
                var instanceName = lines[i].Split(" ", StringSplitOptions.RemoveEmptyEntries)[1];
                var instanceUsage = 100.0 - Convert.ToDouble(lines[i].Split(" ", StringSplitOptions.RemoveEmptyEntries)[^1].Replace(',', '.'));
                usage[instanceName] = instanceUsage;
            }
            return (SortedList<string, double>)usage.OrderBy(kvp => kvp.Key);
        }

        private static SortedList<string, double> GetDiskUsage()
        {
            SortedList<string, double> usage = new();
            var lines = cpuReadingsLinux.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            for (int i = 2; i < lines.Length; i++)
            {
                var instanceName = lines[i].Split(" ", StringSplitOptions.RemoveEmptyEntries)[0];
                var instanceUsage = Convert.ToDouble(lines[i].Split(" ", StringSplitOptions.RemoveEmptyEntries)[^1].Replace(',', '.'));
                usage[instanceName] = instanceUsage;
            }
            return (SortedList<string, double>)usage.OrderBy(kvp => kvp.Key);
        }

        private static float GetRemainingMemory()
        {
            var command = new ProcessStartInfo("free")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"free -m\"",
                RedirectStandardOutput = true
            };
            var commandOutput = "";
            using (var process = Process.Start(command))
            {
                if (process == null)
                {
                    throw new Exception("Error when executing process: " + command.Arguments);
                }
                commandOutput = process.StandardOutput.ReadToEnd();
            }
            var usage = commandOutput.Split("\n")[1].Split(" ", StringSplitOptions.RemoveEmptyEntries)[^1];
            return float.Parse(usage);
        }

        private static float GetSystemUptime()
        {
            var command = new ProcessStartInfo("cat")
            {
                FileName = "/bin/bash",
                Arguments = "-c \"cat /proc/uptime\"",
                RedirectStandardOutput = true
            };
            var commandOutput = "";
            using (var process = Process.Start(command))
            {
                if (process == null)
                {
                    throw new Exception("Error when executing process: " + command.Arguments);
                }
                commandOutput = process.StandardOutput.ReadToEnd();
            }
            return float.Parse(commandOutput.Split(" ")[0]);
        }
    }
}
