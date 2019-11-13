using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;

namespace JorJika.Api.WindowsService
{
    public static class WebHostTools
    {
        /// <summary>
        /// This event is raised before IWebHostBuilder.Build() method. You can add methods to the ref parameter builder.
        /// Used builder methods: .UseKestrel().UseUrls().UseContentRoot().UseStartup() 'Avoid using these methods'.
        /// </summary>
        /// <param name="builder">Web Builder</param>
        public delegate void WebHostBuilderBeforeBuildEventHandler(ref IWebHostBuilder builder);

        /// <summary>
        /// This event is raised before IWebHostBuilder.Build() method. You can add methods to the ref parameter builder.
        /// Used builder methods: .UseKestrel().UseUrls().UseContentRoot().UseStartup() 'Avoid using these methods'.
        /// </summary>
        public static event WebHostBuilderBeforeBuildEventHandler WebHostBuilderBeforeBuild;

        /// <summary>
        /// Runs application. If no arguments specified you will be entered to Instruction menu.
        /// When debugger is attached it runs application in debug mode without instruction menu.
        /// </summary>
        /// <typeparam name="TStartup">Your Startup.cs class Type</typeparam>
        /// <param name="args">arguments</param>
        /// <param name="bindHost">IP/Host to bind. If null, it will detect ethernet and get local ip from there.</param>
        /// <param name="bindPort">Port to bind</param>
        public static void Run<TStartup>(string[] args, string bindHost = null, string bindPort = "5300") where TStartup : class
        {
            Console.WriteLine("Hello there.");

            Console.WriteLine("---------------------------------------------------------------------------");
            Console.WriteLine("---------------------------------------------------------------------------");
            Console.WriteLine("---------Project is built using JorJika.Api.WindowsService package---------");
            Console.WriteLine("---------------------------------------------------------------------------");
            Console.WriteLine("---------------------------------------------------------------------------");

            var pathUrl = "";
            if (string.IsNullOrWhiteSpace(bindHost))
            {
                pathUrl = $"http://{GetLocalIp()}:{bindPort}";
            }
            else
            {
                pathUrl = $"http://{bindHost}:{bindPort}";

            }

            Console.WriteLine($"Url for binding will be used: {pathUrl}");


            if (args.Count() > 1)
            {
                Console.WriteLine("");
                Console.WriteLine("Only one argument is allowed");
                Console.ReadLine();
                return;
            }

            var mode = Debugger.IsAttached ? "--debugger" : args.FirstOrDefault()?.Trim() ?? "";
            var isService = mode == "--service";


            var pathToContentRoot = "";
            var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
            try
            {
                switch (mode)
                {
                    case "--console":
                        pathToContentRoot = Directory.GetCurrentDirectory();
                        break;

                    case "--docker":
                        pathToContentRoot = Directory.GetCurrentDirectory();
                        pathUrl = $"http://*:{bindPort}";
                        break;

                    case "--service":

                        pathToContentRoot = Path.GetDirectoryName(pathToExe);
                        break;

                    case "--debugger":
                        pathToContentRoot = Directory.GetCurrentDirectory();

                        break;
                    default:

                    instructions:

                        FileInfo executableInfo = new FileInfo(pathToExe);
                        string fileName = executableInfo.Name.Replace(executableInfo.Extension, "");

                        Console.WriteLine("");
                        Console.WriteLine("----------------");
                        Console.WriteLine("--Instructions--");
                        Console.WriteLine("----------------");
                        Console.WriteLine("1) Install and run Windows Service");
                        Console.WriteLine("2) Uninstall Windows Service");
                        Console.WriteLine("3) Just Run from here");
                        Console.WriteLine("4) Check local ip bind function");

                        var result = Console.ReadLine();
                        int resultInt;
                        if (!int.TryParse(result, out resultInt))
                        {
                            Console.WriteLine("You must type only a number from 1 to 4");
                            goto instructions;
                        }

                        if (!(resultInt >= 1 && resultInt <= 4))
                        {
                            Console.WriteLine("You must type only a number from 1 to 4");
                            goto instructions;
                        }

                        switch (resultInt)
                        {
                            case 1:
                                try
                                {
                                    try
                                    {
                                        StopService(fileName);
                                        System.Threading.Thread.Sleep(1000);
                                        UninstallService(pathToExe);
                                    }
                                    catch { }

                                    try
                                    {
                                        InstallAndRunService(pathToExe);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Error in InstallAndRunService method:");
                                        Console.WriteLine(ex.Message);
                                        goto instructions;
                                    }

                                    Console.WriteLine("Press any key to close this window");
                                    Console.ReadLine();
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Exception:");
                                    Console.WriteLine(ex.Message);
                                    Console.WriteLine("");
                                    Console.WriteLine("Press any key to return instructions...");
                                    Console.ReadLine();

                                    goto instructions;
                                }

                            case 2:
                                try
                                {

                                    StopService(fileName);
                                    System.Threading.Thread.Sleep(1000);
                                    UninstallService(pathToExe);

                                    Console.WriteLine("Press any key to close this window");
                                    Console.ReadLine();
                                    return;
                                }
                                catch (Exception ex)
                                {

                                    Console.WriteLine("Exception:");
                                    Console.WriteLine(ex.Message);
                                    Console.WriteLine("");
                                    Console.WriteLine("Press any key to return instructions...");
                                    Console.ReadLine();

                                    goto instructions;
                                }

                            case 4:
                                try
                                {
                                    Console.WriteLine("LocalIp for Bind will be used by Default:");
                                    Console.WriteLine(GetLocalIp());
                                    Console.WriteLine("");
                                    Console.WriteLine("Press any key to return instructions...");
                                    Console.ReadLine();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Exception:");
                                    Console.WriteLine(ex.Message);
                                    Console.WriteLine("");
                                    Console.WriteLine("Press any key to return instructions...");
                                    Console.ReadLine();
                                    goto instructions;
                                }
                                goto instructions;

                            default:
                                pathToContentRoot = Directory.GetCurrentDirectory();
                                break;
                        }

                        break;
                }



                Uri hostUri = new Uri(pathUrl.Replace("*", "127.0.0.1")); //for docker

                var builder = WebHost.CreateDefaultBuilder(args.Where(arg => arg != "--console" && arg != "--service" && arg != "--debugger").ToArray())
                 .UseKestrel()
                 .UseUrls($"{hostUri.Scheme}://{(mode == "--docker" ? "*" : hostUri.Host)}:{hostUri.Port}")
                 .UseContentRoot(pathToContentRoot)
                 .UseStartup<TStartup>();

                WebHostBuilderBeforeBuild?.Invoke(ref builder);

                var host = builder.Build();
                var serverFeatures = host.ServerFeatures.Get<IServerAddressesFeature>();
                if (serverFeatures.Addresses.Count == 0)
                {
                    Console.WriteLine("Server Features object has changed");
                    serverFeatures.Addresses.Add(hostUri.AbsoluteUri);
                }
                else
                    Console.WriteLine("Server Features object has not changed");

                if (isService)
                    host.RunAsService();
                else
                    host.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (!isService) Console.ReadLine();
            }
        }

        public static string GetLocalIp()
        {
            var ipaddress = "";
            var firstUpInterface = NetworkInterface.GetAllNetworkInterfaces()
                                                    .OrderBy(x => x.NetworkInterfaceType.ToString())
                                                    .OrderByDescending(c => c.Speed)
                                                    .FirstOrDefault(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                                                     && (c.NetworkInterfaceType == NetworkInterfaceType.Ethernet || c.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                                                     && !c.Name.Contains("bluetooth")
                                                                     && c.OperationalStatus == OperationalStatus.Up
                                                                     );
            if (firstUpInterface != null)
            {
                var props = firstUpInterface.GetIPProperties();
                // get first IPV4 address assigned to this interface
                var firstIpV4Address = props.UnicastAddresses
                    .Where(c => c.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(c => c.Address)
                    .FirstOrDefault();

                if (firstIpV4Address != null)
                    ipaddress = firstIpV4Address.ToString();
            }

            return ipaddress;
        }

        public static void InstallAndRunService(string pathToExe)
        {
            Console.WriteLine("Starting Service...");
            FileInfo fi = new FileInfo(pathToExe);

            ServiceInstaller.InstallAndStart(fi.Name.Replace(fi.Extension, ""), fi.Name.Replace(fi.Extension, ""), $"{pathToExe} --service");
        }

        private static void StartService(string pathToExe)
        {
            const string sc = @"cmd.exe";
            string arguments = @"/C sc start ""{0}""";
            FileInfo fi = new FileInfo(pathToExe);

            Console.WriteLine("Starting Service");

            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = sc,
                Arguments = string.Format(arguments, fi.Name.Replace(fi.Extension, "")),
                WorkingDirectory = fi.DirectoryName,
                CreateNoWindow = true,
                UseShellExecute = false

            };

            System.Threading.Thread.Sleep(1000);
            using (var process = new Process())
            {
                process.StartInfo = psi;
                process.Start();
                process.WaitForExit();
            }
        }

        public static void UninstallService(string pathToExe)
        {
            FileInfo fi = new FileInfo(pathToExe);
            var svc = fi.Name.Replace(fi.Extension, "");

            using (var currentService = ServiceController.GetServices(Environment.MachineName).FirstOrDefault(s => s.ServiceName == svc))
            {
                if (currentService != null)
                {
                    currentService.Refresh();
                    if (currentService.Status == ServiceControllerStatus.Stopped)
                    {
                        Console.WriteLine($"Uninstalling {svc}");
                        ServiceInstaller.Uninstall(svc);
                    }

                }
            }
        }

        public static void StopService(string svc)
        {
            //ServiceInstaller.StopService(svc);

            using (var currentService = ServiceController.GetServices(Environment.MachineName).FirstOrDefault(s => s.ServiceName == svc))
            {
                if (currentService != null)
                {
                    Console.WriteLine($"Service {svc} detected");

                    currentService.Refresh();
                    if (currentService.Status != ServiceControllerStatus.Stopped)
                    {
                        Console.WriteLine($"Stopping Service: {svc}");
                        currentService.Stop();
                        Console.WriteLine($"Service: {svc} - Stopped.");

                    }
                    else
                        Console.WriteLine($"Service {svc} is already Stopped");
                }
            }
        }
    }
}
